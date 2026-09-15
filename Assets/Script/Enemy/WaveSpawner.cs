using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

public class WaveSpawner : MonoBehaviour
{

    [Tooltip("적이 따라갈 격자 맵. 인스펙터에서 주입(Find 함수 미사용 지침).")]
    [SerializeField] private MapBoard board;
    public MapBoard Board => board;
    [Tooltip("이 스포너가 담당하는 지역(레인) 번호. SpawnerManager가 이 값으로 매핑한다.")]
    [SerializeField] private int region = 1;
    public int Region => region;
    public int Enemycount;
    [Tooltip("구버전 표시용 폴백. 팝업 프리팹에 StageInfoView가 붙어 있으면 쓰이지 않는다.")]
    public TMP_Text text;
    [Tooltip("스테이지 정보 팝업(아이콘 행 + 툴팁). SpawnerManager가 클릭 때마다 넣어준다.")]
    public StageEnemyInfoView infoView;
    [Tooltip("보스 라운드에서 일반 몹은 즉시, 보스는 이 시간(초) 뒤에 등장.")]
    private float bossSpawnDelay = 18f;
    
    private WaveTable waveTable;
    public IReadOnlyList<Vector3> waypoints; // 단일 경로 폴백(레인 정보가 없을 때만 사용).

    [Tooltip("스폰 타일별 경로(레인)를 제공. board와 같은 오브젝트에 붙는다. 비어 있으면 런타임에 자동 부착.")]
    [SerializeField] private EnemyLanes enemyLanes;
    [Tooltip("이번 라운드에 활성화할 포탈(레인) 최소 개수.")]
    [SerializeField] private int minActivePortals = 1;
    [Tooltip("이번 라운드에 활성화할 포탈(레인) 최대 개수.")]
    [SerializeField] private int maxActivePortals = 3;
    [Tooltip("공중·수영 적이 따라갈 저작 경로(Tools/Enemy/Enemy Route Maker로 그린다). " +
             "이 board의 모듈에 맞는 에셋을 꽂는다. 비워 두면 지금까지처럼 자동 길찾기로만 움직인다.")]
    [SerializeField] private EnemyRouteSet enemyRoutes;
    public EnemyRouteSet EnemyRoutes => enemyRoutes;

    // 스폰 번호가 가리키는 실제 스폰 칸 좌표. 트레일 미리보기가 실제 스폰과 같은 기준으로 저작 경로를 찾을 때 쓴다.
    public Vector2Int SpawnCoord(int spawnIndex) => _spawnTiles[spawnIndex].Coord;

    // 스폰 타일별 전체 경로(날짜가 바뀌기 전까지 캐시). GetPaths가 스폰당 1경로를 준다.
    private IReadOnlyList<IReadOnlyList<Vector3>> _allPaths;
    // enemyLanes.Changed 중복 구독 방지용. EnsurePaths가 캐시 히트로 일찍 끝나도 한 번만 걸린다.
    private bool _hookedLaneChanges;
    // 이번 라운드에 활성화된 경로들. 포탈 표시(낮)와 적 경로 배분(밤)이 이 집합을 공유한다.
    private readonly List<IReadOnlyList<Vector3>> _activePaths = new();
    // 이번 라운드에 활성화된 포탈(스폰) 번호. _activePaths와 같은 순서라야 그 포탈의 갈래를 고를 수 있다.
    public IReadOnlyList<int> ActiveSpawns => _activeSpawns;
    // 활성 레인 추첨용 임시 버퍼(GC 회피).
    private readonly List<IReadOnlyList<Vector3>> _laneBuffer = new();
    // 활성 포탈의 스폰 번호. _activePaths와 같은 순서라야 그 포탈의 갈래를 고를 수 있다.
    private readonly List<int> _activeSpawns = new();
    private readonly List<int> _spawnBuffer = new();
    // 스폰 번호 → 스폰 타일. EnemyLanes가 레인을 담는 순서(ApplyLanes)와 같게 만든다 —
    // 그래야 _activeSpawns의 번호로 그 포탈의 좌표를 짚어 저작 경로를 찾을 수 있다.
    private readonly List<Tile> _spawnTiles = new();
    // 저작 경로가 있는 활성 포탈 후보(GC 회피).
    private readonly List<int> _routedPortals = new();
    private readonly List<EnemyRouteSet.Entry> _entryBuffer = new();
    // 진단 로그가 좌표 목록을 찍을 때 쓰는 버퍼. 두 목록을 한 문장에 같이 넣으므로 따로 둔다.
    private readonly List<Vector2Int> _spawnCoordBuffer = new();
    private readonly List<Vector2Int> _spawnCoordBuffer2 = new();
    // "저작 경로가 없다" 경고를 라운드마다 종류별 한 번만 남긴다 — 매 스폰마다 찍으면 콘솔이 잠긴다.
    private readonly HashSet<EnemyRouteKind> _warnedKinds = new();
    /// <summary>이번 라운드에 켜진 포탈(레인)들의 경로. 각 경로 [0]이 포탈 위치.</summary>
    public IReadOnlyList<IReadOnlyList<Vector3>> ActivePaths => _activePaths;

