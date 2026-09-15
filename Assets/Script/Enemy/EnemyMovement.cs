using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 스폰→본진 경로 추종 이동만 담당한다. EnemyBase가 소유하고 매 프레임 Tick으로 굴린다.
/// 위치·경로·보드 등록은 여기서 관리하고, 본진 도달은 ArrivedAtCore 이벤트로 알린다
/// (도달 후 처리·디스폰은 EnemyBase의 몫 — 라이프사이클은 본체가 쥔다).
///
/// 공격/사망이 이동을 멈추고 재개하는 건 Pause/Resume/Stop으로,
/// 대시·소환처럼 스킬이 직접 위치를 옮기는 동안은 Suspended로 제어한다.
/// </summary>
public class EnemyMovement
{
    private const string MovingBool = "IsMoving"; // 지상 이동
    private const string SwimBool = "IsSwim";     // 헤엄 이동(수영 몹만 — 없는 컨트롤러는 이 이름을 쓰지 않는다)

    private readonly GameObject _go;
    private readonly Transform _tf;
    private readonly Animator _animator;
    private readonly float _arriveSqr; // 웨이포인트 도달 판정 거리의 제곱

    private readonly List<Vector3> _path = new();
    private int _pathIndex;
    private Vector2Int _lastCell = new(int.MinValue, int.MinValue); // 직전 칸 — 바뀐 프레임에만 보드 갱신
    private bool _moving;
    private bool _animMoving; // Animator에 보고한 마지막 이동 상태 — 바뀐 프레임에만 SetBool 호출
    private bool _animSwim;   // 그때 보고한 대상이 IsSwim이었는지(false면 IsMoving)

    public MapBoard Board { get; private set; }
    public bool HasPath => _path.Count > 0;
    public IReadOnlyList<Vector3> Path => _path;          // 대시 등 경로 기준 스킬이 참조
    public int PathIndex => _pathIndex;

    /// <summary>대시/넉백 등 스킬이 직접 위치를 옮기는 동안 true — 일반 경로 이동이 위치를 덮어쓰지 않게 멈춘다.</summary>
    public bool Suspended { get; set; }

    /// <summary>공중(지형 무시). true면 중간 경로점을 건너뛰고 본진(마지막 웨이포인트)으로 직선 이동한다.</summary>
    public bool Flying { get; set; }

    /// <summary>수영(헤엄 칸 통행). 물 칸(PassType.Swim) 위를 지나갈 수 있다는 뜻이며,
    /// 경로 자체는 저작 경로(EnemyRouteSet) → 일반 레인 순으로 받은 것을 그대로 따른다.
    /// 받은 경로가 아예 없을 때만 물 칸까지 열어 스스로 길을 찾는다(EnterMap 참조).
    /// Flying이 켜져 있으면 그쪽이 더 넓은 통행권이라 이 값은 무시된다.</summary>
    public bool Swimming { get; set; }

    /// <summary>받은 웨이포인트가 사람이 그린 경로인가(EnemyRouteSet). true면 공중·수영 자동 재탐색을
    /// 건너뛰고 그대로 따른다 — 그린 의도가 자동 계산에 지면 저작이 무의미하다.
    /// 저작이 없어 폴백으로 들어온 경우엔 false라 지금까지의 동작이 그대로 유지된다.</summary>
    public bool Authored { get; set; }

    /// <summary>물속 이동속도 배율(EnemyBase.swimSpeedMultiplier와 같은 값). 경로를 "칸 수 최단"이 아니라
    /// "가장 빨리 도착하는 길"로 잡는 데 쓴다 — 물이 빠르면 조금 돌더라도 물길을 택한다. 1이면 기존과 같다.</summary>
    public float SwimSpeedMultiplier { get; set; } = 1f;

    /// <summary>물에 들어가고 나올 때 1회당 손해로 치는 거리(타일 수). 잠수/상승 모션 동안 멈추는 시간을
    /// 길찾기 비용에 반영한다 — 안 넣으면 한 칸짜리 웅덩이를 들렀다 나오며 오히려 늦게 도착한다.</summary>
    public float SwimTransitionPenaltyTiles { get; set; }

    /// <summary>물 구간 안에 있는 동안 true — 이동 상태를 IsMoving 대신 IsSwim으로 보고한다.
    /// EnemySwim이 매 프레임 갈아끼운다. 통행권(Swimming)과 달리 "지금 물에 들어가 있는가"라 별개 값이다.</summary>
    public bool SwimAnim { get; set; }

