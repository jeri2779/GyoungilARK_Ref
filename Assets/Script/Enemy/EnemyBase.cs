using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

public abstract class EnemyBase : MonoBehaviour,IDamageAble,IUnit,IStunAble,IDebuffCarrier
{
    [SerializeField] protected string enemyKey;
    [SerializeField] protected List<SkillDataSO> skills = new();
    [Tooltip("은신(Cloaking) 공용 설정. IsCloaking일 때만 사용 — 걸을 땐 은신 재질, 저지 시 원래 재질.")]
    [SerializeField] private CloakSettingsSO cloakSettings;
    [Tooltip("기본 공격 애니 클립의 원래 길이(초). 공속이 빨라져 공격 간격(1/AS)이 이 값보다 짧아지면 애니를 그만큼 배속한다. 0이면 배속하지 않음.")]
    [SerializeField] private float attackClipLength = 0f;
    /// <summary>공격 한 번이 실제로 재생하는 애니 길이(초). 배속 보정의 기준값.
    /// 2타 이상으로 나누어 때리는 적(GrimReaper 등)은 이걸 재정의해 모든 클립 길이의 합을 돌려준다 —
    /// 그래야 공격 한 번이 통째로 공격 간격 안에 들어온다. 0이면 배속하지 않는다는 뜻이므로 그대로 흘려보낼 것.</summary>
    protected virtual float AttackClipLength => attackClipLength;
    [Tooltip("파고들기/솟아오르기 애니 이벤트가 안 왔을 때 강제로 다음 상태로 넘기는 시간(초). 클립 길이보다 넉넉하게.")]
    [SerializeField] private float burrowTimeout = 3f;
    [Tooltip("잠수(Pool)/상승(Up) 애니 이벤트가 안 왔을 때 강제로 다음 상태로 넘기는 시간(초). IsSwim일 때만 사용. 클립 길이보다 넉넉하게.")]
    [SerializeField] private float swimTimeout = 3f;
    [Tooltip("물속(헤엄 중) 이동속도 배율. IsSwim일 때만 사용 — 1이면 지상과 같다.")]
    [SerializeField, Min(1f)] private float swimSpeedMultiplier = 1.5f;
    [Tooltip("물에 들어가고 나올 때 1회당 손해로 칠 거리(타일 수). 잠수/상승 모션 동안 제자리에 멈추는 시간을 길찾기 비용에 반영한다.\n" +
             "물길로 새는 최소 길이 = 2×이 값 ÷ (1 − 1/속도배율). 예: 0.7 / 배율 1.5 → 물이 약 4칸 이상 이어져야 그쪽으로 돈다.\n" +
             "0이면 한 칸짜리 웅덩이도 들렀다 나온다(잠수·상승에 멈추는 시간 때문에 실제로는 더 늦게 도착할 수 있다).")]
    [SerializeField, Min(0f)] private float swimTransitionPenaltyTiles = 0.7f;
    [Tooltip("진단용. 켜면 스폰할 때마다 물길 경로 비용을 Console에 찍는다. 물길을 왜 안 타는지 볼 때만 켜고 평소엔 끈다.")]
    [SerializeField] private bool logSwimPath;
    [Tooltip("솟아오르며 영웅에게 거는 스턴 시간(초). 0이면 스턴을 걸지 않는다. Hero가 IStunAble을 구현하기 전까진 효과 없음.")]
    [SerializeField] private float burrowEmergeStun = 2f;
    [Tooltip("적 머리 위 체력바. 없는 프리팹이면 비워두면 된다(체력바 로직 전체가 no-op).")]
    public Slider healthSlider;
    [Tooltip("체력바가 현재 체력을 따라가는 속도. 클수록 빠르게 붙는다.")]
    private float sliderSpeed = 10f;
    [Tooltip("체력바 패널에 위에 보일 보스 이름")]
    public TMP_Text bossName;
    [Tooltip("보스 체력바의 현재 체력/최대 체력 텍스트(예: 4800/5000). 비우면 표시하지 않음.")]
    public TMP_Text hpText;
    [Tooltip("보스 체력바의 공격력 텍스트. 버프·디버프가 반영된 현재 값이 표시된다. 비우면 표시하지 않음.")]
    public TMP_Text attackText;
    [Tooltip("보스 체력바의 방어력 텍스트. 버프·디버프가 반영된 현재 값이 표시된다. 비우면 표시하지 않음.")]
    public TMP_Text defenseText;
    [Tooltip("디버프 아이콘이 소환될 부모. GridLayoutGroup이 달린 오브젝트를 꽂는다. 비우면 디버프 아이콘 로직 전체가 no-op.")]
    public RectTransform debuffIconRoot;
    [Tooltip("디버프 아이콘 공용 설정(아이콘 프리팹 + 종류별 스프라이트). 에셋 하나를 모든 보스가 돌려쓴다.")]
    [SerializeField] private DebuffIconSetSO debuffIconSet;
    private float[] skillTimers;
    private bool[] skillRunning;
    private CancellationTokenSource skillCts;
    private CancellationTokenSource[] skillCtsPer; // 스킬별 취소 토큰(skillCts에 연결) — 스턴 시 액티브 스킬만 개별 취소
    // 유닛 생존 동안 유효한 취소 토큰(OnDisable에서 취소). Heal처럼 Execute보다 오래 사는
    // fire-and-forget 효과는 per-skill 토큰(Execute 종료 시 dispose됨)이 아니라 이걸 써야 디스폰 시 정상 취소된다.
    public CancellationToken LifetimeToken => skillCts != null ? skillCts.Token : CancellationToken.None;
    [field: SerializeField] public float Hp { get; protected set; }   // 인스펙터 표시용(런타임 값 확인). 값은 ApplyData/재생/피격이 갱신.
    // 아래 스탯들은 StatContainer(sc)에서 파생 — 값/버프는 sc가 단일 소스. ApplyData가 sc를 채운 뒤부터 유효.
    public float MaxHp => sc[StatType.HP];
    public int Defense => Mathf.RoundToInt(sc[StatType.DEF]);
    public int AttackPower => Mathf.RoundToInt(sc[StatType.ATK]);
    public float AttackSpeed => sc[StatType.AS];
    public int Range { get; protected set; }
    public float MoveSpeed => Mathf.Max(0.1f,sc[StatType.SPD]);
    // 실제 이동에 쓰는 속도 — 물 구간에 들어가 있는 동안만 배율을 곱한다.
    // 스탯(sc)에 버프로 걸지 않는 이유: 사망·디스폰·풀 재사용·스턴·상승 타임아웃마다 해제 시점을 챙겨야 하고
    // 한 군데라도 놓치면 재사용된 적에게 속도 버프가 영구히 남는다. 매 프레임 다시 계산하면 남을 상태가 없다.
    private float CurrentMoveSpeed => _swim.UseSwimAnim ? MoveSpeed * swimSpeedMultiplier : MoveSpeed;
    public EnemyType Type { get; protected set; }        // 근거리/원거리
    public EnemyClass Class { get; protected set; }      // 일반/엘리트/보스
    public EnemyAttribute Attribute { get; protected set; } // 외부에서 볼수있는 특성
    private EnemyAttribute Dataattribute {get; set;} //원본
    public bool IsCloaking => (Dataattribute & EnemyAttribute.Cloaking) != 0; // 은신
    public bool IsFly      => (Dataattribute & EnemyAttribute.Fly)      != 0; // 공중
    public bool IsUnJudged => (Dataattribute & EnemyAttribute.UnJudged) != 0; // 저지 불가
    public bool IsBerserk => (Dataattribute & EnemyAttribute.Berserk) != 0; //폭주
    public bool IsHitsShield => (Dataattribute & EnemyAttribute.HitsShield) != 0; // 타수 보호막
    // 재생. 원본 특성이거나, 화염족이 점화된 동안(FlameIgniteRegen)이면 켜진다.
    public bool IsRegeneration => (Dataattribute & EnemyAttribute.Regeneration) != 0 || FlameIgniteRegen;
    public bool IsBurrow => (Dataattribute & EnemyAttribute.Burrow) != 0; // 잠행: 숨어 이동, 저지 시 솟아올라 공격
    public bool IsSwim => (Dataattribute & EnemyAttribute.Swim) != 0; // 수영: 헤엄 칸(PassType.Swim)을 지나갈 수 있음