    // 스코프에 등록되면 주입됨. 아니면 Spawn 시 Instance로 폴백.
    private PoolManager _pool;
    private SpawnerManager spawnerManager;
    [Inject] public void Construct(PoolManager pool,SpawnerManager spawnerManager)
    {
        _pool = pool;
        this.spawnerManager = spawnerManager;
    }
    
    public event Action EnemyAllClear;
    private void Start()
    {
        waveTable = DataTableManager.Get<WaveTable>(DataTableIds.Wave);
        if (waveTable == null)
        {
            Debug.LogWarning("WaveSpawner: WaveTable을 찾을 수 없음");
            return;
        }

        if (board == null)
            Debug.LogWarning("WaveSpawner: MapBoard가 주입되지 않았습니다. 적이 이동하지 않습니다.", this);
        else
        {
            waypoints = board.GetWaypoints(0f); // 레인 정보가 없을 때 쓰는 단일 경로 폴백
            EnsurePaths();                      // 스폰 타일별 경로(레인) 캐시
            EnemyGridService.mapBoard = board;
        }
       ResetText();
    }

    // 스폰 타일별 경로(레인)를 확보한다. EnemyLanes가 없으면 board에 자동 부착한다.
    // EnemyLanes.Awake(실행순서 100)가 board.Awake 이후 레인을 굽는다 → 어떤 Start 순서에서도 안전.
    private void EnsurePaths()
    {
        if (_allPaths != null || board == null) return;
        if (enemyLanes == null)
            enemyLanes = board.GetComponent<EnemyLanes>() ?? board.gameObject.AddComponent<EnemyLanes>();
        if (!_hookedLaneChanges)
        {
            enemyLanes.Changed += OnLanesChanged; // 날짜가 바뀌어 레인이 다시 구워지면 캐시를 버리고 다시 받는다
            _hookedLaneChanges = true;
        }
        _allPaths = enemyLanes.GetPaths(0f);
        RebuildSpawnTiles();
    }

    // 스폰 번호로 스폰 타일을 짚을 표를 만든다. EnemyLanes.ApplyLanes가 레인을 훑는 순서대로
    // 처음 보는 Start를 쌓으면 그쪽 spawnLanes 번호와 그대로 짝이 맞는다.
    private void RebuildSpawnTiles()
    {
        _spawnTiles.Clear();
        if (enemyLanes == null) return;

        IReadOnlyList<LaneData> lanes = enemyLanes.Lanes;
        for (int i = 0; i < lanes.Count; i++)
        {
            Tile start = lanes[i].Start;
            if (start == null) continue;
            if (!_spawnTiles.Contains(start)) _spawnTiles.Add(start);
        }
    }

    // EnemyLanes가 레인을 다시 구울 때(날짜 변경 등) 호출된다. 캐시를 비우고 그 자리에서 새 경로로 다시 채운다.
    private void OnLanesChanged()
    {
        _allPaths = null;
        EnsurePaths();
    }

    private void OnDestroy()
    {
        if (enemyLanes != null)
            enemyLanes.Changed -= OnLanesChanged;
    }

    // 이번 라운드에 켤 포탈(레인)을 min~max 범위에서 랜덤 개수만큼 활성화한다.
    // 포탈 테이블 없이 굴리는 폴백 경로(테스트·보정용) — SpawnWave가 "추첨 없이 스폰됐다"를 감지했을 때 부른다.
    //
    // pathSeed를 넘기면 개수까지 그 시드로 뽑는다. 안 그러면 이 한 경로만 매번 달라져
    // "시드를 넣었는데 가끔 포탈이 바뀐다"가 된다 — 실제 게임에서도 SpawnWave 안에서 도달 가능한 길이다.
    public int RollActivePortals(string pathSeed = null)
    {
        int span = Mathf.Max(1, maxActivePortals - minActivePortals + 1);
        if (string.IsNullOrEmpty(pathSeed))
            return RollActivePortals(UnityEngine.Random.Range(minActivePortals, maxActivePortals + 1), null);

        // 개수와 조합을 같은 시드에서 뽑되 유도 문자열을 나눈다 — 하나의 난수기를 순차로 쓰면
        // 개수가 1 달라졌을 때 뒤따르는 조합 추첨까지 통째로 밀린다.
        var countRng = new System.Random(GameSeeding.Derive($"{pathSeed}:portalcount", 0));
        return RollActivePortals(minActivePortals + countRng.Next(0, span),
            new System.Random(GameSeeding.Derive($"{pathSeed}:portalpick", 0)));
    }