    /// <summary>본진 도달 신호 — 구독자(EnemyBase)가 OnArrivedAtCore + 디스폰을 처리한다.</summary>    
    public event System.Action ArrivedAtCore;

    public EnemyMovement(GameObject go, Animator animator, float arriveSqr)
    {
        _go = go;
        _tf = go.transform;
        _animator = animator;
        _arriveSqr = arriveSqr;
    }

    /// <summary>대시 등으로 앞선 지점에 착지한 뒤, 정상 이동이 그 지점부터 이어지게 목표 인덱스를 맞춘다.</summary>
    public void ResumeFrom(int index) => _pathIndex = Mathf.Clamp(index, 0, _path.Count - 1);

    /// <summary>대시가 도중에 멈춘 경우 등, 현재 위치에서 가장 가까운 경로 지점부터 정상 이동을 이어간다.</summary>
    public void ResumeFromNearest()
    {
        if (_path.Count > 0) _pathIndex = NearestPathIndex(_tf.position);
    }

    /// <summary>현재 위치에서 경로를 따라 dist(월드거리)만큼 앞선 지점. 경로 끝이면 마지막 점으로 클램프. landIndex=착지 세그먼트.</summary>
    public Vector3 PointAhead(float dist, out int landIndex)
    {
        if (_path.Count == 0) { landIndex = -1; return _tf.position; }

        Vector3 pos = _tf.position;
        for (int i = _pathIndex; i < _path.Count; i++)
        {
            Vector3 seg = _path[i] - pos;
            float len = seg.magnitude;
            if (len >= dist) { landIndex = i; return pos + seg.normalized * dist; }
            dist -= len;
            pos = _path[i];
        }
        landIndex = _path.Count - 1; // 본진 도달 — 오버슈트 방지
        return _path[^1];
    }

    // ---- 이동 제어 (공격/사망/비활성에서 EnemyBase가 호출) ----
    public void Pause() => _moving = false;                 // 공격 등으로 일시 정지
    public void Resume()
    {
        _moving = true;
        _animMoving = false;
    }
              // 재개
    public void Stop() { _moving = false; Suspended = false; } // 사망/비활성 — 완전 정지

    public void LeaveBoard()
    { 
        if (Board != null) Board.RemoveEnemy(_go); 
    } // 현재 칸에서 빠짐

    // 스폰→본진 경로를 세팅하고 이동을 시작한다.
    public void EnterMap(MapBoard board, IReadOnlyList<Vector3> waypoints, bool snapToStart, float moveSpeed, string enemyKey)
    {
        Board = board;
        _path.Clear();
        _pathIndex = 0;
        _moving = false;

        if (board == null)
        {
            Debug.LogWarning($"[{_go.name}] MapBoard가 주입되지 않아 이동할 수 없습니다.", _go);
            return;
        }

        // 웨이포인트는 타일 윗면 기준(offset 0)으로 받는다.
        IReadOnlyList<Vector3> src = waypoints ?? board.GetWaypoints(0f);

        // 사람이 그린 경로(EnemyRouteSet)를 받았으면 다시 찾지 않는다 — 아래 두 분기가 그대로 덮어써 버린다.
        // 저작이 없어 폴백으로 온 경우엔 Authored가 false라 지금까지의 재탐색이 그대로 돈다.
        if (!Authored)
        {
            if (Flying && src.Count > 0)
            {
                Vector3 startW = snapToStart ? src[0] : _tf.position;
                var flying = FlyingPathfinder.BuildWaypoints(
                    board, startW, src[src.Count - 1], EnemyRoutePath.FlightHeight);
                if (flying.Count > 0) src = flying;
            }
            // 수영 적은 저작 경로(EnemyRouteSet)가 있으면 그걸 타고, 없으면 지상 적과 같은 일반 레인 경로를
            // 그대로 따른다 — 자동 재탐색은 설계된 레인을 무시하고 맵을 가로지르는 길을 내놓기 때문이다.
            // 그래서 레인이 아예 없을 때(= 물이 걷는 길을 완전히 막은 맵)만 최후 수단으로 헤엄 길찾기를 돈다.
            // Flying은 통행권이 더 넓으니 그쪽이 우선.
            else if (Swimming && src.Count == 0)
            {
                // 여기까지 왔다는 건 src가 비었다는 뜻이라 시작·목표를 모두 현재 위치로 잡는다.
                // 도착 판정은 좌표가 아니라 Tile.IsCore라 목표 좌표가 근사여도 본진까지 이어진다(SwimPathfinder 주석 참조).
                Vector3 startW = _tf.position;
                Vector3 goalW = _tf.position;
                var swimming = SwimPathfinder.BuildWaypoints(
                    board, startW, goalW, SwimSpeedMultiplier, SwimTransitionPenaltyTiles);
                if (swimming.Count > 0) src = swimming;
            }
        }

        foreach (Vector3 p in src) _path.Add(p);

        if (_path.Count == 0)
        {
            Debug.LogWarning($"[{_go.name}] 스폰→본진 경로가 없습니다. (스폰/본진 배치·통행 지형 확인)", _go);
            return;
        }

        if (moveSpeed <= 0f)
            Debug.LogWarning($"[{_go.name}] MoveSpeed={moveSpeed} — 제자리에 멈춥니다. EnemyTable '{enemyKey}' 행 확인.", _go);

        _moving = true;
        if (snapToStart)
            _tf.position = _path[0]; // 스폰 지점에서 시작
        else
            _pathIndex = NearestPathIndex(_tf.position); // 현재 위치 유지, 가까운 지점부터 이어감

        _lastCell = board.WorldToCell(_tf.position);
        board.MoveEnemy(_go, _tf.position); // 현재 칸 등록
    }

