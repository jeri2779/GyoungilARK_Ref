using UnityEngine;

/// <summary>
/// 수영(Swim) 이동 연출만 담당한다. EnemyBase가 소유하고 매 프레임 Tick으로 굴린다.
/// Setup을 안 했거나(=수영 몹이 아님) Animator가 없으면 Tick/Reset은 조용히 no-op.
///
/// 흐름 — 칸이 바뀐 프레임에만 판정한다(EnemyMovement가 보드에 보고하는 것과 같은 기준):
///   지상 → 물칸 진입 : Pool 트리거(잠수) → 클립 끝 이벤트 → 헤엄
///   물칸 → 물칸      : 아무것도 하지 않는다(그대로 헤엄)
///   물칸 → 지상 진입 : Up 트리거(상승) → 클립 끝 이벤트 → 지상 이동
///
/// 잠수/상승 모션 중엔 IsTransitioning이 true라 EnemyBase가 이동을 멈춘다 —
/// 걸어가면서 잠수하는 게 보이지 않게(EnemyBurrow의 파고들기/솟아오르기와 같은 취지).
///
/// 헤엄 중 이동 파라미터는 IsMoving이 아니라 IsSwim으로 나간다. 두 bool을 여기서 직접 쓰지 않고
/// EnemyMovement.SwimAnim만 갈아끼우는 이유 — 이동 상태를 쓰는 곳을 하나로 두어
/// IsMoving과 IsSwim이 동시에 true로 남는 상태를 원천적으로 만들지 않는다.
///
/// Map은 공개 읽기 API(MapBoard.WorldToCell/TryGetCell, Tile.State.Pass)만 본다 — 수정하지 않는다.
/// </summary>
public class EnemySwim
{
    private const string DiveTrigger = "Pool";      // 임시 이름 — Animator 파라미터와 같아야 한다
    private const string SurfaceTrigger = "Up";     // 임시 이름
    private const string SwimBool = "IsSwim";       // 실제로 쓰는 쪽은 EnemyMovement. 여기선 존재 확인만 한다

    private enum Phase
    {
        Ground,     // 지상 이동(IsMoving)
        Diving,     // 잠수 모션 중 — 정지
        Swimming,   // 헤엄 이동(IsSwim)
        Surfacing,  // 상승 모션 중 — 정지
    }

    private Animator _animator;
    private float _timeout;             // 애니 이벤트 누락 대비 강제 진행 시간(초)
    private Phase _phase = Phase.Ground;
    private float _pending;             // 전이 대기 시간(잠수/상승 중 하나만 대기하므로 하나로 충분)
    private Vector2Int _lastCell = new(int.MinValue, int.MinValue);
    private bool _paramChecked;
    private GameObject _markerPrefab;
    private GameObject _marker;
    private Renderer[] _renderers;
    private bool IsSwiming => _phase ==Phase.Swimming;
    public bool IsSetup => _animator != null;

    /// <summary>잠수/상승 모션 재생 중인지 — 이 동안엔 제자리에 멈춘다.
    /// 수영 몹이 아니면 항상 false라 기존 적의 이동은 영향받지 않는다.
    /// 이벤트가 누락돼도 WatchPendingTransition의 타임아웃이 풀어주므로 영구 정지는 없다.</summary>
    public bool IsTransitioning => IsSetup && (_phase == Phase.Diving || _phase == Phase.Surfacing);

    /// <summary>물 구간에 들어와 있는지(잠수 시작~상승 완료). true면 EnemyMovement가 IsSwim으로 보고한다.
    /// 잠수·상승 모션 중엔 이동이 멈춰 두 bool 모두 false이므로, 이 값이 미리 켜져 있어도 문제되지 않는다.</summary>
    public bool UseSwimAnim => IsSetup && _phase != Phase.Ground;

    // 수영 몹일 때 EnemyBase가 (Attribute 결정 뒤) 1회 호출.
    public void Setup(Animator animator, float timeout,GameObject root)
    {
        if (animator == null||root == null) return;
        _animator = animator;
        _timeout = timeout > 0f ? timeout : 3f;
        _renderers = root.GetComponentsInChildren<Renderer>(true);
        _markerPrefab = Resources.Load<GameObject>("EnemyEffectPrefab/Bubble");
    }

    /// <summary>매 프레임 호출. 이동(EnemyMovement.Tick)이 끝난 뒤에 불러야 이번 프레임 위치로 칸을 판정한다.
    /// suppress(사망)면 물거품만 거둔다 — 사망 애니가 도는 동안에도 Update는 계속 돌기 때문에,
    /// 여기서 막지 않으면 밖에서 반납한 거품을 다음 프레임에 이 함수가 다시 꺼낸다.</summary>
    public void Tick(MapBoard board, Vector3 position, bool suppress)
    {
        if (!IsSetup) return;

        // Reset()을 부르지 않는 이유 — _phase를 Ground로 되돌리면 UseSwimAnim이 꺾여 사망 애니 도중에
        // IsSwim bool이 내려간다. 물에서 죽었다는 상태는 그대로 두고 연출만 거둔다
        // (_debuffEffects.Tick의 suppress와 같은 취지 — 사망이 연출을 이긴다).
        if (suppress) { DespawnMarker(); return; }

        CheckParams();

        if (board != null)
        {
            Vector2Int cell = board.WorldToCell(position);
            if (cell != _lastCell)
            {
                _lastCell = cell;
                Evaluate(IsSwimCell(board, cell));
            }
        }

        WatchPendingTransition();
        UpdateMarker(position);
    }
    // 물거품 반납은 Tick(사망)·Reset(디스폰)·UpdateMarker(상승) 세 곳에서 쓰므로 한 군데로 모아 둔다.
    private void DespawnMarker()
    {
        if (_marker != null) { PoolManager.Instance.Despawn(_marker); _marker = null; }
    }