    // count개의 레인을 랜덤으로 활성화한다. 유효 경로가 count보다 적으면 있는 만큼만.
    // 반환: 실제 활성화된 포탈 수.
    //
    // rng를 넘기면 그걸로 뽑는다 — 같은 난수기면 항상 같은 포탈이 열리므로,
    // (게임 시드, 지역, 일차)로 만든 난수기를 주면 세이브를 다시 로드해도 그날 포탈이 재현된다.
    // null이면 예전처럼 전역 UnityEngine.Random을 쓴다(시드를 못 얻는 테스트 경로용).
    public int RollActivePortals(int count, System.Random rng = null)
    {
        EnsurePaths();
        _activePaths.Clear();
        _activeSpawns.Clear();
        _warnedKinds.Clear(); // 포탈 구성이 바뀌었으니 저작 누락 경고를 이 라운드에 다시 낼 수 있게 한다

        _laneBuffer.Clear();
        _spawnBuffer.Clear();
        if (_allPaths != null)
            for (int i = 0; i < _allPaths.Count; i++)
                if (_allPaths[i] != null && _allPaths[i].Count > 0) { _laneBuffer.Add(_allPaths[i]); _spawnBuffer.Add(i); }

        if (_laneBuffer.Count == 0) // 레인 정보 없음 → 단일 경로 폴백
        {
            if (waypoints != null && waypoints.Count > 0) { _activePaths.Add(waypoints); _activeSpawns.Add(NoSpawn); }
            return _activePaths.Count;
        }

        int take = Mathf.Clamp(count, 1, _laneBuffer.Count);
        for (int i = 0; i < take; i++) // Fisher-Yates 부분 셔플로 take개 뽑기
        {
            // rng.Next(i, n)과 Random.Range(i, n) 둘 다 상한 배타라 뽑는 범위가 같다.
            int j = rng != null ? rng.Next(i, _laneBuffer.Count) : UnityEngine.Random.Range(i, _laneBuffer.Count);
            (_laneBuffer[i], _laneBuffer[j]) = (_laneBuffer[j], _laneBuffer[i]);
            (_spawnBuffer[i], _spawnBuffer[j]) = (_spawnBuffer[j], _spawnBuffer[i]);
            _activePaths.Add(_laneBuffer[i]);
            _activeSpawns.Add(_spawnBuffer[i]);
        }
        return _activePaths.Count;
    }

    // 코어에서 가장 먼(=경로가 가장 긴) 스폰 레인 하나만 고정 활성화한다. 보스 라운드의 "끝 구석" 포탈용.
    // 경로는 스폰→코어 순서라 Count가 클수록 코어에서 멀다. 동률이면 앞선 레인(좌표순 정렬) → 결정적.
    public int ActivateCornerPortal()
    {
        EnsurePaths();
        _activePaths.Clear();
        _activeSpawns.Clear();
        _warnedKinds.Clear(); // 포탈 구성이 바뀌었으니 저작 누락 경고를 이 라운드에 다시 낼 수 있게 한다

        IReadOnlyList<Vector3> corner = null;
        int best = -1;
        int cornerSpawn = NoSpawn;
        if (_allPaths != null)
            for (int i = 0; i < _allPaths.Count; i++)
            {
                var p = _allPaths[i];
                if (p == null || p.Count == 0) continue;
                if (p.Count > best) { best = p.Count; corner = p; cornerSpawn = i; }
            }

        if (corner == null) { corner = waypoints; cornerSpawn = NoSpawn; } // 레인 정보 없으면 단일 경로 폴백
        if (corner != null && corner.Count > 0) { _activePaths.Add(corner); _activeSpawns.Add(cornerSpawn); }
        return _activePaths.Count;
    }

    // 레인 정보 없이 켠 폴백 경로라 스폰 번호가 없다는 표시.
    private const int NoSpawn = -1;

