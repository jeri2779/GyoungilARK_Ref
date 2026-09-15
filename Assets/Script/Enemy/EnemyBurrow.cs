using UnityEngine;

/// <summary>
/// 잠행(Burrow) 연출만 담당한다. EnemyBase가 소유하고, 저지/사망 여부를 surface로 넘겨 매 프레임 Tick으로 굴린다.
/// Setup을 안 했거나(=잠행 몹이 아님) Animator가 없으면 Tick/Reset은 조용히 no-op.
///
/// 흐름 — Animator의 Burrowed(bool) 하나로 굴린다:
///   저지 없음 → Burrowed=true → [BurrowIn] 파고들기 → 끝 프레임 이벤트 → 렌더러 off (이후 Move/Idle이 돌아도 안 보임)
///   저지 발생 → 렌더러 즉시 on + Burrowed=false → [Emerge] 솟아오르기 → 끝 프레임 이벤트 → IsSurfaced=true → 공격 허용
///   영웅 사망/제거 → 저지 해제 → 다시 파고든다
///
/// "숨어 있는 동안"을 클립 포즈가 아니라 렌더러 on/off로 처리하는 이유:
/// 땅 밑에서 이동/대기하는 전용 클립을 따로 만들지 않아도 기존 Move/Idle을 그대로 쓸 수 있다.
/// (클립 포즈로 깊이를 내면 Move 스테이트로 전이하는 순간 지상 포즈라 몸이 튀어 올라온다)
/// </summary>
public class EnemyBurrow
{
    private const string BurrowedBool = "Burrowed";
    // 지면 마커는 잠행 몹이면 늘 같은 것을 쓴다 — 적 프리팹마다 꽂아 두면 새 잠행 몹을 만들 때마다
    // 빠뜨릴 수 있어(비면 마커 없이 조용히 숨는다) EnemySwim의 Bubble과 같은 방식으로 여기서 직접 로드한다.
    private const string MarkerPath = "EnemyEffectPrefab/BurrowEffect";

    private Animator _animator;
    private Renderer[] _renderers;
    private GameObject _markerPrefab;
    private float _timeout;          // 애니 이벤트 누락 대비 강제 진행 시간(초)

    private GameObject _marker;
    private bool _hidden;            // Animator에 보고한 마지막 상태(EnemyBase._stunAnimActive와 같은 패턴)
    private bool _buried;            // 파고들기 애니가 끝나 렌더러를 껐는지
    private bool _surfaced;          // 솟아오르기 애니가 끝났는지 — 공격/피격 허용 기준
    private float _pending;          // 전이 대기 시간(파고들기/솟아오르기 중 하나만 대기 중이므로 하나로 충분)
    private bool _hasParam, _paramChecked;

    public bool IsSetup => _animator != null;

    /// <summary>완전히 솟아올랐는지. 잠행 몹이 아니면 항상 true라 기존 적의 공격 로직은 영향받지 않는다.</summary>
    public bool IsSurfaced => !IsSetup || _surfaced;

    /// <summary>
    /// 파고들기/솟아오르기 모션이 재생 중인지 — 이 동안엔 제자리에 멈춘다(걸으면서 파고드는 게 안 보이게).
    /// 잠행 몹이 아니면 항상 false라 기존 적의 이동은 영향받지 않는다.
    /// 이벤트가 누락돼도 WatchPendingTransition의 타임아웃이 풀어주므로 영구 정지는 없다.
    /// </summary>
    public bool IsTransitioning => IsSetup && (_hidden ? !_buried : !_surfaced);

    // 잠행 몹일 때 EnemyBase가 (Attribute 결정 뒤) 1회 호출.
    public void Setup(GameObject root, Animator animator, float timeout)
    {
        if (root == null || animator == null) return;
        _animator = animator;
        _renderers = root.GetComponentsInChildren<Renderer>(true);
        _markerPrefab = Resources.Load<GameObject>(MarkerPath);
        if (_markerPrefab == null)
            Debug.LogWarning($"[{root.name}] Resources/{MarkerPath}를 못 찾음 — 마커 없이 숨는다.", root);
        _timeout = timeout > 0f ? timeout : 3f;
    }

    /// <summary>surface=true(저지/사망)면 솟아오르고, 아니면 파고든다. markerPos엔 보통 적의 현재 위치를 넘긴다.
    /// dead면 지면 마커만 거둔다 — 사망 애니가 도는 동안에도 Update는 계속 돌기 때문에,
    /// 여기서 막지 않으면 죽은 몸 밑에 흙먼지가 계속 깔려 있다(EnemySwim의 물거품과 같은 문제).</summary>
    public void Tick(bool surface, Vector3 markerPos, bool dead = false)
    {
        if (!IsSetup) return;

        bool hide = !surface;
        if (hide != _hidden)
        {
            _hidden = hide;
            _pending = 0f;
            if (hide)
            {
                _surfaced = false;          // 파고들기 시작 → 즉시 공격·피격 불가(안전한 방향으로 먼저 닫는다)
                if(!dead)
                EnemySoundManager.Play("Burrow", at: markerPos);
            }
            else
            {
                _buried = false;
                SetRenderers(true);         // 솟아오르는 모습이 보여야 하므로 애니 시작 전에 켠다
                if(!dead)
                EnemySoundManager.Play("BurrowUp", at: markerPos);
            }
            SetBurrowedBool(hide);
        }

        // 사망 — 흙먼지를 끌고 가지 않는다(EnemySwim.Tick의 suppress와 같은 취지).
        // 이 검사는 반드시 위 전이 블록 <b>뒤</b>에 와야 한다 — 앞에 두면 SetRenderers(true)가 안 돌아
        // 땅속에서 죽은 적이 보이지 않는 채로 사망 애니를 재생한다(EnemySwim은 렌더러를 안 만지므로 맨 위에 둔 것이다).
        // WatchPendingTransition도 건너뛴다 — 죽으면 솟아오르기 이벤트가 안 오는 게 정상이라 타임아웃 경고는 거짓 경보다.
        if (dead) { DespawnMarker(); return; }

        WatchPendingTransition();
        UpdateMarker(markerPos);
    }

