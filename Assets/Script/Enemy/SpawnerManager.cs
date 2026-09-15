using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer;

// 독립 레인(지역) 스포너들을 한곳에서 관리한다.
// - 지역별 WaveSpawner를 인스펙터로 등록 → 지역번호로 조회
// - 지역 해금 상태(IsUnlockregion) 관리
// - 해금된 지역들에만 웨이브 스폰 지시
// - 모든 해금 지역이 전멸하면 AllRegionsClear 통지
public class SpawnerManager : MonoBehaviour
{
    [SerializeField] private List<WaveSpawner> spawners = new();

    [SerializeField] private List<int> startUnlocked = new() { 1 };

    // 지역번호 → 스포너
    private readonly Dictionary<int, WaveSpawner> _byRegion = new();
    // 지역번호 → 해금 여부
    public Dictionary<int, bool> IsUnlockregion = new();
    // 지역번호 → 그 지역이 해금된 시점의 글로벌 DayCount - 1. LocalStage 계산용 오프셋.
    // 시작 해금 지역(startUnlocked)은 0으로 둬서 기존처럼 글로벌 DayCount와 로컬 진행도가 같게 유지한다.
    private readonly Dictionary<int, int> _unlockOffset = new();
    public ModuleLogic[] moduleLogics;
    public GameObject spawnPoint;
    private float yOffset = 1f;
    // 지역번호 → 그 지역에 이번 라운드 활성화된 포탈들(레인마다 1개).
    public Dictionary<int,List<GameObject>> spawnPoints = new();