    // 경로 중 현재 위치에서 가장 가까운 웨이포인트 인덱스(소환·중간 투입 시 시작점).
    private int NearestPathIndex(Vector3 pos)
    {
        int best = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _path.Count; i++)
        {
            float d = (_path[i] - pos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = i; }
        }
        return best;
    }

    /// <summary>매 프레임 EnemyBase.Update에서 호출. active=이동 허용(사망 아님), moveSpeed=현재 이동속도.</summary>
    public void Tick(bool active, float moveSpeed)
    {
        if (!_moving || !active || Board == null || Suspended) { SetMoving(false); return; }

        // 근접 영웅에게 저지당하면 그 자리에서 정지(타일 저지 시스템). 풀리면 다시 전진.
        bool advancing = !Board.IsBlocked(_go)||_tf.GetComponent<EnemyBase>().IsUnJudged||_tf.GetComponent<EnemyBase>().IsFly;
        if (advancing)
        {
            Vector3 target = _path[_pathIndex];
            _tf.position = Vector3.MoveTowards(_tf.position, target, moveSpeed * Time.deltaTime);
            FaceToward(target);
        }
        SetMoving(advancing); // 저지되면 Idle, 풀리면 Move — 상태 바뀐 프레임에만 반영

        // 현재 칸이 바뀐 프레임에만 보드에 보고 → 저지·커버·타겟팅이 이걸로 갱신된다.
        Vector2Int now = Board.WorldToCell(_tf.position);
        if (now != _lastCell)
        {
            _lastCell = now;
            Board.MoveEnemy(_go, _tf.position);
        }

        // 이번 웨이포인트 도달 → 다음 목표로.
        if ((_tf.position - _path[_pathIndex]).sqrMagnitude > _arriveSqr) return;
        _pathIndex++;
        if (_pathIndex >= _path.Count)
        {
            _moving = false;            // 원본 ArriveAtCore 순서: 먼저 멈추고 → 도달 통지(중복 발화 방지)
            ArrivedAtCore?.Invoke();
        }
    }

    // 이동 상태를 Animator에 반영 — 바뀐 프레임에만 SetBool을 호출해 낭비/리셋 방지.
    // 물 구간(SwimAnim)에서는 같은 이동 상태를 IsSwim으로 보고한다.
    private void SetMoving(bool moving)
    {
        bool swim = SwimAnim;
        if (_animMoving == moving && _animSwim == swim) return;

        if (_animator != null)
        {
            // 보고 대상이 바뀌는 프레임엔 이전 파라미터를 먼저 내린다 — 안 내리면 IsMoving과 IsSwim이
            // 같이 true로 남아 Animator가 어느 전이를 타는지 전이 순서에 따라 갈린다.
            if (_animSwim != swim) _animator.SetBool(_animSwim ? SwimBool : MovingBool, false);
            _animator.SetBool(swim ? SwimBool : MovingBool, moving);
        }

        _animMoving = moving;
        _animSwim = swim;
    }

    private void FaceToward(Vector3 target)
    {
        Vector3 flat = target - _tf.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
            _tf.rotation = Quaternion.Slerp(
                _tf.rotation, Quaternion.LookRotation(flat), 12f * Time.deltaTime);
    }
}