    /// <summary>이 적이 따를 저작 경로의 종류. None이면 저작 경로를 쓰지 않고 맵 레인을 그대로 따른다.
    /// 우선순위는 EnemyMovement.EnterMap의 if(Flying) else if(Swimming)과 같게 둔다 —
    /// 공중이 통행권이 더 넓으므로 둘 다 가진 적은 공중으로 본다.</summary>
    public EnemyRouteKind RouteKind =>
        IsFly ? EnemyRouteKind.Air :
        IsSwim ? EnemyRouteKind.Swim : EnemyRouteKind.None;
    // 화염족: 점화를 튕겨내며 그만큼 재생을 얻고, 화염 오라(FlameAuraSkillId)를 특성으로 갖는다.
    public bool IsFlame => (Dataattribute & EnemyAttribute.Flame) != 0;
    // 화염족이 점화를 튕겨낸 뒤 재생이 유지되는 만료 시각(_shieldExpiry와 같은 방식 — 코루틴 없이 지연 만료라 풀링 안전).
    // 장부(_debuffTracker)를 못 쓰는 이유: ImmuneDebuffs가 Ignite를 막으므로 DebuffSO.Apply가
    // 장부에 기록하기 전에 되돌아간다. 그래서 "막혔다"는 알림(OnDebuffBlocked)에서 직접 시각을 새긴다.
    private float _flameRegenExpiry;
    private bool FlameIgniteRegen => Time.time < _flameRegenExpiry;

    /// <summary>화염족이 불에 닿아 강해져 있는 동안. 불 칸을 밟는 내내 1초마다 갱신되고,
    /// 벗어나면 마지막 점화가 지속됐을 시간만큼 남았다가 꺼진다.
    /// 재생과 화염 오라(FireZoneSO)가 같은 창을 본다 — 시계를 하나만 두려고 여기로 모았다.</summary>
    public bool FlameEmpowered => FlameIgniteRegen;
    public bool IsDead { get; protected set; }
    public bool IsSpawnInvincible = false;
    private StatContainer sc = new();
    public StatContainer Stats => sc;
    public Animator animator;
    protected bool _attacking; // 공격 모션 재생 중 — 이 동안 스킬 시전을 막아 애니메이터 충돌 방지
    protected EnemyMovement _move; // 경로 추종 이동 — Awake에서 생성, 아래 API는 여기로 위임
    private PoolManager _pool;
    private GameManager gameManager;
    public GameManager GameManager => gameManager;
    private ResourcesManager resourceManager;
    private bool firstEnable =false;
    [Inject]
    public void Construct(PoolManager pool,WaveSpawner waveSpawner,GameManager gameManager,BuffManager buffManager,ResourcesManager resourceManager)
    {
        _pool = pool;
        this.waveSpawner = waveSpawner;
        this.gameManager = gameManager;
        this.buffManager = buffManager;
        this.resourceManager = resourceManager;
    }
    // 영웅이 건 디버프(둔화 등)를 풀 반납 시 벗기기 위해 필요하다 — 자세한 이유는 OnDisable 주석 참조.
    // GameLifeTimeScope가 .AsSelf()로 등록하므로 위 Construct에서 해석된다.
    private BuffManager buffManager;
    protected PoolManager Pool => _pool ??= PoolManager.Instance; // 파생 클래스(Bat 등)도 재사용
    
    private WaveSpawner waveSpawner;
    // 이 적이 속한 스포너(레인). 스폰 시 스포너가 SetOwner로 주입 → 죽거나 본진 도달 시 그 스포너의 카운트만 감소.
    // 분열체는 부모의 Owner를 그대로 물려받아 같은 레인 카운트에 반영된다.
    public WaveSpawner Owner => waveSpawner;
    public void SetOwner(WaveSpawner spawner) => waveSpawner = spawner;

    private bool _dieEventSent;

    private void SendDieEvent()
    {
        if (_dieEventSent) return;
        _dieEventSent = true;
        waveSpawner?.EnemyDieEvent();
    }

    private bool AnySkillRunning()
    {
        if (skillRunning == null) return false;
        for (int i = 0; i < skillRunning.Length; i++)
            // 배경 오라(BlocksBasicAttack=false)는 계속 실행 중이어도 일반 공격을 막지 않는다.
            if (skillRunning[i] && skills[i] != null && skills[i].BlocksBasicAttack) return true;
        return false;
    }

    [Header("Movement")]
    [Tooltip("웨이포인트 도달 판정 거리의 제곱(작을수록 정확). 기본값 유지 권장.")]
    [SerializeField] private float arriveSqr = 0.0004f;

    public MapBoard Board => _move.Board;
    public bool HasPath => _move.HasPath;
    public IReadOnlyList<Vector3> Path => _move.Path;
    public int PathIndex => _move.PathIndex;
    public bool MovementSuspended { get => _move.Suspended; set => _move.Suspended = value; }
    public void ResumeFrom(int index) => _move.ResumeFrom(index);
    public void ResumeFromNearest() => _move.ResumeFromNearest();
    public Vector3 PointAhead(float dist, out int landIndex) => _move.PointAhead(dist, out landIndex);
    private float _damageBlock;   // 고정 감소 수치
    private float _shieldExpiry;  // Time.time 기준 만료 시각 — 코루틴 없이 지연 만료(풀링 안전)

    private bool _berserkOn;        // 이번 생존 동안 이미 발동했는지(중복 누적 방지)
    private Vector3 _baseScale;     // 원래 스케일 — 풀 재사용 시 여기로 복구(분열체가 줄여놓은 걸 리셋)
    public bool IsShielded => Time.time < _shieldExpiry;

    private bool _stunAnimActive;
    private bool _hasStunParam;
    private bool _stunParamChecked;
    // 스턴 만료 시각은 _debuffTracker가 들고 있다 — 따로 필드를 두면 시계가 둘이 되어 어긋난다.
    // IsStunned는 "기절 연출"용으로만 남긴다(애니 bool·별 이펙트). 행동 차단은 아래 셋으로 갈라 본다.
    public bool IsStunned => _debuffTracker.Has(DebuffType.Stun);