    [Tooltip("스테이지 정보 패널(고정 UI). 포탈을 클릭하면 켜지고 그 지역 웨이브로 내용이 채워진다. " +
             "포탈마다 월드 팝업을 스폰하던 방식을 대체한다 — 패널은 하나뿐이고 SetActive로만 껐다 켠다.")]
    [SerializeField] private StageEnemyInfoView stageInfoPanel;
    // 지금 패널에 내용을 채워 넣은 지역. 닫을 때 그 스포너의 참조만 끊으면 되므로 들고 있는다.
    private int _shownRegion = -1;
    // 해금된 모든 지역의 적이 전멸했을 때 1회 발생.
    public Camera cam;
    public event Action AllRegionsClear;
    private static SpawnerManager instance;
    public static SpawnerManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("SpawnerManager");
                instance = go.AddComponent<SpawnerManager>();
            }
            return instance;
        }
    }

    // UiManager가 ESC로 메뉴를 열지 판단할 때 쓴다(HasEscapeCloseTarget) - 이 패널이 떠 있는 채로
    // ESC를 누르면 패널만 닫혀야지 메뉴까지 같이 열리면 안 된다. Instance 프로퍼티를 쓰지 않는 이유는
    // 그쪽은 null이면 빈 GameObject를 새로 만들어버려서, 스포너가 없는 씬에서 매 프레임 이 체크만으로
    // 유령 SpawnerManager가 생기는 부작용이 생기기 때문이다.
    public static bool IsStageInfoOpen =>
        instance != null && instance.stageInfoPanel != null && instance.stageInfoPanel.gameObject.activeSelf;
    private bool changeCheck;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        BuildRegistry();
        foreach(var m in moduleLogics)
        {
            if(m==null)continue;
            m.OnStateChanged += state =>
                UnlockRegion(m.ModuleId,m.CurrentState);
        }
        changeCheck = true;
        HideStageInfos();
    }

    private void OnDestroy()
    {
        foreach (var s in spawners)
            if (s != null) s.EnemyAllClear -= OnRegionClear;
        
        if(_gameManager!=null)
        {
            _gameManager.ChangeToDay-=ChangeDay;
            _gameManager.ChangeToNight-=ChangeNight;
        }
    }


    void Update()
    {
        if(changeCheck)
        InputClick();
    }
    private void InputClick()
    {
        if (cam == null) cam = Camera.main;          // 인스펙터 미할당 시 메인 카메라로 폴백
        if (cam == null || Mouse.current == null) return;
        if(Time.timeScale==0)return;
        if(!Mouse.current.leftButton.wasPressedThisFrame) return;
        // 팝업(월드 스페이스 캔버스) 위를 클릭한 거면 여기선 아무것도 하지 않는다.
        // 이 가드가 없으면 아이콘을 눌러도 레이가 스폰 타일을 못 맞춰 HideStageInfos()가 돌고
        // 툴팁이 뜨는 같은 프레임에 팝업이 사라진다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        foreach (var kv in _byRegion)
        {
            WaveSpawner spawner = kv.Value;
            if (spawner == null || spawner.Board == null) continue;

            Tile tile = spawner.Board.CellFromRay(ray);
            if (tile == null || !tile.isEnemySpawn) 
            {
                HideStageInfos();
                continue;
            }
            if(!IsUnlocked(kv.Key))continue;
            ShowStageInfo(kv.Key, tile); // 스테이지 정보 패널을 켜고 그 지역의 웨이브 정보로 채운다
            return;                                            // 맞는 지역 하나 찾으면 끝
        }
    }
    private IObjectResolver _resolver;
    [Inject] public void Construct(IObjectResolver resolver) => _resolver = resolver;
    private GameManager _gameManager;
    private int CurrentDay
    {
        get
        {
            if (_gameManager == null && _resolver != null) _gameManager = _resolver.Resolve<GameManager>();
            return _gameManager != null ? _gameManager.DayCount : 0;
        }
    }

    // 지역별 로컬 진행도. 그 지역이 해금된 날을 1일차로 다시 센다 —
    // 나중에 해금된 지역이 글로벌 DayCount 배율을 그대로 물려받아 첫 웨이브부터 몰리는 걸 막는다.
    // 오프셋은 해금되는 순간이나 정보 조회 시점이 아니라, 그 지역의 첫 밤 스폰(SpawnStage) 때 확정한다.
    // ResultState처럼 UnlockNextModule()이 OnDay()로 DayCount가 오르기 "직전"에 불리는 경로가 있어서,
    // 그 사이에 굳히면(해금 이벤트와 같은 프레임에 트레일·포탈 조회가 끼어든다) 아직 안 오른 DayCount
    // 기준으로 고정되어 그 지역이 영구히 하루씩 밀린다.
    public int LocalStage(int region)
    {
        // 조회 전용 — 오프셋을 굳히지 않는다. 잠긴 지역, 그리고 해금됐지만 아직 첫 밤을 안 지난
        // 지역(해금은 DayCount가 오르기 직전에 일어난다)은 둘 다 1일차로 답한다.
        if (!IsUnlocked(region)) return 1;
        return _unlockOffset.TryGetValue(region, out int off) ? CurrentDay - off : 1;
    }

    // 실제 스폰 시점의 진행도. 오프셋을 굳히는 건 이 경로에서만 — 이 시점의 DayCount는 이미 그 밤의
    // 값이라 "아직 안 오른 DayCount로 굳는" 어긋남이 구조적으로 불가능하다.
    private int SpawnStage(int region) => CurrentDay - EnsureOffset(region);

    /// <summary>
    /// 이 지역의 오프셋을 돌려주고, 아직 없으면 지금 일차를 1일차로 삼아 정해 넣는다.
    ///
    /// 오프셋을 굳히는 곳은 여기 하나뿐이라야 한다 — LocalStage와 GetOffset이 각자 굳히던 때는
    /// 세이브(GetUnlockDay→GetOffset)가 먼저 굳혀 버리면 아래 로그가 아예 안 찍혀서,
    /// "제대로 잡혔는데 로그가 안 뜬 것"과 "여전히 틀린 것"을 구분할 수 없었다.
    ///
    /// 로그를 남기는 이유: 오프셋은 지역당 한 번만 정해지고, 한 번 틀리면 그 지역 웨이브가 영구히 어긋난다
    /// (0으로 굳으면 나중에 열린 지역이 첫 밤부터 1001 대역으로 스폰된다). 게다가 이제는 그 값이
    /// RegionSave.unlockDay로 세이브에 박히므로 되돌릴 수도 없다. 지역 수만큼만 찍혀 콘솔을 어지럽히지 않는다.
    /// </summary>
    private int EnsureOffset(int region)
    {
        if (_unlockOffset.TryGetValue(region, out int off)) return off;

        off = CurrentDay - 1;
        _unlockOffset[region] = off;
        // Debug.Log($"SpawnerManager: {region}지역 진행도 기준 확정 — 오프셋 {off} " +
        //           $"(글로벌 {CurrentDay}일차 = 이 지역 {CurrentDay - off}일차)", this);
        return off;
    }

    void Start()
    {
        if (_gameManager == null && _resolver != null)
        _gameManager = _resolver.Resolve<GameManager>();
        if(_gameManager!=null)
        {
            _gameManager.ChangeToDay+=ChangeDay;
            _gameManager.ChangeToNight+=ChangeNight;
        }
        ShowAllPortals();
    }
    private void ChangeDay()
    {
        changeCheck =true;
        HideAllPortals();
        ShowAllPortals();
    }
    private void ChangeNight()
    {
        changeCheck = false;
        HideStageInfos(); // 밤엔 정보 패널을 닫는다
    }

    // 해금된 모든 지역에 포탈 표시(낮). 이미 떠 있으면 중복 생성하지 않는다.
    private void ShowAllPortals()
    {
        if (spawnPoint == null) return;
        foreach (var kv in _byRegion)
            if (IsUnlocked(kv.Key)) ShowPortal(kv.Key);
    }

    // 한 지역에만 포탈 표시. 확장(해금) 시에도 이 함수로 즉시 생성한다.
    // 이번 라운드에 켤 레인을 랜덤으로 뽑아, 활성 레인 시작점마다 포탈을 하나씩 세운다.
    private void ShowPortal(int region)
    {
        if (spawnPoint == null) return;
        if (spawnPoints.TryGetValue(region, out var existing) && existing != null && existing.Count > 0) return; // 이미 존재

        if (!_byRegion.TryGetValue(region, out var spawner) || spawner == null || spawner.Board == null) return;
        
        // 보스 라운드(10의 배수 일차)엔 1지역만 코어에서 가장 먼 "끝 구석" 포탈 1개로 고정.
        // 그 외엔 포탈 테이블 Count만큼 랜덤 활성화(밤 스폰과 같은 집합 공유).
        if (region == 1 && CurrentDay > 0 && CurrentDay % 10 == 0)
            spawner.ActivateCornerPortal();
        else
            spawner.RollActivePortals(PortalCount(region), PortalRng(region));
        var paths = spawner.ActivePaths;
        if (paths == null || paths.Count == 0) return;

        // 켜진 길들 위에 포탈을 세운다 (RestorePortal과 같이 쓰는 헬퍼)
        SpawnPortalsFor(region, paths);
    }

    // 포탈 테이블(Region,Id,Count)에서 이번 지역·라운드에 열 포탈 수를 읽는다.
    // Id는 WaveTable과 같은 라운드 체계(10일차 초과는 1001~1005 순환). 행이 없으면 1개.
    private int PortalCount(int region)
    {
        PortalTable table = DataTableManager.Get<PortalTable>(DataTableIds.Portal);
        if (table == null) return 1;
        int id = WaveSpawner.GetStageLookupId(LocalStage(region));
        return table.GetCount(region, id, 1);
    }

    // 이 지역·이 일차의 포탈을 뽑을 난수기.
    // 같은 (게임 시드, 지역, 일차)면 항상 같은 포탈이 열린다 — 세이브를 다시 로드해도 그날 포탈이 그대로 재현된다.
    //
    // 영웅 뽑기(ConsumeHeroDraw)처럼 저장되는 순번이 필요 없는 이유:
    // 뽑기는 플레이어가 임의 횟수로 부르지만 포탈은 지역·일차마다 정확히 한 번만 굴리므로,
    // (지역, 일차) 쌍 자체가 순번 노릇을 한다. 그래서 SaveData에 추가할 필드가 없다.
    //
    // 일차는 LocalStage가 아니라 CurrentDay를 쓴다 — LocalStage는 지역마다 값이 겹치고
    // 10일차 넘어가면 순환하지만, CurrentDay는 밤마다 전역에서 유일하다.
    private bool _warnedNoSeed;

    private System.Random PortalRng(int region)
    {
        int day = CurrentDay;   // 이 게터가 필요하면 _gameManager를 여기서 resolve한다
        string seed = _gameManager != null ? _gameManager.GameSeed : null;
        // 시드를 못 얻으면(테스트 씬 등) null을 돌려 예전처럼 전역 Random으로 굴리게 둔다.
        // 다만 조용히 넘기면 "왜 아직도 포탈이 매번 다르지"의 원인을 못 찾는다 — 한 번만 알린다.
        if (string.IsNullOrEmpty(seed))
        {
            if (!_warnedNoSeed)
            {
                _warnedNoSeed = true;
                Debug.LogWarning("SpawnerManager: GameManager의 시드를 얻지 못해 포탈을 전역 Random으로 뽑습니다 " +
                                 "— 로드할 때마다 포탈이 달라집니다.", this);
            }
            return null;
        }
        return new System.Random(GameSeeding.Derive($"{seed}:portal:{region}", day));
    }

    // 이 지역·이 밤의 "적 경로 추첨" 시드 문자열. WaveSpawner가 이걸 받아 적 한 마리마다
    // (행 순번, 마릿수 인덱스)를 덧붙여 난수기를 만든다 — 같은 시드·같은 날이면 어느 적이 어느 포탈에서
    // 어느 갈래로 나오는지까지 그대로 재현된다.
    //
    // 포탈 추첨(PortalRng)과 유도 문자열을 나눠 둔다("portal" vs "path"). 같은 문자열을 쓰면
    // 포탈 개수 하나만 바뀌어도 경로 배분까지 통째로 달라져, 둘 중 무엇 때문에 바뀌었는지 못 가린다.
    //
    // 스폰하는 그 자리에서 만들어 넘긴다(WaveSpawner에 필드로 심어 두지 않는다) —
    // RefreshPortals·RestorePortal이나 로드 직후 일차 복원이 낮과 밤 사이에 끼어들 수 있어서다.
    private string PathSeed(int region)
    {
        int day = CurrentDay;   // 이 게터가 _gameManager를 resolve한다 — seed를 먼저 읽으면 아직 null일 수 있다
        string seed = _gameManager != null ? _gameManager.GameSeed : null;
        if (string.IsNullOrEmpty(seed)) return null;   // PortalRng가 이미 한 번 경고했다
        return $"{seed}:path:{region}:{day}";
    }

    // 세이브 로드가 끝난 뒤 포탈을 다시 뽑는다.
    //
    // 왜 필요한가: LoadManager는 모든 Start()가 끝난 '다음 프레임'에 복원한다(UniTask.Yield).
    // 그런데 이 매니저의 Start()는 그 전 프레임에 이미 ShowAllPortals()로 포탈을 굴려버린다 —
    // 그 시점의 DayCount는 아직 복원 전 값이라, 시드 유도식이 엉뚱한 일차로 계산된다.
    // 예전엔 어차피 랜덤이라 티가 안 났지만 시드를 쓰면 '항상 같지만 틀린' 포탈이 나온다.
    // 복원이 끝난 뒤 이걸 한 번 불러주면 올바른 일차로 다시 뽑는다.
    //
    // NightReady 저장본은 RestorePortal이 좌표를 그대로 되살리므로 부르면 안 된다(그 밤의 포탈이 바뀐다).
    public void RefreshPortals()
    {
        HideAllPortals();
        ShowAllPortals();
    }

    private void HideAllPortals()
    {
        if (spawnPoint == null) return;
        foreach (var kv in spawnPoints)
        {
            if (kv.Value == null) continue;
            foreach (var portal in kv.Value)
                if (portal != null) PoolManager.Instance.Despawn(portal);
        }
        spawnPoints.Clear();
    }

    // 구역 클릭 시: 고정 스테이지 정보 패널을 켜고 그 지역의 웨이브 정보로 채운다.
    // 패널은 씬에 하나뿐이라 지역을 바꿔 클릭하면 같은 패널의 내용만 갈아끼운다.
    private void ShowStageInfo(int region, Tile clickedTile)
    {
        if (stageInfoPanel == null) return;
        if (!spawnPoints.TryGetValue(region, out var portals) || portals == null || portals.Count == 0) return; // 포탈 없으면 표시 안 함
        if (!_byRegion.TryGetValue(region, out var spawner) || spawner == null) return;

        // 클릭한 스폰 칸 위에 실제로 포탈이 서 있는지만 확인한다(포탈은 레인 시작 칸에 세워지므로 좌표가 일치).
        // 이번 라운드에 안 뽑혀 포탈이 없는 칸이면 적이 나오지 않는 자리 — 패널을 띄우지 않고 떠 있던 것도 닫는다.
        if (FindPortalAt(spawner.Board, portals, clickedTile) == null) { HideStageInfos(); return; }

        // 다른 지역을 눌렀으면 이전 지역의 참조부터 끊는다(스포너 하나가 패널을 계속 물고 있지 않게).
        if (_shownRegion >= 0 && _shownRegion != region) UnbindSpawner(_shownRegion);

        stageInfoPanel.Show();
        // 툴팁 스탯이 EnemyBase와 같은 배율을 쓰려면 글로벌 일차가 필요하다.
        // 패널은 컨테이너에 등록돼 있지 않아 GameManager를 직접 주입받지 못하므로 여기서 넘긴다.
        stageInfoPanel.SetDay(CurrentDay);
        spawner.infoView = stageInfoPanel;
        spawner.text = null;   // 패널을 쓰는 동안은 TMP 한 덩어리 폴백을 죽여둔다
        _shownRegion = region;
        spawner.OnClickStage(region, LocalStage(region), UnlockedCount(), CurrentDay); // 웨이브/증원/보스 정보 기록
    }

    // 클릭한 칸과 같은 격자 좌표에 서 있는 포탈을 찾는다. 없으면 null(그 칸은 이번 라운드에 안 뽑힌 스폰 지점).
    private static GameObject FindPortalAt(MapBoard board, List<GameObject> portals, Tile clickedTile)
    {
        if (board == null || clickedTile == null) return null;

        Vector2Int want = clickedTile.Coord;
        foreach (GameObject portal in portals)
        {
            if (portal == null) continue;
            if (board.WorldToCell(portal.transform.position) == want) return portal;
        }
        return null;
    }

    // 패널을 닫는다. 스폰 칸이 아닌 곳을 클릭했을 때와 밤 전환 때 불린다.
    // 패널은 하나뿐이므로 스폰/회수 없이 SetActive만 내린다.
    private void HideStageInfos()
    {
        if (_shownRegion >= 0) UnbindSpawner(_shownRegion);
        _shownRegion = -1;
        if (stageInfoPanel != null) stageInfoPanel.Hide();
    }

    // 패널을 물고 있던 스포너의 참조를 끊는다. 끊기 전에 ResetText로 떠 있던 툴팁/행을 먼저 정리해야
    // 다음에 다른 지역이 같은 패널을 쓸 때 지난 내용이 남지 않는다.
    private void UnbindSpawner(int region)
    {
        if (!_byRegion.TryGetValue(region, out var spawner) || spawner == null) return;
        spawner.ResetText();
        spawner.infoView = null;
        spawner.text = null;
    }
    private void BuildRegistry()
    {
        _byRegion.Clear();
        foreach (var s in spawners)
        {
            if (s == null) continue;
            _byRegion[s.Region] = s;
            if (!IsUnlockregion.ContainsKey(s.Region))
                IsUnlockregion[s.Region] = false;

            s.EnemyAllClear -= OnRegionClear; 
            s.EnemyAllClear += OnRegionClear; 
        }

        foreach (int region in startUnlocked)
        {
            IsUnlockregion[region] = true;
            _unlockOffset[region] = 0; // 글로벌 DayCount와 로컬 진행도를 그대로 일치시킨다
        }
    }

    public void UnlockRegion(int region,ModuleState state)
    {
        if(state!=ModuleState.Locked)
        {
            // 오프셋은 여기서 계산하지 않는다 — LocalStage가 이 지역을 처음 조회하는 시점에 확정한다(위 주석 참고).
            IsUnlockregion[region] = true; //해금 할때 씀
            if (changeCheck) ShowPortal(region); // 낮에 확장하면 확장된 곳에 즉시 포탈 생성
        }
        else
        {
            IsUnlockregion[region] = false;
        }
    }

    public bool IsUnlocked(int region)
        => IsUnlockregion.TryGetValue(region, out bool v) && v; //해금 확인용

    // 현재 해금된 지역 번호 목록. 증원 소스로 각 스포너에 넘긴다(스포너가 자기 지역은 알아서 제외).
    public List<int> UnlockedRegions()
    {
        var list = new List<int>();
        foreach (var kv in _byRegion)
            if (IsUnlocked(kv.Key)) list.Add(kv.Key);
        return list;
    }

    // 해금 지역 수를 스포너 밖(EnemyBase의 체력 배율 등)에서 읽기 위한 진입점.
    // Instance 프로퍼티는 매니저가 없으면 빈 오브젝트를 새로 만들어버리므로 여기서는 쓰지 않는다 —
    // 매니저가 없는 씬(적 단독 테스트 등)에서는 0을 돌려주고, 호출부가 배율 1배로 폴백하게 둔다.
    public static int UnlockedRegionCount => instance != null ? instance.UnlockedCount() : 0;

    // 해금된 지역 수. 각 지역의 증원 단계(9001, 9002 …)를 정하는 값이라 스포너에 그대로 넘긴다.
    // 클릭할 때마다 불리므로 목록을 만들지 않고 세기만 한다.
    private int UnlockedCount()
    {
        int count = 0;
        foreach (var kv in _byRegion)
            if (IsUnlocked(kv.Key)) count++;
        return count;
    }

    /// <summary>
    /// 전 지역이 해금된 뒤 지난 라운드 수(=DayCount 차이). 아직 다 안 열렸으면 0.
    /// 지역 해금 배율이 표 마지막 칸에서 멈춘 뒤에도 보스가 계속 세지게 하는 데 쓴다
    /// (EnemyBase.BossFullUnlockHpBonus). 매니저가 없는 씬에서는 0이라 호출부가 보너스 없이 폴백한다.
    /// </summary>
    public static int RoundsSinceFullUnlock => instance != null ? instance.RoundsSinceFullUnlockInternal() : 0;

    private int RoundsSinceFullUnlockInternal()
    {
        // 스포너가 아직 안 모였으면 "전부 해금"을 0개 중 0개로 착각해 참이 되어버린다 — 그 전엔 0을 돌려준다.
        if (_byRegion.Count == 0) return 0;
        if (UnlockedCount() < _byRegion.Count) return 0;

        return Mathf.Max(0, CurrentDay - FullUnlockDay());
    }

    // 전 지역 해금이 끝난 일차.
    //
    // 예전에는 "전부 해금된 뒤 처음 조회되는 시점의 DayCount"를 런타임 필드에 걸어 잠갔다.
    // 그런데 그 필드가 세이브에 안 들어가서, 로드할 때마다 기준이 그날로 리셋됐다 —
    // 200일차 세이브를 열면 그동안 쌓인 보스 보너스가 통째로 0이 되어버렸다.
    //
    // 지금은 이미 저장되고 있는 값에서 유도한다. 지역별 해금일이 RegionSave.unlockDay로 저장·복원되므로
    // (SaveCapture가 GetUnlockDay로 담고 RestoreOffset이 되돌린다),
    // 마지막으로 열린 지역의 해금일이 곧 전 지역 해금일이다. 기존 세이브도 그대로 구제된다.
    //
    // 매번 다시 계산한다 — 지역 수만큼(6칸) 딕셔너리를 훑는 게 전부고,
    // 캐싱해두면 로드로 오프셋이 복원되기 전 값이 굳어 지금 고치는 버그를 그대로 되풀이한다.
    private int FullUnlockDay()
    {
        int last = 1;   // 처음부터 열려 있는 지역은 해금일 1일차
        foreach (var kv in _byRegion)
        {
            // 잠긴 지역은 0을 돌려주므로 last를 밀지 않는다.
            // 오프셋이 아직 안 잡힌 지역은 GetUnlockDay 안의 GetOffset이 지금 기준으로 정해 넣는다(그 값도 세이브를 탄다).
            int unlockDay = GetUnlockDay(kv.Key);
            if (unlockDay > last) last = unlockDay;
        }
        return last;
    }

    public void SpawnWave(int round) //해당라운드 전체소환 (round는 GameManager.DayCount와 항상 같음 — 지역별 진행도는 LocalStage로 따로 계산)
    {
        var unlocked = UnlockedRegions();
        // 웨이브 조합(어떤 몹이 나올지)은 지역별 LocalStage, 마릿수 배율은 글로벌 DayCount 기준으로 유지한다.
        foreach (int region in unlocked)
            _byRegion[region].SpawnWave(region, SpawnStage(region), unlocked.Count, CurrentDay, PathSeed(region));
    }


    public void SpawnRegion(int region,int round) //해당 지역 라운드 소환(테스트 용)
    {
        if (!IsUnlocked(region)) return;
        if (_byRegion.TryGetValue(region, out WaveSpawner s))
            s.SpawnWave(region, SpawnStage(region), UnlockedCount(), CurrentDay, PathSeed(region));
    }
    private void OnRegionClear() //몹 다잡았을때
    {
        foreach (var kv in _byRegion)
            if (IsUnlocked(kv.Key) && kv.Value.Enemycount > 0)
                return; // 아직 남은 지역이 있음

        AllRegionsClear?.Invoke();
    }
    
    // 길들에서 포탈을 하나씩 만들어 세운다. ShowPortal(낮에 켤 때)과 RestorePortal(로드할 때) 둘 다 이걸 쓴다.
    private void SpawnPortalsFor(int region, IReadOnlyList<IReadOnlyList<Vector3>> paths)
    {
        // 포탈을 담을 목록을 길 개수만큼 미리 만든다
        var portals = new List<GameObject>(paths.Count);
        // 포탈을 땅 위로 살짝 띄우는 높이값
        Vector3 lift = Vector3.up * yOffset;
        // 길 하나마다 그 길의 시작점 위에 포탈을 하나 꺼내 세운다 (풀에서 재사용)
        foreach (var path in paths)
        {
            portals.Add(PoolManager.Instance.Spawn(spawnPoint, path[0] + lift, Quaternion.identity));
        }
        // 이 지역의 포탈 목록으로 기록해둔다 (나중에 지우거나 찾을 때 씀)
        spawnPoints[region] = portals;
    }

    // 이 지역이 전체 날짜보다 며칠 늦게 시작했는지(오프셋)를 읽는다. 세이브할 때 부른다.
    public int GetOffset(int region)
    {
        // 이미 정해진 오프셋이 있으면 그 값을 그대로 돌려준다
        if (_unlockOffset.TryGetValue(region, out int off)) return off;
        // 아직 안 열린 지역이면 오프셋이 필요 없으니 0을 돌려준다(굳히지도 않는다 — 열릴 때 잡아야 한다)
        if (!IsUnlocked(region)) return 0;

        // 열려있는데 값이 없으면, 지금 날짜 기준으로 새로 정해서 저장해둔다
        return EnsureOffset(region);
    }

    /// <summary>
    /// 이 지역이 해금된 날(1-base). 0은 "해금일이 없다" — 아직 잠긴 지역이다.
    /// 세이브에 담을 값이며, 이 값이 있으면 로드가 오프셋을 추측할 필요가 없어진다
    /// (오프셋 0이 "1일차 해금"인지 "아직 안 열림"인지 구분이 안 되는 게 아래 RestoreOffset의 골칫거리였다).
    /// </summary>
    public int GetUnlockDay(int region)
    {
        if (!IsUnlocked(region)) return 0;
        return GetOffset(region) + 1;   // 오프셋이 아직 없으면 GetOffset이 지금 기준으로 정해 넣는다
    }

    // 저장해뒀던 오프셋 숫자를 그대로 다시 집어넣는다. 로드할 때 부른다.
    //
    // unlockDay: 세이브에 기록된 해금일(1-base). 0이면 그 칸이 없는 옛 세이브라는 뜻이고,
    // 그때만 아래 offset 추측 규칙으로 넘어간다. 값이 있으면 그게 곧 정답이라 추측할 게 없다.
    public void RestoreOffset(int region, int offset, int unlockDay = 0)
    {
        // 해금일이 저장된 세이브 — 잠김/1일차 해금이 숫자로 구분되므로 그대로 쓴다.
        if (unlockDay > 0)
        {
            _unlockOffset[region] = unlockDay - 1;
            return;
        }
        if (offset <= 0 && !IsUnlocked(region)) return;
        // 계산 없이 저장된 값 그대로 넣는다
        _unlockOffset[region] = offset;
    }

    // 지금 켜져 있는 포탈들의 칸 좌표를 읽어온다. 세이브할 때 부른다.
    public bool TryGetActiveSpawnCoords(int region, out Vector2Int[] coords)
    {
        // 일단 빈 값으로 시작해둔다
        coords = Array.Empty<Vector2Int>();
        // 이 지역 담당 스포너가 없으면 읽을 게 없다
        if (!_byRegion.TryGetValue(region, out var spawner) || spawner == null) return false;

        // 지금 켜진 포탈들의 번호 목록
        IReadOnlyList<int> activeSpawns = spawner.ActiveSpawns;
        // 번호마다 실제 칸 좌표를 찾아서 담는다
        coords = new Vector2Int[activeSpawns.Count];
        for (int i = 0; i < activeSpawns.Count; i++)
        {
            coords[i] = spawner.SpawnCoord(activeSpawns[i]);
        }
        return true;
    }

    // 저장된 좌표대로 포탈을 다시 세운다. 로드할 때 부른다.
    public void RestorePortal(int region, Vector2Int[] savedCoords)
    {
        // 포탈을 세울 기준 오브젝트가 없으면 아무것도 못 하니 끝낸다
        if (spawnPoint == null) return;
        // 이 지역 담당 스포너를 못 찾으면 끝낸다
        if (!_byRegion.TryGetValue(region, out var spawner) || spawner == null) return;

        // 혹시 이미 떠 있는 포탈이 있으면(중복 방지) 먼저 다 지운다
        if (spawnPoints.TryGetValue(region, out var existing) && existing != null)
        {
            // 떠 있던 포탈들을 하나씩 반납한다
            foreach (var portal in existing)
                if (portal != null) PoolManager.Instance.Despawn(portal);
            // 이 지역의 포탈 기록도 지운다
            spawnPoints.Remove(region);
        }

        // 저장된 좌표대로 길을 켠다
        spawner.ActivateSpawnsAt(savedCoords);

        // 켜진 길들 위에 포탈을 세운다
        SpawnPortalsFor(region, spawner.ActivePaths);
    }
     [SerializeField] private GameObject guardPanel;
    [SerializeField] private Animator directingUi;
    // 좌/우 화살표 이동은 Animator가 아니라 WarningArrowSlide가 코드로 굴린다(Arrows에 붙인다) —
    // Directing 루트 클립은 Warning Builder가 구울 때마다 ClearCurves로 지워서 커브를 못 남긴다.
    [SerializeField] private WarningArrowSlide directingArrowSlide;
    [SerializeField] private string directingStateName = "Directing";
    [SerializeField] private float directingTimeout = 5f;
    public bool isDirecting = false;
    public async UniTask BossOpeningDirecting(CancellationToken token, float bossDelay = 0f)
    {
        if (bossDelay > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(bossDelay), cancellationToken: token);

        float savedTimeScale = Time.timeScale;
        AnimatorUpdateMode savedUpdateMode = directingUi != null ? directingUi.updateMode : AnimatorUpdateMode.Normal;
        try
        {
            guardPanel?.SetActive(true);
            Time.timeScale = 0f;
            isDirecting = true;
            if (directingUi != null)
            {
                // 기본 Normal 모드는 timeScale을 따라가므로 얼린 동안 재생되게 언스케일드로 전환.
                directingUi.updateMode = AnimatorUpdateMode.UnscaledTime;
                directingUi.Rebind();   // 재사용되는 오브젝트라 지난 연출 끝난 지점이 아니라 처음부터 다시 재생
                directingUi.Update(0f);

                // 화살표는 메인 연출과 같은 프레임에 출발시킨다. 끄는 쪽은 Directing 루트 CanvasGroup
                // 알파 커브가 자식 전체에 걸리므로 저절로 WARNING과 같이 사라진다.
                // 인스펙터에 안 꽂혀 있으면 조용히 건너뛴다(화살표 없는 연출도 그대로 돌아야 한다).
                if (directingArrowSlide != null) directingArrowSlide.Play();

                directingUi.SetTrigger(directingStateName);

                // 일반 Play가 아니라 PlayImportant — System 그룹으로 빼고, 남아 울리던 효과음을 끊고,
                // 재생 동안 BGM을 눌러서 다른 소리에 묻히지 않게 한다.
                EnemySoundManager.PlayImportant("WarningHit");
                EnemySoundManager.PlayImportant("Warning!");
                await WaitForDirectingAnim(directingUi, directingStateName, directingTimeout, token);
            }
        }
        catch (OperationCanceledException)
        {
            // 씬 전환/취소 — 정상 취소이므로 무시하고 finally에서 원복만 한다.
        }
        finally
        {
            if (directingUi != null) directingUi.updateMode = savedUpdateMode;
            // 취소로 중간에 끊기면 화살표가 계속 미끄러진다(unscaled라 timeScale 복구와 무관하게 돈다).
            // 위치는 안 건드린다 — 되돌리면 아직 안 사라진 화살표가 순간이동한다. 다음 연출은 Play()가 맞춘다.
            if (directingArrowSlide != null) directingArrowSlide.Stop();
            Time.timeScale = savedTimeScale;
            guardPanel?.SetActive(false);
            isDirecting = false;
            EnemySoundManager.PlayBgm("BossBGM");
        }
    }

    // EnemyBase.WaitForAttackAnim과 같은 패턴이지만, 이 연출은 Time.timeScale=0인 동안 돌아가므로
    // 시간 소스를 전부 언스케일드로 맞춘다 — 스케일드로 두면(Time.deltaTime, UniTask.Delay 기본값)
    // 얼려있는 동안 delta가 계속 0이라 영원히 안 끝난다.
    private static async UniTask WaitForDirectingAnim(Animator anim, string stateName, float timeout, CancellationToken token)
    {
        const int layer = 0;
        float elapsed = 0f;
        while (anim != null && !anim.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"BossOpeningDirecting: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", anim);
                return;
            }
            await UniTask.Yield(token);
        }
        if (anim == null) return;

        var info = anim.GetCurrentAnimatorStateInfo(layer);
        float wait = info.length / Mathf.Max(0.01f, anim.speed);
        await UniTask.Delay(TimeSpan.FromSeconds(wait), ignoreTimeScale: true, cancellationToken: token);
    }
}