    // 저장된 스폰 칸 좌표들을 그대로 활성화한다 (NightReady 로드 전용)
    public int ActivateSpawnsAt(IReadOnlyList<Vector2Int> savedCoords)
    {
        // 길 목록이 아직 안 만들어졌으면 만든다
        EnsurePaths();
        // 지금까지 켜져 있던 길·번호를 비우고, 저작 누락 경고도 다시 낼 수 있게 한다
        _activePaths.Clear();
        _activeSpawns.Clear();
        _warnedKinds.Clear();

        // 저장된 좌표 하나마다
        for (int i = 0; i < savedCoords.Count; i++)
        {
            // 그 좌표가 몇 번 스폰 칸인지 찾는다
            int spawn = FindSpawnIndex(savedCoords[i]);
            // 지금 맵에 없는 좌표면 건너뛴다 (저장 당시와 맵이 다를 때만 생김)
            if (spawn < 0) continue;
            // 그 번호의 길과 번호를 활성 목록에 넣는다
            _activePaths.Add(_allPaths[spawn]);
            _activeSpawns.Add(spawn);
        }

        return _activePaths.Count;
    }

    // 좌표와 같은 스폰 칸의 인덱스를 찾는다 (ActivateSpawnsAt 전용)
    private int FindSpawnIndex(Vector2Int coord)
    {
        for (int i = 0; i < _spawnTiles.Count; i++)
        {
            if (_spawnTiles[i].Coord == coord) return i;
        }
        return -1;
    }

    // 인덱스 하나를 뽑는다. rng가 있으면 그걸로(재현됨), 없으면 예전처럼 전역 Random.
    // System.Random.Next(0,n)과 UnityEngine.Random.Range(0,n) 둘 다 상한 배타라 범위가 같다.
    private static int Pick(System.Random rng, int count) =>
        rng != null ? rng.Next(0, count) : UnityEngine.Random.Range(0, count);

    /// <summary>이 적이 따라갈 경로. authored=true면 사람이 그린 경로라 적이 재탐색하지 않는다.
    ///
    /// 공중·수영 적은 그 종류의 저작 경로가 있는 포탈에서만 나온다 — 여러 곳에 그려 뒀으면 그중 랜덤.
    /// 활성 포탈에 그 종류 경로가 하나도 없으면 경고를 남기고 기존 자동 경로로 돌아간다
    /// (웨이브가 비어 라운드가 깨지는 것보다 낫다).
    ///
    /// rng는 이 적 한 마리만의 난수기다(SpawnWaveRout이 마리마다 새로 만든다). null이면 전역 Random —
    /// 시드를 못 얻는 테스트 경로에서만 그렇게 된다.</summary>
    private IReadOnlyList<Vector3> NextSpawnPath(EnemyRouteKind kind, System.Random rng, out bool authored)
    {
        authored = false;

        // 저작 경로를 못 쓴 이유를 전부 남긴다. 조용히 폴백하면 "기능이 안 되는 것"과 구분이 안 된다.
        if (kind == EnemyRouteKind.None)
        {
            // 지상 적 — 저작 경로 대상이 아니다(맵 레인을 그대로 쓴다).
        }
        else if (enemyRoutes == null)
        {
            Warn(kind, "WaveSpawner의 enemyRoutes가 비어 있습니다 — 인스펙터에 EnemyRouteSet을 꽂아 주세요.");
        }
        else if (board == null)
        {
            Warn(kind, "board가 주입되지 않아 저작 경로를 해석할 수 없습니다.");
        }
        else
        {
            IReadOnlyList<Vector3> drawn = AuthoredPath(kind, rng);
            if (drawn != null)
            {
                authored = true;
                return drawn;
            }
        }

        if (_activePaths.Count == 0) return waypoints;
        return BranchPath(Pick(rng, _activePaths.Count), rng);
    }

    // 종류별로 한 번만 남긴다 — 매 스폰마다 찍으면 콘솔이 잠긴다. 라운드가 바뀌면 다시 낸다.
    private void Warn(EnemyRouteKind kind, string reason)
    {
        if (!_warnedKinds.Add(kind)) return;
        Debug.LogWarning($"[{name}] {KindWord(kind)} 적이 저작 경로를 쓰지 못했습니다 — {reason} " +
            "자동 길찾기로 대신 움직입니다.", this);
    }