    // 행동별 차단 게이트. 매 프레임 폴링이라 이미 시전된 스킬은 스스로 CannotMove를 봐야 한다(대시 참조).
    public bool CannotMove => _debuffTracker.HasAny(DebuffType.Stun | DebuffType.Root);       // 기절·속박
    public bool CannotAttack => _debuffTracker.Has(DebuffType.Stun);                          // 기절만 — 속박은 평타 허용
    public bool CannotCast => _debuffTracker.HasAny(DebuffType.Stun | DebuffType.Silence);    // 기절·침묵
    private GameObject stunEffectPrefab;
    [Tooltip("머리 위 이펙트가 뜰 위치(기절 별 등). 머리 위에 빈 오브젝트를 만들어 꽂는다(유닛마다 키가 달라 원점으로는 안 맞는다). 비우면 머리 이펙트가 뜨지 않음.")]
    [SerializeField] private Transform stunEffectAnchor;
    [Tooltip("몸통 이펙트가 뜰 위치(독·화상 등). 머리 앵커와 같은 방식으로 빈 오브젝트를 꽂는다. 비우면 몸통 이펙트가 뜨지 않음. " +
             "발밑에 깔리는 이펙트(둔화 등)는 앵커가 필요 없다 — 이펙트 쪽 anchor를 Foot으로 두면 유닛 원점을 쓴다.")]
    [SerializeField] private Transform bodyEffectAnchor;
    public Transform BodyEffectAnchor => bodyEffectAnchor != null ? bodyEffectAnchor : transform;
    // 디버프별 이펙트 설정은 모든 적이 같은 것을 쓰므로 프리팹마다 꽂지 않고 Resources에서 읽는다
    // (EnemyCloak의 CloakSettings, 위의 EnemyEffectPrefab/Stun과 같은 방식).
    // 종류를 추가할 때 프리팹 8개를 다시 손대지 않아도 된다 — 에셋 하나만 고치면 전부 반영된다.
    private const string DebuffEffectSetPath = "EnemyEffectPrefab/DebuffEffectSet";
    // 화염족이 특성으로 갖는 화염 오라 스킬 ID(Resources/Skills 아래).
    // 특성이 곧 오라이므로 CSV Skills 칸에 안 적어도 LoadStats가 붙여 준다.
    // 화염족이 아닌 적에게 붙이고 싶으면 그때만 Skills 칸에 직접 적으면 된다.
    private const string FlameAuraSkillId = "FireZoneSkill";
    protected virtual void Awake()
    {
        _baseScale = transform.localScale; // 프리팹 원래 스케일 스냅샷(분열 축소 후 복구 기준)
        _bar.Setup(healthSlider, sliderSpeed); // LoadStats(→ApplyData)가 바를 채우므로 그보다 먼저
        _debuffs.Setup(debuffIconRoot, debuffIconSet); // 아이콘은 디버프가 걸릴 때 풀에서 소환된다
        _statText.Setup(hpText, attackText, defenseText);
        LoadStats();
        animator = GetComponent<Animator>();
        _move = new EnemyMovement(gameObject, animator, arriveSqr);
        // 잠행 몹은 은신 셰이더 페이드를 쓰지 않는다(연출을 EnemyBurrow가 전담) — Cloaking 비트는 피격 판정용으로만 남긴다.
        if (IsCloaking && !IsBurrow) _cloak.Setup(gameObject, cloakSettings); // Attribute 결정(LoadStats) 뒤에 호출
        if (IsBurrow) _burrow.Setup(gameObject, animator, burrowTimeout);
        if (IsSwim) _swim.Setup(animator, swimTimeout,gameObject);   // Attribute 결정(LoadStats) 뒤에 호출
        stunEffectPrefab = Resources.Load<GameObject>("EnemyEffectPrefab/Stun");
        // Resources.Load는 내부 캐시가 있어 적마다 불러도 에셋을 다시 읽지 않는다.
        var debuffEffectSet = Resources.Load<DebuffEffectSetSO>(DebuffEffectSetPath);
        _debuffEffects.Setup(debuffEffectSet, stunEffectAnchor, bodyEffectAnchor, transform, stunEffectPrefab);
    }

    protected virtual void OnEnable()
    {
        LoadStats();
        _attacking = false;
        _shieldExpiry = 0f;
        _flameRegenExpiry = 0f;         // 풀 재사용 시 이전 개체가 남긴 화염족 재생 제거
        _debuffTracker.Clear();         // 풀 재사용 시 이전 개체의 디버프 잔여 제거(스턴 포함)
        _stunAnimActive = false;        // animator.Rebind()로 bool도 초기화되므로 상태만 맞춰둔다
        _berserkOn = false;
        _dieEventSent = false;          // 풀 재사용 시 새 생존 시작 — 카운트를 다시 한 번 내릴 수 있게
        SplitGeneration = 0;
        transform.localScale = _baseScale; 
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
        EnemyRegistry.Register(this);
        EnemyArchiveData.Unlock(enemyKey);   // 등장 = 도감 해금 (멱등 — 재등장해도 최초 1회만 저장)
        _move.Resume();
        _cloak.Reset();
        _burrow.Reset();   // animator.Rebind() 뒤라 Burrowed bool이 유지된다(스폰 = 숨은 상태로 시작)
        skillCts = new CancellationTokenSource();
        _move.ArrivedAtCore += HandleArrivedAtCore;
        RunSkillLoop(skillCts.Token).Forget();
        RunAttackLoop(skillCts.Token).Forget();
        // 화염족은 스폰 시점엔 재생이 꺼져 있어도 점화되면 켜지므로 루프를 미리 돌려둔다.
        // 루프 안에서 매 프레임 IsRegeneration을 다시 보므로 꺼진 동안은 회복하지 않는다.
        if(IsRegeneration || IsFlame)Regeneration(skillCts.Token).Forget();
    }
    protected virtual void OnDisable()
    {
        // 죽은 채로 비활성화되면 여기서 카운트를 내린다. 비활성화는 동기 실행이라 풀 재사용과 겹칠 수 없고,
        // DieRoutine의 취소가 끝내 관측되지 않는 경우(씬 언로드, 도메인 리로드)까지 덮는다.
        // 본진 도달은 IsDead가 아니므로 여기 안 걸리고, 이미 보낸 경우는 SendDieEvent가 무시한다.
        if (IsDead) SendDieEvent();
        _cloak.Reset();
        _burrow.Reset();   // 지면 마커를 풀에 반납(안 하면 적에 딸려가 재사용 시 되살아난다)
        _swim.Reset();     // 지상 상태로 되돌린다(안 하면 재사용된 개체가 헤엄 중인 채로 걸어 나온다)
        _bar.Reset();
        _debuffs.Reset();  // 소환된 아이콘을 풀에 반납(안 하면 다음 스폰이 이전 개체의 디버프 아이콘을 물고 나온다)
        _debuffEffects.Reset(); // 디버프 중에 죽어도 이펙트가 남지 않게 풀에 반납
        _statText.Reset();   // 캐시를 비워, 재사용된 개체가 이전 개체의 숫자를 한 프레임 보여주지 않게
        _move.Pause();
        _move.LeaveBoard(); // 어떤 경로로 사라지든 현재 칸에서 빠진다
        _move.ArrivedAtCore -= HandleArrivedAtCore;
        sc.RemoveModifier(this);          // 자기 자신이 건 것(폭주 등)만 지운다 — Source가 this인 것만 걸린다
        // 영웅이 건 디버프는 Source가 AttackDataSO라 위 한 줄로는 안 지워지고, Stat.ResetBase도 모디파이어를 남긴다.
        // 즉 안 벗기면 둔화 걸린 채 죽은 적이 풀에서 재사용될 때 느린 상태로 되살아나고,
        // 더 나쁘게는 BuffManager가 이 인스턴스를 Target으로 계속 물고 있다가 만료 시점에
        // 그 오브젝트를 지금 쓰고 있는 다른 적의 스탯을 벗긴다. 장부와 모디파이어를 여기서 같이 끊는다.
        buffManager?.RemoveAllBuffs(this);
        _debuffTracker.Clear();           // 조회 장부도 같이 끊는다 — 안 하면 재사용된 개체가 이전 디버프를 물고 나온다
        DotRegistry.Clear(this);          // 지속 피해 장부에서도 빠진다(만료 전에 죽거나 반납된 경우)
        skillCts?.Cancel();
        skillCts?.Dispose();
        skillCts = null;
    }
    /// <summary>authored=true면 받은 웨이포인트가 사람이 그린 경로라는 뜻 —
    /// 공중·수영 자동 재탐색을 건너뛰고 그대로 따른다(그린 의도가 자동 계산에 지면 저작이 무의미하다).</summary>
    public void EnterMap(MapBoard board, IReadOnlyList<Vector3> waypoints = null, bool snapToStart = true,
        bool authored = false)
    {
        _move.Flying = IsFly; // 공중 특성이면 지형 무시(본진으로 직선). Map/길찾기는 건드리지 않음
        _move.Swimming = IsSwim; // 수영 특성이면 헤엄 칸까지 열어 경로 재탐색(공중이면 무시된다)
        _move.Authored = authored;
        // 물에서 빠른 만큼 물길을 싸게 쳐서, 칸 수 최단이 아니라 "가장 빨리 도착하는 길"로 경로를 잡는다.
        // 실제 이동에 쓰는 배율(CurrentMoveSpeed)과 같은 값을 넘겨야 경로와 실제 속도가 어긋나지 않는다.
        _move.SwimSpeedMultiplier = swimSpeedMultiplier;
        _move.SwimTransitionPenaltyTiles = swimTransitionPenaltyTiles;
        SwimPathfinder.LogPathCost = logSwimPath; // 바로 다음 줄에서 경로를 만들므로 이 값이 그대로 쓰인다
        _move.EnterMap(board, waypoints, snapToStart, MoveSpeed, enemyKey);
    }