    /// <summary>Pool(잠수) 클립 마지막 프레임의 Animation Event → EnemyBase.AnimEvent_Dived가 호출.</summary>
    public void NotifyDived()
    {
        if (_phase != Phase.Diving) return;
        _phase = Phase.Swimming;
        _pending = 0f;
    }

    /// <summary>Up(상승) 클립 마지막 프레임의 Animation Event → EnemyBase.AnimEvent_Emerged가 호출.</summary>
    public void NotifyEmerged()
    {
        if (_phase != Phase.Surfacing) return;
        _phase = Phase.Ground;
        _pending = 0f;
    }

    /// <summary>풀 재사용/디스폰 — 물거품을 반납하고 지상 상태로 되돌린다. bool은 EnemyMovement가 쥐고 있어 여기서 안 건드린다.</summary>
    public void Reset()
    {
        // 물거품은 풀에서 꺼낸 오브젝트다. 여기서 반납하지 않으면 적이 풀로 돌아갈 때 딸려가 재사용 시 되살아난다
        // (EnemyBurrow.Reset과 같은 처리 — 이 한 줄이 빠져 물에서 죽은 적의 거품이 판에 남았다).
        DespawnMarker();
        _phase = Phase.Ground;
        _pending = 0f;
        // 다음 스폰의 첫 Tick이 반드시 칸을 재판정하게 한다(물 위에 스폰되면 바로 잠수).
        _lastCell = new Vector2Int(int.MinValue, int.MinValue);
    }

    // 칸이 바뀐 프레임의 상태 판정. 잠수·상승 중엔 건드리지 않는다 —
    // 그 동안엔 이동이 멈춰 칸이 바뀔 일이 없고, 대시·넉백으로 밀려난 경우라도
    // 진행 중인 모션을 중간에 끊지 않는 편이 안전하다(다음 칸 변화에서 스스로 맞춰진다).
    private void Evaluate(bool onSwimCell)
    {
        if (_phase == Phase.Ground && onSwimCell) Begin(Phase.Diving, DiveTrigger);
        else if (_phase == Phase.Swimming && !onSwimCell) Begin(Phase.Surfacing, SurfaceTrigger);
    }
    private void UpdateMarker(Vector3 pos)
    {
        if (IsSwiming)
        {
            if (_marker == null)
            {
                if (_markerPrefab == null) return;
                _marker = PoolManager.Instance.Spawn(_markerPrefab, pos,_markerPrefab.transform.rotation);
            }
            else
            {
                _marker.transform.position = pos; // 물에서 이동하는동안
            }
            return;
        }
        DespawnMarker();
    }
    private void Begin(Phase phase, string trigger)
    {
        _phase = phase;
        _pending = 0f;
        // 눌러 둔 같은 트리거가 큐에 남아 있으면 다음 전이가 즉시 소비돼 버린다 — 먼저 지운다.
        _animator.ResetTrigger(trigger);
        _animator.SetTrigger(trigger);
    }

    // 이 칸이 헤엄으로만 지나는 칸인가. 격자 밖이면 지상으로 본다(물 위에서 멈춰 있는 것보다 안전).
    private static bool IsSwimCell(MapBoard board, Vector2Int cell)
    {
        return board.TryGetCell(cell, out Tile tile) && tile.State.Pass == PassType.Swim;
    }

    // 애니 이벤트를 안 걸었거나 애니가 멈춘 경우(예: 스턴으로 animator.speed=0) 영영 진행이 안 되는 걸 막는다.
    // EnemyBurrow.WatchPendingTransition과 같은 취지.
    private void WatchPendingTransition()
    {
        if (!IsTransitioning) { _pending = 0f; return; }

        _pending += Time.deltaTime;
        if (_pending < _timeout) return;
        _pending = 0f;

        if (_phase == Phase.Diving)
        {
            _phase = Phase.Swimming;
            Debug.LogWarning($"[{_animator.name}] 잠수 애니 이벤트(AnimEvent_Dived)가 안 왔음 — " +
                             $"'{DiveTrigger}' 클립 마지막 프레임 확인.", _animator);
        }
        else
        {
            _phase = Phase.Ground;
            Debug.LogWarning($"[{_animator.name}] 상승 애니 이벤트(AnimEvent_Emerged)가 안 왔음 — " +
                             $"'{SurfaceTrigger}' 클립 마지막 프레임 확인.", _animator);
        }
    }

    // 필요한 파라미터가 다 있는지 1회 검사(파라미터 목록은 런타임에 안 바뀐다).
    // 없으면 SetTrigger/SetBool이 조용히 무시돼 "왜 안 되지"로 한참 헤매게 되므로 미리 알려준다.
    private void CheckParams()
    {
        if (_paramChecked) return;
        _paramChecked = true;

        bool dive = false, up = false, swim = false;
        foreach (AnimatorControllerParameter p in _animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == DiveTrigger) dive = true;
            else if (p.type == AnimatorControllerParameterType.Trigger && p.name == SurfaceTrigger) up = true;
            else if (p.type == AnimatorControllerParameterType.Bool && p.name == SwimBool) swim = true;
        }

        if (!dive || !up || !swim)
            Debug.LogWarning($"[{_animator.name}] Animator에 수영 파라미터가 빠졌다 — " +
                             $"Trigger '{DiveTrigger}'={dive}, Trigger '{SurfaceTrigger}'={up}, Bool '{SwimBool}'={swim}", _animator);
    }
}