    // 이 종류의 저작 경로가 있는 활성 포탈 중 하나를 랜덤으로 골라 웨이포인트를 만든다.
    // 쓸 경로가 없으면 null — 호출부가 기존 자동 경로로 폴백한다.
    private IReadOnlyList<Vector3> AuthoredPath(EnemyRouteKind kind, System.Random rng)
    {
        _routedPortals.Clear();

        for (int i = 0; i < _activeSpawns.Count; i++)
        {
            int spawn = _activeSpawns[i];
            if (spawn < 0 || spawn >= _spawnTiles.Count) continue; // 레인 정보 없이 켠 폴백 포탈(NoSpawn)
            if (enemyRoutes.Has(kind, _spawnTiles[spawn].Coord)) _routedPortals.Add(spawn);
        }

        if (_routedPortals.Count == 0)
        {
            // 활성 포탈 좌표와 에셋에 그려진 좌표를 나란히 찍는다 — 둘이 아예 안 겹치면
            // 다른 모듈에 그린 것이다(좌표는 모듈 로컬 0-base라 모듈이 다르면 같은 숫자가 다른 칸을 뜻한다).
            enemyRoutes.SpawnsOf(kind, _spawnCoordBuffer);
            Warn(kind, $"활성 포탈 {ActivePortalWord()}에 그려진 경로가 없습니다 " +
                       $"(에셋에 그려진 {KindWord(kind)} 스폰: {CoordWord(_spawnCoordBuffer)}).");
            return null;
        }

        int pick = _routedPortals[Pick(rng, _routedPortals.Count)];
        Vector2Int coord = _spawnTiles[pick].Coord;

        // 같은 스폰에 여러 벌 그렸으면 갈래로 보고 랜덤 — 기존 BranchPath와 같은 취급이다.
        enemyRoutes.Collect(kind, coord, _entryBuffer);
        if (_entryBuffer.Count == 0) return null;

        EnemyRouteSet.Entry entry = _entryBuffer[Pick(rng, _entryBuffer.Count)];
        List<Vector3> points = EnemyRoutePath.Build(board, entry);

        if (points.Count == 0)
        {
            if (_warnedKinds.Add(kind))
                Debug.LogWarning($"[{name}] {KindWord(kind)} 저작 경로가 본진까지 이어지지 않습니다 " +
                    $"(스폰 {coord}) — 자동 길찾기로 대신 움직입니다.", this);
            return null;
        }

        return points;
    }

    private static string KindWord(EnemyRouteKind kind)
    {
        if (kind == EnemyRouteKind.Air) return "공중";
        if (kind == EnemyRouteKind.Swim) return "수영";
        return "지상";
    }

    // 이번 라운드에 켜진 포탈의 좌표. 스폰 표가 비었으면 그 사실을 알린다(레인이 아직 안 구워진 경우).
    private string ActivePortalWord()
    {
        if (_spawnTiles.Count == 0) return "(스폰 표가 비어 있음 — 레인이 구워지지 않았습니다)";

        _spawnCoordBuffer2.Clear();
        for (int i = 0; i < _activeSpawns.Count; i++)
        {
            int spawn = _activeSpawns[i];
            if (spawn < 0 || spawn >= _spawnTiles.Count) continue;
            _spawnCoordBuffer2.Add(_spawnTiles[spawn].Coord);
        }

        return CoordWord(_spawnCoordBuffer2);
    }

    private static string CoordWord(List<Vector2Int> coords)
    {
        if (coords.Count == 0) return "없음";

        var text = new System.Text.StringBuilder();
        for (int i = 0; i < coords.Count; i++)
        {
            if (i > 0) text.Append(", ");
            text.Append('(').Append(coords[i].x).Append(',').Append(coords[i].y).Append(')');
        }

        return text.ToString();
    }

    // 이 포탈에서 갈라지는 길 중 하나. 갈래가 하나뿐이거나 막혀 있으면 대표 경로 그대로.
    private IReadOnlyList<Vector3> BranchPath(int pick, System.Random rng)
    {
        int spawn = _activeSpawns[pick];
        if (enemyLanes == null || spawn == NoSpawn) return _activePaths[pick];

        int branches = enemyLanes.BranchCount(spawn);
        if (branches <= 1) return _activePaths[pick];

        var branch = enemyLanes.GetBranchPath(spawn, Pick(rng, branches), 0f);
        if (branch == null || branch.Count == 0) return _activePaths[pick];
        return branch;
    }
    public void ResetText()
    {
        if (infoView != null) infoView.Clear();
        if(text == null || string.IsNullOrEmpty(text.text))return;

        text.text = string.Empty;
    }
    public void SpawnWave(int currentStage, string pathSeed = null)
    {
        Enemycount =0;
        if (_activePaths.Count == 0) RollActivePortals(pathSeed); // 포탈 추첨 없이 스폰되면(테스트 등) 여기서 보정
        int lookupId = GetStageLookupId(currentStage); // 10일차 초과는 1001~1005 라운드로 순환 조회

        int row = 0;   // 웨이브 행 순번 — 경로 추첨 시드의 일부다(자세한 이유는 SpawnWaveRout 주석)
        foreach(var wave in waveTable.GetWave(1,lookupId))
        {
            int count = GetScaleCount(wave.Count, currentStage); // 라운드가 돌수록 마릿수 스케일업
            SpawnWaveRout(wave, count, 0f, this.GetCancellationTokenOnDestroy(), pathSeed, row++).Forget();
            Enemycount += count;
        }
    }