    // 재생 특성 회복량(초당 최대체력 비율). 매 프레임 deltaTime만큼 나눠 채워 부드럽게 차오른다.
    private const float RegenPerSecond = 0.005f;
    // 은신 렌더링은 EnemyCloak가 전담. 은신 몹이면 Awake에서 Setup, 매 프레임 Tick으로 굴린다.
    private readonly EnemyCloak _cloak = new();
    // 머리 위 체력바는 EnemyHealthBar가 전담(빌보드·보간·표시 여부). 프리팹에 Slider가 없으면 통째로 no-op.
    private readonly EnemyHealthBar _bar = new();
    // 잠행 연출은 EnemyBurrow가 전담(Burrowed bool·렌더러 on/off·지면 마커). 잠행 몹이 아니면 통째로 no-op.
    private readonly EnemyBurrow _burrow = new();
    // 수영 연출은 EnemySwim이 전담(물칸 진입 시 Pool→헤엄, 지상칸 만나면 Up→지상). 수영 몹이 아니면 통째로 no-op.
    private readonly EnemySwim _swim = new();
    // 체력바 아래 디버프 아이콘은 EnemyDebuffBar가 전담. 아이콘을 안 꽂은 프리팹이면 통째로 no-op.
    private readonly EnemyDebuffBar _debuffs = new();
    // 디버프 이펙트는 EnemyDebuffEffects가 전담(종류별 프리팹·앵커, 중복 시 갱신, 앵커 추종).
    // 등록된 이펙트가 없으면 통째로 no-op.
    private readonly EnemyDebuffEffects _debuffEffects = new();
    // 보스 체력바의 스탯 숫자(체력/공격력/방어력)는 EnemyStatText가 전담. 텍스트를 안 꽂은 프리팹이면 통째로 no-op.
    private readonly EnemyStatText _statText = new();
    // 걸린 디버프와 남은 시간 장부. 인스턴스 필드라 적마다 따로 굴러간다(static이나 SO에 두면 전원이 공유해버린다).
    private readonly DebuffTracker _debuffTracker = new();
    public DebuffTracker Debuffs => _debuffTracker;

    [Tooltip("이 적에게 걸리지 않는 디버프. 보스는 여기 설정과 별개로 기절 면역이 기본으로 붙는다.")]
    [SerializeField] private DebuffType immuneDebuffs = DebuffType.None;

    // 보스 기절 면역 on/off. false로 내리면 보스도 기절에 걸려 디버프 아이콘을 눈으로 확인할 수 있다(테스트용).
    private static readonly bool BossStunImmunity = true;

    // 보스는 기절 면역이 기본이다 — 스턴 한 번으로 무력화되면 보스 구실을 못 한다.
    // Class는 CSV(EnemyTable)에서 오므로 새 보스를 추가해도 데이터 작업 없이 적용된다.
    // 프리팹의 immuneDebuffs는 여기에 더해진다(빼지는 못한다 — 보스인데 기절이 걸려야 하는 예외가 생기면 그때 방식을 바꾼다).
    public bool isImmuneDebuffs => BossStunImmunity && Class != EnemyClass.Normal;
    public DebuffType ImmuneDebuffs =>
        immuneDebuffs | (isImmuneDebuffs ? DebuffType.Stun : DebuffType.None)|(IsFlame?DebuffType.Ignite:DebuffType.None);

    public bool IsImmuneTo(DebuffType mask) => (ImmuneDebuffs & mask) != 0;
        
    private readonly Dictionary<StatType, float> baseStats = new();

    protected virtual void Update()
    {
        _move.SwimAnim = _swim.UseSwimAnim;
        _move.Tick(!IsDead && !CannotMove && !_burrow.IsTransitioning && !_swim.IsTransitioning, CurrentMoveSpeed);
        if (!gameObject.activeInHierarchy) return;
        UpdateExposedAttribute();
        _cloak.Tick(CloakClear);
        _burrow.Tick(CloakClear, transform.position, IsDead);
        _swim.Tick(Board, transform.position, IsDead);
        StunTick();
    }


    // 체력바는 LateUpdate에서 굴린다 — 이동(Update)과 카메라 회전(CameraInput.Update)이 모두 끝난 뒤라야
    // 빌보드가 한 프레임 밀리지 않는다.
    protected virtual void LateUpdate()
    {
        bool cloakedNow = (Attribute & EnemyAttribute.Cloaking) != 0;
        _bar.Tick(Hp, MaxHp, IsDead, cloakedNow,Class);
        // 스탯 숫자는 값이 바뀐 것만 갱신한다 — 매 프레임 불러도 문자열/메시를 다시 만들지 않는다.
        _statText.Tick(Hp, MaxHp, AttackPower, Defense);
        _debuffs.Tick(sc, baseStats, _debuffTracker);
        // 이동(Update)이 끝난 뒤에 앵커를 따라가야 이펙트가 한 프레임 밀리지 않는다.
        // 죽으면 스턴 연출을 끌고 가지 않는다 — 사망 애니 위에 별이 돌고 있으면 이상하다.
        _debuffEffects.Tick(_debuffTracker, IsDead);
    }