    // 지면 마커 반납은 Tick(사망)·Reset(디스폰)·UpdateMarker(솟아오름 완료) 세 곳에서 쓰므로 한 군데로 모아 둔다.
    private void DespawnMarker()
    {
        if (_marker != null) { PoolManager.Instance.Despawn(_marker); _marker = null; }
    }

    /// <summary>파고들기 클립 마지막 프레임의 Animation Event → EnemyBase.AnimEvent_Burrowed가 호출.</summary>
    public void NotifyBurrowed()
    {
        if (!IsSetup || !_hidden || _buried) return;
        _buried = true;
        SetRenderers(false);
    }

    /// <summary>솟아오르기 클립 마지막 프레임의 Animation Event → EnemyBase.AnimEvent_Surfaced가 호출.</summary>
    public void NotifySurfaced()
    {
        if (!IsSetup || _hidden) return;
        _surfaced = true;
        
    }

    /// <summary>풀 재사용/디스폰 — 마커를 반납하고 숨은 상태로 되돌린다. animator.Rebind() 뒤에 불려야 bool이 남는다.</summary>
    public void Reset()
    {
        // 마커는 풀에서 꺼낸 오브젝트다. 여기서 반납하지 않으면 적이 풀로 돌아갈 때 딸려가 재사용 시 되살아난다.
        DespawnMarker();
        if (!IsSetup) return;

        _hidden = true;
        _buried = false;      // 스폰 직후엔 파고들기 애니부터 재생 → 이벤트가 오면 렌더러를 끈다
        _surfaced = false;
        _pending = 0f;
        SetRenderers(true);
        SetBurrowedBool(true);
    }

    // 애니 이벤트를 안 걸었거나 애니가 멈춘 경우(예: 스턴으로 animator.speed=0) 영영 진행이 안 되는 걸 막는다.
    // EnemyBase.WaitForAttackAnim의 타임아웃+경고와 같은 취지.
    private void WatchPendingTransition()
    {
        bool waiting = _hidden ? !_buried : !_surfaced;
        if (!waiting) { _pending = 0f; return; }

        _pending += Time.deltaTime;
        if (_pending < _timeout) return;
        _pending = 0f;

        if (_hidden)
        {
            _buried = true;
            SetRenderers(false);
            Debug.LogWarning($"[{_animator.name}] 파고들기 애니 이벤트(AnimEvent_Burrowed)가 안 왔음 — 클립 마지막 프레임 확인.", _animator);
        }
        else
        {
            _surfaced = true;
            Debug.LogWarning($"[{_animator.name}] 솟아오르기 애니 이벤트(AnimEvent_Surfaced)가 안 왔음 — 클립 마지막 프레임 확인.", _animator);
        }
    }

    // 지면 마커는 "숨어 있거나 아직 다 올라오지 않은" 동안 유지 → 솟아오르는 중에도 흙먼지가 남아 자연스럽다.
    private void UpdateMarker(Vector3 pos)
    {
        bool want = _hidden || !_surfaced;
        if (want)
        {
            if (_marker == null)
            {
                if (_markerPrefab == null) return;
                _marker = PoolManager.Instance.Spawn(_markerPrefab, pos, Quaternion.identity);
            }
            else
            {
                _marker.transform.position = pos; // 숨어서 이동하는 동안 따라온다
            }
            return;
        }
        DespawnMarker();
    }

    // SetActive가 아니라 Renderer.enabled를 쓴다 — 계층을 건드리지 않아 Animator/자식 스크립트가 그대로 돈다.
    private void SetRenderers(bool on)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = on;
    }

    private void SetBurrowedBool(bool value)
    {
        if (HasParam()) _animator.SetBool(BurrowedBool, value);
    }

    // Bool "Burrowed" 파라미터가 있는지 1회 검사 후 캐시(파라미터 목록은 런타임에 안 바뀜).
    private bool HasParam()
    {
        if (_paramChecked) return _hasParam;
        _paramChecked = true;
        foreach (var p in _animator.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == BurrowedBool) { _hasParam = true; break; }
        if (!_hasParam)
            Debug.LogWarning($"[{_animator.name}] Animator에 Bool '{BurrowedBool}' 파라미터가 없음 — 잠행 연출이 동작하지 않는다.", _animator);
        return _hasParam;
    }
}