    // [애널리틱스 비활성화] private float _roundStartTime;
    // [애널리틱스 비활성화] private int _roundDayCount;

    /// <param name="pathSeed">이 지역·이 밤의 경로 추첨 시드 문자열. SpawnerManager가 게임 시드로 만들어 넘긴다.
    /// null이면 예전처럼 전역 Random으로 경로를 뽑는다(시드를 못 얻는 테스트 씬).
    /// 필드로 들고 있지 않고 매번 인자로 받는 이유 — 포탈은 낮에 굴리고 적은 밤에 스폰되는데,
    /// 그 사이에 RefreshPortals/RestorePortal이나 일차 복원이 끼어들면 필드에 남은 시드가 다른 날의 것이 된다.
    /// 스폰하는 그 자리에서 만들어 넘기면 그 어긋남이 구조적으로 불가능하다.</param>
    public void SpawnWave(int region,int currentStage, int unlockedRegionCount = 1, int? scaleStage = null, string pathSeed = null)
    {
        Enemycount =0;
        if (_activePaths.Count == 0) RollActivePortals(pathSeed); // 포탈 추첨 없이 스폰되면 여기서 보정
        int lookupId = GetStageLookupId(currentStage);
        int scale = scaleStage ?? currentStage;
        // 웨이브 행 순번. 일반·증원·보스 세 갈래를 통틀어 하나로 센다 —
        // 갈래마다 0부터 다시 세면 서로 다른 행의 첫 마리가 같은 시드를 받아 전부 같은 포탈로 몰린다.
        int row = 0;
        foreach(var wave in waveTable.GetWave(region,lookupId))
        {
            int count = GetScaleCount(wave.Count, scale); // 라운드가 돌수록 마릿수 스케일업
            SpawnWaveRout(wave, count, 0f, this.GetCancellationTokenOnDestroy(), pathSeed, row++).Forget();
            Enemycount += count;
        }
        for (int tier = 0; tier < ReinforceTiers(unlockedRegionCount); tier++)
        {
            foreach (var wave in waveTable.GetWave(region, ReinforceBaseId + tier))
            {
                SpawnWaveRout(wave, wave.Count, 0f, this.GetCancellationTokenOnDestroy(), pathSeed, row++).Forget(); // 증원은 라운드 배율을 안 붙인다(표에 적은 마릿수 그대로)
                Enemycount += wave.Count;
            }
        }

        if (region == 1 && currentStage >= 10 && currentStage % 10 == 0)
        {
            // 씬 전환/오브젝트 파괴 시 연출이 알아서 취소되도록 UniTask의 파괴 토큰을 쓴다.
            // 지역 CancellationTokenSource는 아무도 Cancel/Dispose하지 않아 사실상 None과 같았다.
            spawnerManager.BossOpeningDirecting(this.GetCancellationTokenOnDestroy(), bossSpawnDelay - 0.5f).Forget();
            foreach (var w in waveTable.GetWave(1, 5001))
            {
                SpawnWaveRout(w, w.Count, bossSpawnDelay, this.GetCancellationTokenOnDestroy(), pathSeed, row++).Forget();
                Enemycount += w.Count;
            }
        }
        // [애널리틱스 비활성화] _roundStartTime = Time.time;
        // [애널리틱스 비활성화] _roundDayCount = currentStage;
        // [애널리틱스 비활성화] AnalyticsRecorder.RoundStart(region, currentStage, Enemycount);
    }