    // 외부(영웅 등)에서 이 적을 duration초간 스턴. 이동/공격/스킬 시전이 모두 멈춘다.
    // 이미 걸린 스턴보다 긴 스턴이 들어오면 만료 시각을 갱신(중첩 시 최댓값). 애니 bool은 StunTick이 켠다.
    public void Stun(float duration)
    {
        if (IsDead || duration <= 0f) return;
        if (IsSpawnInvincible) return;
        if (IsImmuneTo(DebuffType.Stun))
        {
            return;
        }
        bool wasStunned = IsStunned;
        _debuffTracker.Apply(DebuffType.Stun, duration);
        if (!wasStunned) InterruptActiveSkills();
    }

    // 자기 자신에게 거는 쪽 — 시전자도 자기 자신으로 보고 공격력을 넘긴다.
    // 그래서 지속 피해의 공격력 몫(DotDebuffSO.atkPercent)이 이 적의 공격력 기준으로 들어간다.
    public void ApplyDebuff(DebuffSO debuff, float durationOverride = 0f, float scale = 1f, object source = null)
    {
        if (debuff == null || IsDead) return;
        // debuff.Apply(DebuffContext.For(this, buffManager, gameManager, source ?? debuff), durationOverride, scale);
        DebuffApply.To(this,debuff,buffManager,gameManager,durationOverride,scale:scale,attackerAtk : AttackPower);
    }
    
    // 남에게 거는 쪽 — 이 적이 시전자이므로 자기 공격력을 같이 넘긴다.
    // 지속 피해(DotDebuffSO.atkPercent)가 그 값의 몇 %를 틱마다 더한다. 안 쓰는 디버프는 무시한다.
    public void ApplyDebuffTo(Component target, DebuffSO debuff,
        float durationOverride = 0f, float scale = 1f, object source = null)
    {
        if (debuff == null || target == null) return;

        debuff.Apply(DebuffContext.For(target, buffManager, gameManager, source ?? debuff, AttackPower),
            durationOverride, scale);
    }

    public bool HasDebuff(DebuffType mask) => _debuffTracker.HasAny(mask);

    // 면역(ImmuneDebuffs)으로 막힌 디버프 알림 — DebuffSO.Apply가 부른다.
    // 화염족은 점화를 튕겨내는 대신, 막힌 점화가 지속됐을 시간만큼 재생을 얻는다(불에 닿으면 오히려 강해진다).
    // 겹치면 더 늦은 만료 시각이 이긴다 — 불 칸에 계속 서 있으면 갱신되며 유지된다.
    public void OnDebuffBlocked(DebuffType type, float duration)
    {
        if (IsDead || duration <= 0f) return;
        if (!IsFlame || type != DebuffType.Ignite) return;

        _flameRegenExpiry = Mathf.Max(_flameRegenExpiry, Time.time + duration);
    }
    public float DebuffRemaining(DebuffType type) => _debuffTracker.GetRemaining(type);

    /// <summary>
    /// 모디파이어가 붙기 전 원본 스탯값. 표에 적힌 디버프 수치가 "이 적의 기본 스탯 기준"일 때,
    /// 현재 스탯과의 비율을 scale로 넘겨 강화 상태를 반영하는 데 쓴다(SpiderToxin의 독 참조).
    /// 기록되지 않은 스탯은 0 — 나누기 전에 반드시 확인할 것.
    /// </summary>
    protected float BaseStat(StatType type) => baseStats.TryGetValue(type, out float v) ? v : 0f;

    /// <summary>현재 스탯 / 원본 스탯. 원본이 0이면 1을 돌려준다(비율을 낼 수 없으므로 조정 없음).</summary>
    protected float StatRatio(StatType type)
    {
        float baseValue = BaseStat(type);
        return baseValue > 0f ? sc[type] / baseValue : 1f;
    }

    // 스턴 시작 시 호출 — 애니를 재생하는 액티브 스킬(BlocksBasicAttack=true)만 즉시 취소한다.
    // 배경 오라(BlocksBasicAttack=false, 지속형)는 끊지 않고 계속 유지.
    private void InterruptActiveSkills()
    {
        if (skillCtsPer == null) return;
        for (int i = 0; i < skillCtsPer.Length; i++)
            if (skillRunning[i] && skills[i] != null && skills[i].BlocksBasicAttack)
                skillCtsPer[i]?.Cancel();
    }

    // 스턴 상태를 Animator에 반영 — 바뀐 프레임에만 처리(만료 시 자동 해제도 여기서).
    // Stun bool 파라미터가 있으면 그걸로(Stun 스테이트가 연출 담당), 없으면 애니를 얼려서 "굳음"으로 대체.
    private void StunTick()
    {
        bool stunned = IsStunned && !IsDead;
        if (_stunAnimActive == stunned) return;
        _stunAnimActive = stunned;
        if (animator == null) return;

        if (HasStunParam())
            animator.SetBool("Stun", stunned);   // Stun 스테이트가 연출을 담당(권장)
        else
            animator.speed = stunned ? 0f : 1f;  // fallback: 파라미터 없으면 현재 프레임에서 얼림 → 풀리면 원복
    }
    