    // 씬 전환/오브젝트 파괴 시(예: 전투 중 타이틀로 나가기) 대기 중이던 스폰이 알아서 취소되도록
    // GetCancellationTokenOnDestroy()를 받는다 - BossOpeningDirecting과 같은 패턴. 이게 없으면
    // WaveSpawner와 GameLifeTimeScope가 이미 파괴된 뒤에 Delay가 끝나 PoolManager.Spawn을 호출하고,
    // VContainer가 사라진 씬에서 WaveSpawner를 FindComponentProvider로 다시 찾으려다 예외를 던진다.
    //
    // pathSeed·row는 이 웨이브 행에서 나올 적들의 경로를 재현하기 위한 것이다.
    // 마리마다 (시드, 행 순번, 마릿수 인덱스)로 난수기를 새로 만든다 — 난수기 하나를 돌려 쓰지 않는 이유:
    // 이 메서드는 웨이브 행마다 .Forget()으로 동시에 돌고 SpawnTime/Delay만큼 await하므로,
    // 어느 행의 몇 번째 적이 먼저 뽑느냐가 프레임 타이밍에 따라 달라진다. 순차 소비형 난수기였다면
    // 그 순서가 흔들리는 순간 같은 시드에서도 다른 경로가 나온다. 키에 순번을 박아 두면 순서와 무관해진다.
    private async UniTask SpawnWaveRout(WaveTable.Data wave, int count, float startDelay, CancellationToken cancellationToken,
        string pathSeed = null, int row = 0)
    {
        var prefab = waveTable.GetMonsterPrefab(wave);
        if (prefab == null)
        {
            Debug.LogWarning($"WaveSpawner: 프리팹 로드 실패 '{wave.Prefab}' (ID {wave.ID})");
            return;
        }
        // 시작 지연(보스 지연 등) → 그 위에 웨이브별 SpawnTime을 더한다.
        if (startDelay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: cancellationToken);
        if (wave.SpawnTime > 0f) await UniTask.Delay(TimeSpan.FromSeconds(wave.SpawnTime), cancellationToken: cancellationToken);

        for (int i = 0; i < count; i++)
        {

            var go = (_pool ??= PoolManager.Instance).Spawn(prefab, Vector3.zero, Quaternion.identity);
            if (go.TryGetComponent(out EnemyBase enemy))
            {
                enemy.SetOwner(this);
                // 공중·수영은 저작 경로가 있는 포탈에서만, 나머지는 활성 포탈 중 랜덤.
                IReadOnlyList<Vector3> path = NextSpawnPath(enemy.RouteKind, PathRng(pathSeed, row, i), out bool authored);
                enemy.EnterMap(board, path, true, authored);
            }

            if (i < count - 1 && wave.Delay > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(wave.Delay), cancellationToken: cancellationToken);
        }
    }
    // 적 한 마리의 경로 추첨 난수기. 같은 (게임 시드, 지역, 일차, 웨이브 행, 마릿수 인덱스)면 항상 같은 경로가 나온다.
    // 시드가 없으면 null — 호출부(Pick)가 전역 Random으로 돌아간다.
    private static System.Random PathRng(string pathSeed, int row, int index)
    {
        if (string.IsNullOrEmpty(pathSeed)) return null;
        return new System.Random(GameSeeding.Derive($"{pathSeed}:row{row}", index));
    }

    public void EnemyDieEvent()
    {
        Enemycount--;
        if(Enemycount<=0)
        {
            // [애널리틱스 비활성화] AnalyticsRecorder.RoundEnd(region, _roundDayCount, Time.time - _roundStartTime);
            EnemyAllClear?.Invoke();
            // Debug.Log("적 전멸 이벤트 발생");
        }
    }

    public void AddSpawnCount(int n) => Enemycount += n;

    // 스테이지 정보 표시. infoView가 있으면 적 아이콘 + x마릿수 행으로, 없으면 예전처럼 텍스트 한 덩어리로 쓴다.
    // (프리팹에 StageInfoView를 아직 붙이지 않은 상태에서도 게임이 돌아가도록 폴백을 남겨둠)
    public void OnClickStage(int region,int currentstage, int unlockedRegionCount = 1, int? scaleStage = null)
    {
        if (waveTable == null) return;                          // Start 전 클릭 방어
        if (infoView == null && text == null) return;           // 표시할 대상이 아무것도 없음

        int lookupId = GetStageLookupId(currentstage);
        int scale = scaleStage ?? currentstage;
        // 이번 클릭 내용만 남게 매번 비우고 시작한다(안 비우면 클릭할수록 목록이 쌓인다).
        if (infoView != null) infoView.Begin();
        else text.text = string.Empty;

        // 일반 웨이브와 증원을 한 덩어리로 합쳐서 센다.
        // 증원을 "(증원)" 배지가 붙은 별도 줄로 띄우지 않는 이유: 플레이어에게 필요한 정보는
        // "오늘 밤 이 몹이 몇 마리 오는가"뿐이라, 같은 몹이면 마릿수를 더하고
        // 증원에만 나오는 몹은 일반 몹과 똑같은 [아이콘] x마릿수 모양으로 붙인다.
        _stageCounts.Clear();
        _stageOrder.Clear();

        foreach(var w in waveTable.GetWave(region,lookupId))
            AccumulateStageCount(w.MonsterName, GetScaleCount(w.Count, scale));

        // 마릿수는 SpawnWave와 같이 배율 없는 원본 그대로 — 여기서 GetScaleCount를 붙이면
        // 팝업이 실제 스폰보다 많은 수를 알려준다.
        for (int tier = 0; tier < ReinforceTiers(unlockedRegionCount); tier++)
        {
            foreach (var w in waveTable.GetWave(region, ReinforceBaseId + tier))
                AccumulateStageCount(w.MonsterName, w.Count);
        }

        for (int i = 0; i < _stageOrder.Count; i++)
            AddStageLine(_stageOrder[i], _stageCounts[_stageOrder[i]], null);

        // 보스는 합치지 않는다 — 그 밤이 보스전이라는 것 자체가 정보라 배지를 남긴다.
        if(currentstage>=10&&currentstage%10==0&&region==1)
        {
            foreach(var w in waveTable.GetWave(1,5001))
                AddStageLine(w.MonsterName, w.Count, "Ui_Boss");
        }
    }