    // 애니메이터에 Bool "Stun" 파라미터가 있는지 1회 검사 후 캐시(파라미터 목록은 런타임에 안 바뀜).
    private bool HasStunParam()
    {
        if (_stunParamChecked) return _hasStunParam;
        _stunParamChecked = true;
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == "Stun") { _hasStunParam = true; break; }
        return _hasStunParam;
    }

    // Hero가 읽는 공개 Attribute 갱신.
    // 은신 유닛이 저지당하는 동안엔 Cloaking 비트를 빼서 Hero의 unattackable 필터를 통과(=공격 가능)시킨다.
    // 저지가 풀리면 다시 원본으로 돌아가 공격 불가. 연출(EnemyCloak)과 동일한 IsBlocked 조건이라 "보이는 것=때릴 수 있는 것"이 항상 일치.
    private void UpdateExposedAttribute()
    {
        EnemyAttribute exposed = Dataattribute;
        // 잠행 몹은 "다 솟아오른 뒤"에만 노출한다 — 아직 땅속인데 때릴 수 있으면
        // "보이는 것 == 때릴 수 있는 것" 불변식이 깨진다(셰이더 페이드일 땐 저절로 맞았지만 물리 연출은 아니다).
        if (IsCloaking && Board != null && Board.IsBlocked(gameObject)
            && (!IsBurrow || _burrow.IsSurfaced))
            exposed &= ~EnemyAttribute.Cloaking;
        // 화염족이 점화된 동안 얻는 재생도 밖에서 보이게 켠다 — 도감/툴팁이 실제 상태와 어긋나지 않게.
        if (FlameIgniteRegen) exposed |= EnemyAttribute.Regeneration;
        Attribute = exposed;
    }

    // 저지/사망이면 또렷, 아니면 은신으로 페이드. 실제 렌더링은 EnemyCloak가 처리.
    private bool CloakClear => IsDead || (Board != null && Board.IsBlocked(gameObject));



    // 초당 MaxHp*RegenPerSecond를 프레임 단위로 나눠 회복 → 1초마다 툭툭 차는 게 아니라 연속으로 차오름.

    // 본진 도달 시 EnemyMovement가 이벤트로 호출. 도달 후 처리·디스폰은 본체가 쥔다.
    private void HandleArrivedAtCore()
    {
        OnArrivedAtCore();
        if (this != null && gameObject != null) Pool.Despawn(gameObject);
    }

    protected virtual void OnArrivedAtCore()
    {
        // HpDamage를 SendDieEvent보다 먼저 호출한다 — 보스가 본진에 도달해 게임오버가 걸리는 경우,
        // isGameOver 플래그가 켜진 뒤에 SendDieEvent(→EnemyAllClear→OnResult)가 돌아야
        // OnResult의 isGameOver 가드가 먹혀서 다음날로 안 넘어간다. 순서가 반대면
        // 이 적이 마지막 남은 적일 때 게임오버 전에 이미 결과(다음날) 상태로 전환돼버린다.
        var gm = gameManager;
        if (gm == null)
            Debug.LogWarning($"[{name}] GameManager를 찾을 수 없음 — HpDamage 스킵.", this);
        else
        {
            // [애널리틱스 비활성화] int before = gm.Hp;
            gm.HpDamage(Class);
            // [애널리틱스 비활성화] int region = waveSpawner != null ? waveSpawner.Region : -1;
            // [애널리틱스 비활성화] AnalyticsRecorder.EnemyLeaked(enemyKey, Class.ToString(), region, gm.DayCount, before - gm.Hp, gm.Hp);
        }
        SendDieEvent();
        if (Board != null) Board.RemoveEnemy(gameObject);
    }
    private async UniTask RunSkillLoop(CancellationToken token)
    {
        if (skills == null || skills.Count == 0) return;
        skillTimers = new float[skills.Count];
        skillRunning = new bool[skills.Count];
        skillCtsPer = new CancellationTokenSource[skills.Count];

        while (!IsDead)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] == null || skillRunning[i] || skills[i].TriggerOnDeath) continue; // 온데스 스킬은 Die()에서만 발동
                skillTimers[i] += Time.deltaTime;
                // 공격 모션 중이면 시전 보류(타이머는 계속 쌓여서 공격 끝나면 바로 발동).
                // 침묵이면 전부 막고, 속박이면 이동류 스킬(대시)만 막는다.
                if (skillTimers[i] >= skills[i].cooldown && !_attacking && !CannotCast
                    && !(CannotMove && skills[i].MovesSelf))
                {
                    RunSkill(i, token).Forget();
                }
            }
            await UniTask.Yield(token);
        } 
        
    }
    public virtual void EnemySoundAttack()
    {

    }

    // ---- 잠행 Animation Event ----
    // 각 클립 마지막 프레임에 Animation Event로 이 메서드 이름을 걸어준다.
    // 안 걸어도 EnemyBurrow의 타임아웃이 강제로 넘겨주지만(경고 로그), 연출 타이밍이 어긋난다.
    public void AnimEvent_Burrowed() => _burrow.NotifyBurrowed();   // 파고들기 끝 → 렌더러 off

    // ---- 수영 Animation Event ----
    // Pool(잠수)·Up(상승) 클립 마지막 프레임에 Animation Event로 이 이름을 걸어준다.
    // 안 걸면 EnemySwim의 swimTimeout이 강제로 넘겨주지만(경고 로그) 연출 타이밍이 어긋난다.
    public void AnimEvent_Dived() => _swim.NotifyDived();     // 잠수 끝 → 헤엄 이동 시작
    public void AnimEvent_Emerged() => _swim.NotifyEmerged(); // 상승 끝 → 지상 이동 복귀

    public void AnimEvent_Surfaced()
    {
        _burrow.NotifySurfaced();
        if (IsDead) return;
        if (Board == null || !Board.IsBlocked(gameObject)) return;
        if (!Board.TryGetCell(Board.WorldToCell(transform.position), out Tile tile)) return;
        if (tile.OccupantObject == null) return;
        if (tile.OccupantObject.GetComponentInParent<Hero>() is not Hero hero || hero.IsDead) return;
        hero.TakeDamage(AttackPower);
        if (burrowEmergeStun > 0f) (hero as IStunAble)?.Stun(burrowEmergeStun);
    }
    
    private async UniTask RunSkill(int index, CancellationToken token)
    {
        skillRunning[index] = true;
        // 스킬별 CTS(상위 token에 연결) — 스턴 시 이 스킬만 개별 취소할 수 있게(지속형 오라는 건드리지 않음).
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        skillCtsPer[index] = cts;
        try { await skills[index].Execute(this, cts.Token); }
        catch (System.OperationCanceledException) { /* 비활성/파괴/스턴으로 취소 — 정상 */ }
        finally
        {
            skillRunning[index] = false;
            skillTimers[index] = 0f;
            skillCtsPer[index] = null;
            cts.Dispose();
        }
    }

    private async UniTask RunAttackLoop(CancellationToken token)
    {
        float attackTimer = 0f;
        while (!IsDead)
        {
            float interval = AttackSpeed > 0f ? 1f / AttackSpeed : 1f; //attackspped = 초당 공격횟수
            // 공격 모션/스킬 시전 중엔 딜레이를 세지 않는다 — 공격이 끝난 뒤부터 interval을 새로 채워
            // 매 공격 사이에 온전한 간격을 보장(안 그러면 모션 중 타이머가 넘쳐 애니 끝나자마자 연사됨).
            if (!_attacking && !AnySkillRunning() && !CannotAttack)
            {
                attackTimer += Time.deltaTime;
                if (attackTimer >= interval)
                {
                    attackTimer = 0f;
                    Attack();
                }
            }
            await UniTask.Yield(token);
        }
    }
    protected void LoadStats()
    {
        if (string.IsNullOrEmpty(enemyKey)) enemyKey = GetType().Name;

        var data = EnemyStatLoader.Load(enemyKey);
        if (data == null) return;

        ApplyData(data);
        EnemyStatLoader.ResolveSkills(data.Skills, skills);
        // 화염 오라는 화염족이 특성으로 갖는 것이다 — 특성만 켜면 붙으므로 CSV 두 칸(Attribute·Skills)을
        // 맞춰 적을 필요가 없다(빠뜨려서 오라가 조용히 없는 사고를 막는다).
        // ApplyData가 바로 위에서 Dataattribute를 정하므로 이 시점의 IsFlame은 유효하고,
        // LoadStats는 Awake·OnEnable에서 매번 불리지만 HasFlameAura가 두 번째부터 걸러낸다.
        if (IsFlame && !HasFlameAura()) EnemyStatLoader.ResolveSkills(FlameAuraSkillId, skills);
    }

    // 이미 화염 오라 계열 스킬을 들고 있는가. 수치를 달리한 전용 에셋을 Skills 칸에 적은 경우
    // 기본 오라를 덧붙이면 오라가 둘 돌아 피해와 이펙트가 두 겹이 된다 — 그걸 막는다.
    private bool HasFlameAura()
    {
        for (int i = 0; i < skills.Count; i++)
            if (skills[i] is FireZoneSO) return true;
        return false;
    }
    
    protected virtual void ApplyData(EnemyTable.Data data)
    {
        if(!firstEnable||gameManager==null)
        {
            firstEnable = true;
            sc.AddStat(StatType.HP,data.Health);
            sc.AddStat(StatType.ATK,data.Attack);
            sc.AddStat(StatType.AS,data.AttackSpeed);
            sc.AddStat(StatType.DEF,data.Defense);
            sc.AddStat(StatType.SPD,data.MoveSpeed);
            RecordBaseStats(data.Health, data.Attack, data.AttackSpeed, data.Defense, data.MoveSpeed);
            Type = ParseEnum(data.Type, EnemyType.Melee);
            Class = ParseEnum(data.Class, EnemyClass.Normal); 
            Attribute = ParseAttribute(data.Attribute);
            Dataattribute = Attribute;
        }
        else
        {
            // 일차·해금 지역 배율은 EnemyStatScaling이 계산한다 — 스테이지 정보 툴팁이 스폰 전에
            // 보여주는 값과 같은 식을 써야 하므로 표와 식을 그쪽 한 곳에만 둔다.
            var scaled = EnemyStatScaling.Compute(data, Class, gameManager.DayCount);
            sc.SetBaseValue(StatType.HP,scaled.Hp);
            sc.SetBaseValue(StatType.ATK,scaled.Attack);
            sc.SetBaseValue(StatType.AS,data.AttackSpeed);
            sc.SetBaseValue(StatType.DEF,scaled.Defense);
            sc.SetBaseValue(StatType.SPD,data.MoveSpeed);
            RecordBaseStats(scaled.Hp, scaled.Attack, data.AttackSpeed, scaled.Defense, data.MoveSpeed);
        }
        Range = data.Range;
        Hp = sc[StatType.HP];
        _bar.ResetTo(Hp, MaxHp); // 스폰 시 보간 없이 즉시 풀피로(풀 재사용 시 이전 값 잔상 제거)
        IsDead = false;
        // 확인용 임시 로그. bossName 가드 아래에 두면 보스가 아닌 몹은 그 return에 걸려 안 찍히므로 가드보다 위에 둔다.
        // string dayInfo = gameManager != null ? $"{gameManager.DayCount}일차" : "gameManager 미주입 → 일차·지역 배율 미적용";
        // Debug.Log($"[{enemyKey}] 체력 {Hp} · 공격 {AttackPower} · 방어 {Defense} · " +
        //     $"해금 {SpawnerManager.UnlockedRegionCount}개(체력×{EnemyStatScaling.RegionHpScale(Class)}, " +
        //     $"방어×{EnemyStatScaling.RegionDefenseScale(Class)}) · {dayInfo}", this);
        if(bossName == null)return;
        bossName.text = DataTableManager.StringTable.Get(data.Name);
        //MoveSpeed = data.MoveSpeed;
    }

    // 배율 표와 계산식은 EnemyStatScaling으로 옮겼다 — 스테이지 정보 툴팁(StageInfoView)이
    // 스폰 전에 같은 수치를 보여줘야 해서, 한쪽만 고쳐 어긋나는 일이 없게 한 곳에 둔다.

    private void RecordBaseStats(float hp, float atk, float attackSpeed, float def, float spd)
    {
        baseStats[StatType.HP]  = hp;
        baseStats[StatType.ATK] = atk;
        baseStats[StatType.AS]  = attackSpeed;
        baseStats[StatType.DEF] = def;
        baseStats[StatType.SPD] = spd;
    }

    // CSV 문자열 → enum. 비었거나 못 읽으면 fallback으로 대체(대소문자 무시).
    private static T ParseEnum<T>(string raw, T fallback) where T : struct, System.Enum
    {
        if (!string.IsNullOrEmpty(raw) && System.Enum.TryParse(raw.Trim(), true, out T value))
            return value;
        return fallback;
    }
    internal static EnemyAttribute ParseAttribute(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return EnemyAttribute.None;
        EnemyAttribute result = EnemyAttribute.None;
        foreach (string token in raw.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
            if (System.Enum.TryParse(token.Trim(), true, out EnemyAttribute flag))
                result |= flag;
        return result;
    }


    public void ApplyDamageReductionShield(float flatReduce, float duration)
    {
        _damageBlock = Mathf.Max(0f, flatReduce);
        _shieldExpiry = Time.time + duration;
    }

    public void TakeDamage(int damage,bool ignore = false)
    {
        if(IsDead)return;
        if(IsSpawnInvincible)return;
        // 방어무시(ignore)는 방어력만 걷어낸다 — 실드 감소량(_damageBlock)은 그대로 남는다.
        // 둘을 같이 0으로 만들면 지속 피해가 실드까지 뚫는다.
        int reduce = (ignore ? 0 : Defense) + (IsShielded ? Mathf.RoundToInt(_damageBlock) : 0);
        int hitDamage = Mathf.Max(1, damage - reduce);
        if(IsHitsShield)
        {
            Hp -= 1f;
        }
        else
        {
            Hp -= hitDamage;
        }                     
        if (!_berserkOn && Hp < MaxHp * 0.5f && IsBerserk)
        {
            _berserkOn = true;
            // MoveSpeed += 3f;
            // AttackPower += 10;
            sc.AddModifier(StatType.SPD,new Modifier(ModifierType.Flat,3f,0f,StatLayer.Equip,this));
            sc.AddModifier(StatType.ATK,new Modifier(ModifierType.Flat,10f,0f,StatLayer.Equip,this));
        }
        if(Hp<=0)Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        Hp = Mathf.Min(Hp + amount, MaxHp);
    }
    private async UniTask Regeneration(CancellationToken token)
    {
        while(!IsDead)
        {
            // 매 프레임 다시 본다 — 화염족은 점화가 붙었다 풀리는 동안 재생이 켜졌다 꺼진다.
            // 원본 재생 특성이면 항상 true라 기존 거동과 같다.
            if(IsRegeneration && Hp<MaxHp)
            Hp = Mathf.Min(Mathf.Max(1f,Hp + MaxHp * RegenPerSecond * Time.deltaTime), MaxHp);
            await UniTask.Yield(token);
        }
    }

    public void SetCurrentHp(float hp) => Hp = Mathf.Clamp(hp, 1f, MaxHp);

    public int SplitGeneration { get; private set; }

  

    public void SetSplitGeneration(int gen) => SplitGeneration = gen;

    // 스폰 직후 인스턴스 스케일을 원래 크기의 mul배로 설정(분열체 축소용). OnEnable에서 매 스폰 원복되므로 풀 재사용 안전.
    public void SetScaleMul(float mul) => transform.localScale = _baseScale * mul;
    public virtual void Attack()
    {
        if (IsDead || Board == null || skillCts == null) return;
        GameObject target = FindAttackTarget();
        if (target == null) return; // 사거리에 대상 없으면 멈추지도, 공격하지도 않음
        if (IsUnJudged) return;                                              // 저지 불가 = 막는 칸을 통과만, 스쳐 지나가며 때리지 않음
        if (Type == EnemyType.Melee && !Board.IsBlocked(gameObject)) return; // 근접은 실제로 저지당했을 때만 공격
        if (IsBurrow && !_burrow.IsSurfaced) return;                         // 잠행: 다 솟아오르기 전엔 때리지 않는다

        transform.LookAt(target.transform);
        _attacking = true;
        _move.Pause();
        if (animator != null)
        {
            float interval = AttackSpeed > 0f ? 1f / AttackSpeed : 1f;
            float clipLength = AttackClipLength; // 2타 몹은 두 클립 길이의 합이 온다(GrimReaper 참조)
            animator.speed = (clipLength > interval && interval > 0f) ? clipLength / interval : 1f;
            animator.SetTrigger("Attack");
        }
        AttackWatchdog(skillCts.Token).Forget(); // 애니 끝나면 상태 복구(이벤트 누락 대비 타임아웃 포함)
    }

    
    public virtual void AnimEvent_AttackHit()
    {
        if (IsDead) return;
        GameObject target = FindAttackTarget(); 
        if (target != null && target.GetComponentInParent<IDamageAble>() is IDamageAble dmg)
        {
            EnemySoundAttack();
            dmg.TakeDamage(AttackPower);
        }
            
    }
    
    protected GameObject FindAttackTarget()
    {
        if (Board == null) return null;
        Vector2Int origin = Board.WorldToCell(transform.position);
        GameObject target = null;
        int bestDist = int.MaxValue;
        foreach (Tile tile in Board.GetTiles(origin, Range))
        {
            if (tile.OccupantObject == null) continue;
            int d = EnemyTargeting.Distance(origin, tile.Coord);
            // if(tile.OccupantObject.GetComponent<Hero>().IsDead) continue;
            if (tile.OccupantObject.GetComponent<Hero>() is not Hero h || h.IsDead) continue;
            if (d < bestDist) { bestDist = d; target = tile.OccupantObject; }
        }
        return target;
    }

    protected virtual async UniTask AttackWatchdog(CancellationToken token)
    {
        try { await WaitForAttackAnim("Attack", 5f, token); }
        catch (OperationCanceledException) { }
        finally
        {
            if (animator != null) animator.speed = 1f; // 공격 배속 원복(전역 speed이므로 이동/사망 애니에 안 새게)
            _attacking = false; // 공격 모션 끝 → 스킬/다음 공격 허용
            _move.Resume();
        }
    }
    protected virtual async UniTask WaitForAttackAnim(string stateName,float timeout,CancellationToken token)
    {
        const int layer = 0;
        float elapsed = 0f;
        while (animator != null && !animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[{name}] Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", this);
                return;
            }
            await UniTask.Yield(token);
        }
        if (animator == null) return;

        var info = animator.GetCurrentAnimatorStateInfo(layer);
        await UniTask.Delay(TimeSpan.FromSeconds(info.length / Mathf.Max(0.01f, animator.speed)), cancellationToken: token);
    }

    public virtual void Die()
    {
        if (IsDead) return;
        IsDead = true;
        // 스턴 연출을 바로 내린다 — 다음 Update까지 기다리면 그 한 프레임 동안 Stun bool이 켜진 채로
        // Die 트리거가 걸려, 컨트롤러 전이 순서에 따라 사망 애니가 밀릴 수 있다.
        // IsDead를 먼저 세웠으므로 StunTick이 "스턴 아님"으로 보고 bool을 내린다(폴백 경로는 speed를 1로 돌린다).
        StunTick();
        if (animator != null) animator.speed = 1f; // 공격 배속 등 남은 속도 조작까지 원복
        _move.Stop();
        DieSound();
        if (Board != null) Board.RemoveEnemy(gameObject);
        if (skillCts == null) { SendDieEvent(); Despawn(); return; }
        DieRoutine(skillCts.Token).Forget();
    }

    private void TriggerDeathSkills()
    {
        if (skills == null) return;
        for (int i = 0; i < skills.Count; i++)
            if (skills[i] != null && skills[i].TriggerOnDeath)
                skills[i].Execute(this, CancellationToken.None).Forget();
    }
    private async UniTask DieRoutine(CancellationToken token)
    {
        try
        {
            if (animator != null) animator.SetTrigger("Die");
            await WaitForDeathAnim("Die", 5f, token);
        }
        // 취소(비활성·씬 언로드)는 정상 경로다 — 여기서 삼키지 않으면 .Forget()이 미처리 예외로 남긴다.
        // return이 아래를 건너뛰지는 않는다: finally는 return 전에 반드시 돈다.
        // 어떤 경로로 끝나든 적을 판에서 치워야 하므로 일부러 그렇게 둔 것이다.
        catch (OperationCanceledException) { return; }
        finally
        {
            // 한 단계가 던져도 나머지를 계속한다. 중간에 예외가 새면 Despawn이 통째로 날아가,
            // Enemycount만 내려간 채 적은 판에 남아 EnemyAllClear가 산 적을 두고 터진다.
            // 자원을 사망 통보보다 먼저 주는 이유: SendDieEvent가 라운드 종료까지 이어질 수 있어서,
            // 뒤에 두면 결과 집계가 이번 킬의 자원을 놓친다.
            DeathStep(TriggerDeathSkills, "죽음 스킬 발동");
            DeathStep(GetSpecial, "특수 자원 지급");
            DeathStep(SendDieEvent, "사망 통보");
            DeathStep(Despawn, "디스폰");
        }
    }
    public virtual void DieSound()
    {
        
    }

    // 사망 처리 한 단계를 감싼다. 실패해도 다음 단계로 넘어가되, 무슨 단계가 왜 깨졌는지는 남긴다.
    private void DeathStep(Action step, string what)
    {
        try { step(); }
        catch (Exception e) { Debug.LogError($"[{name}] 사망 처리 '{what}' 실패 — {e}", this); }
    }

    // 등급별 특수 자원 지급량. ResourcesManager.GetSpecial()이 한 번에 1개씩만 주므로 횟수로 준다.
    // private const int EliteSpecialDrop = 1;
    private const int BossSpecialDrop = 5;
    // 주입 누락 경고는 한 번만 — 적이 죽을 때마다 찍으면 콘솔이 잠긴다.
    private static bool _warnedNoResourceManager;

    private void GetSpecial()
    {
        if (Class == EnemyClass.Normal||Class==EnemyClass.Elite) return;
        if (resourceManager == null)
        {
            if (!_warnedNoResourceManager)
            {
                _warnedNoResourceManager = true;
                Debug.LogWarning($"[{name}] ResourcesManager가 주입되지 않아 특수 자원을 주지 못했습니다 " +
                    "(이 경고는 한 번만 표시됩니다).", this);
            }
            return;
        }

        // int amount = Class == EnemyClass.Boss ? BossSpecialDrop : EliteSpecialDrop;
        // for (int i = 0; i < amount; i++) resourceManager.GetSpecial();
        for (int i = 0; i < BossSpecialDrop; i++) resourceManager.GetSpecial();
    }
    private async UniTask WaitForDeathAnim(string stateName, float timeout, CancellationToken token)
    {
        const int layer = 0;
        float elapsed = 0f;
        while (animator != null && !animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[{name}] Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", this);
                return;
            }
            await UniTask.Yield(token);
        }
        if (animator == null) return;

        var info = animator.GetCurrentAnimatorStateInfo(layer);
        await UniTask.Delay(TimeSpan.FromSeconds(info.length / Mathf.Max(0.01f, animator.speed)), cancellationToken: token);
    }

    
    protected virtual void Despawn()
    {
        if (this != null && gameObject != null) Pool.Despawn(gameObject);
    }

}