    // 같은 몹은 한 줄로 합치되 처음 나온 순서를 지킨다(표에 적은 순서 = 보여주고 싶은 순서).
    // 매 클릭마다 Clear해서 재사용한다 — 팝업은 자주 열리는데 여기서 컬렉션을 새로 만들 이유가 없다.
    private readonly List<string> _stageOrder = new();
    private readonly Dictionary<string, int> _stageCounts = new();

    private void AccumulateStageCount(string monsterName, int count)
    {
        if (string.IsNullOrEmpty(monsterName)) return;
        if (_stageCounts.TryGetValue(monsterName, out int prev))
        {
            _stageCounts[monsterName] = prev + count;
            return;
        }
        _stageCounts[monsterName] = count;
        _stageOrder.Add(monsterName);
    }

    // 적 한 종류를 한 줄로 추가. badgeKey: "Ui_Boss"(보스) / null(일반).
    // 증원은 배지를 달지 않고 일반 줄에 마릿수만 합쳐 넣는다(AccumulateStageCount).
    // WaveTable.MonsterName과 EnemyTable.Name은 같은 키라 그대로 조회한다(아이콘·설명도 이 키 기준).
    private void AddStageLine(string monsterName, int count, string badgeKey)
    {
        if (infoView != null)
        {
            EnemyTable.Data data = DataTableManager.EnemyTable?.Get(monsterName);
            if (data != null) { infoView.AddRow(data, count, badgeKey); return; }
            // EnemyTable에 행이 없는 몹은 아이콘/설명을 만들 수 없다 → 이름만이라도 남긴다.
            Debug.LogWarning($"WaveSpawner: EnemyTable에 '{monsterName}' 없음 — 이름만 표시");
        }
        if (text == null) return;

        var st = DataTableManager.StringTable;
        string badge = string.IsNullOrEmpty(badgeKey) ? string.Empty : $"({st.Get(badgeKey)})";
        text.text += $"{st.Get(monsterName)} x {count}{badge}\n";
    }

    public static int GetScaleCount(int baseCount,int currentStage)
    {
        if(currentStage<=10)return baseCount;
        int loops = (currentStage-6)/5;
        return Mathf.RoundToInt(baseCount*(1f+loops*0.3f));
    }
    public static int GetStageLookupId(int stage)
    {
        return stage > 10 ? ((stage-6)%5)+1001 : stage;
    }

    // 지역이 해금될수록 그 지역 웨이브에 덧붙는 "증원 몹" 전용 ID 대역의 시작.
    // 단계별로 9001, 9002, 9003 … 순으로 이어진다.
    // 일반 라운드(1~10, 1001~1005)와 겹치지 않으므로 정상 웨이브로는 절대 스폰되지 않는다.
    //
    // 예전에는 A지역 9001을 "다른" 해금 지역들로 흘려보냈지만, 지역마다 테마가 있는 맵 컨셉과 어긋나서
    // 자기 지역 몹이 단계별로 더 나오는 방식으로 바꿨다.
    public const int ReinforceBaseId = 9001;

    // 해금 지역이 N개면 증원은 N-1단계 — 지역이 하나뿐이면 증원이 없고, 하나 열릴 때마다 한 단계씩 붙는다.
    // (예전에 "자기 지역 제외"로 N-1개 지역에서 끌어오던 것과 겹수가 같아 기존 밸런스를 그대로 잇는다.)
    public static int ReinforceTiers(int unlockedRegionCount) => Mathf.Max(0, unlockedRegionCount - 1);
}
