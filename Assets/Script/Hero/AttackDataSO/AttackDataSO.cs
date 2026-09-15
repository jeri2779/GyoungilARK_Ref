using System.Collections.Generic;
using UnityEngine;

public enum AnimSelectMode { Sequential, Random }
// Discrete = 지금까지의 유일한 방식(애니메이션 이벤트 윈도우 기반 1회 집행).
// Continuous = 신규. ContinuousBeamStrategy가 continuousTickInterval마다 continuousDuration 동안
// 애니메이션 이벤트 없이 직접 tick 피해를 적용하는 채널링형 공격(예: 레이저).
public enum AttackTimingMode { Discrete, Continuous }

// timingMode == Continuous 전용. 채널링 한 tick이 "잠긴 타겟에 즉시 피해(Beam)"인지 "실제 투사체
// 발사(ProjectileVolley)"인지 "타겟 잠금 없이 caster 위치 중심 AOE(SelfArea)"인지를 명시한다.
// 예전엔 projectilePrefab != null 이라는 필드 이름과 무관한 null 체크로 갈렸다.
//
// SelfArea: 잠긴 타겟이 없어도(특정 적이 죽거나 벗어나도) 끊기지 않는다 — 매 틱 AttackDamageUtil의
// 자기중심 AOE 분기(attackType=Area, targetMode=SameTarget)를 그대로 호출해 ctx.self 주변 areaRange
// 내 모든 적을 때린다. 제자리에서 계속 도는 근접 채널링(예: 회전 베기) 용도. continuousDuration>0이면
// 그 시간 동안 무조건 돌고, continuousDuration<=0("무제한")이면 다른 delivery와 동일한 "유효 대상 없으면
// 종료" 계약을 따른다 — SelfArea에선 그 유효 대상이 "자기 위치 areaRange 안의 적 존재 여부"다.
//
// 스코프 경고: 이 enum은 ContinuousBeamStrategy 단 한 곳에서만 읽는다. 근접/원거리/힐 구분을
// 여기에 추가하지 말 것 — 그 축은 {SwordMan,Archer,Mage,Healer}AttackState가 주입하는
// IAttackExecutor가 유일한 권위이고, 데이터 쪽에 같은 구분을 만들면 진실 소스가 둘이 된다.
// SelfArea도 "근접 전용"이 아니라 "타겟 고정 없는 자기중심 AOE 틱"이라는 배달 형태를 뜻할 뿐이다.
// groundZonePrefab도 넣지 말 것 — 장판은 배달 방식이 아니라 모든 배달 방식에 부가로 co-occur하는
// 옵션이다(MeleeAttackExecutor/RangedAttackExecutor/Projectile.Hit에서 각각 독립적으로 체크됨).
public enum ContinuousDelivery { Beam = 0, ProjectileVolley = 1, SelfArea = 2 }

[CreateAssetMenu(fileName = "AttackData", menuName = "HeroAttack/AttackData")]
public class AttackDataSO : ScriptableObject
{
    [Header("탐지 (타겟 후보를 찾는 범위)")]
    public int range = 3;
    public RangeShape rangeShape = RangeShape.Diamond;
    [Tooltip("이 공격으로 공격할 수 없는 적 속성(비트 플래그) — 예: 비행/은신")]
    public EnemyAttribute unattackableTarget = EnemyAttribute.Fly | EnemyAttribute.Cloaking;

    // AoE/장판 필터용 — 은신은 사양상 AoE에 그대로 맞아야 하므로 Cloaking 비트는 제외한다.
    public EnemyAttribute AreaUnattackableTarget => unattackableTarget & ~EnemyAttribute.Cloaking;

    [Header("타격 형태")]
    public AttackType attackType = AttackType.Single;
    public float attackPer = 1f;
    public int attackCount = 1;
    public TargetMode targetMode = TargetMode.SameTarget;
    public int targetCount = 1;
    public float shotInterval = 0.1f;

    [Header("범위 공격")]
    public AreaShape areaShape = AreaShape.Diamond;
    public int areaRange = 1;
    public int lineLength = 2;
    [Tooltip("Area+SameTarget일 때 AOE 중심을 자기 위치가 아니라 락온된 타겟 위치로 사용한다 (원거리 스킬의 타겟 중심 스플래시용)")]
    public bool areaCenterOnTarget = false;

    [Header("체인 공격")]
    public int chainRange = 2;
    public int chainCount = 3;
    public float chainFalloff = 0.8f;
    public GameObject chainEffectPrefab;
    public float chainEffectLifetime = 0.3f;

    [Header("타이밍")]
    public AttackTimingMode timingMode = AttackTimingMode.Discrete;
    [Tooltip("timingMode==Continuous 전용: 매 tick 즉시 피해(Beam)인지 투사체 발사(ProjectileVolley)인지 타겟 고정 없는 자기중심 AOE(SelfArea)인지")]
    public ContinuousDelivery continuousDelivery = ContinuousDelivery.Beam;
    [Tooltip("timingMode==Continuous 전용: 채널링 총 지속시간(초). 0 이하로 두면 시간 제한 없이 유효 대상이 사라질 때까지 계속 채널링한다(Beam/ProjectileVolley는 잠긴 타겟, SelfArea는 자기 주변 areaRange 안의 적 존재 여부가 기준).")]
    public float continuousDuration = 2f;
    [Tooltip("timingMode==Continuous 전용: 피해 틱 주기(초)")]
    public float continuousTickInterval = 0.2f;
    [Tooltip("continuousDelivery==Beam 전용: 캐스터→타겟을 잇는 LineRenderer 이펙트 프리팹")]
    public GameObject beamEffectPrefab;

    public AnimSelectMode selectMode = AnimSelectMode.Sequential;
    public string[] animTriggers = { "Attack" };
    public Projectile projectilePrefab;

    // 이 공격이 재생하는 애니메이션 클립의 기본 길이(초). 0이면 배속을 걸지 않는다(안전 폴백).
    public float clipLength = 0f;
    public List<BuffEffect> buffList;
    [Tooltip("이 공격에 맞은 대상에게 걸리는 디버프 — 적 디버프 시스템(DebuffSO)을 그대로 재사용한다")]
    public List<TargetDebuffRef> targetDebuffs;
    [Tooltip("GroundZoneEffect 컴포넌트가 붙은 프리팹 — null이면 이 공격은 장판을 깔지 않음")]
    public GameObject groundZonePrefab;

    public GameObject attackEffect;
    [Tooltip("attackEffect가 풀로 회수되기까지의 시간(초). 0 이하면 회수 타이머를 걸지 않음")]
    public float attackEffectLifetime = 1f;
    public GameObject hitEffect;
    [Tooltip("hitEffect가 풀로 회수되기까지의 시간(초). 0 이하면 회수 타이머를 걸지 않음")]
    public float hitEffectLifetime = 1f;

    [Header("사운드")]
    [Tooltip("EnemySoundManager의 EnemySoundDataBase에 등록된 키. 비워두면 사운드를 재생하지 않는다.")]
    public string attackSoundKey;

    [Header("아군 힐 / 피흡")]
    public float lifestealPercent = 0f;
    public float allyHealAmount = 0f;
    public int allyHealRange = 2;
    public RangeShape allyHealRangeShape = RangeShape.Diamond;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (timingMode != AttackTimingMode.Continuous) return;
        if (continuousDelivery == ContinuousDelivery.ProjectileVolley && projectilePrefab == null)
            Debug.LogWarning($"[{name}] ProjectileVolley인데 projectilePrefab이 비어 있습니다.", this);
        if (continuousDelivery == ContinuousDelivery.Beam && projectilePrefab != null)
            Debug.LogWarning($"[{name}] Beam이므로 projectilePrefab({projectilePrefab.name})은 무시됩니다.", this);
        if (continuousDelivery == ContinuousDelivery.Beam && beamEffectPrefab == null)
            Debug.LogWarning($"[{name}] Beam인데 beamEffectPrefab이 비어 있습니다.", this);
        if (continuousDelivery == ContinuousDelivery.SelfArea && projectilePrefab != null)
            Debug.LogWarning($"[{name}] SelfArea이므로 projectilePrefab({projectilePrefab.name})은 무시됩니다.", this);
        if (continuousDelivery == ContinuousDelivery.SelfArea && !(attackType == AttackType.Area && targetMode == TargetMode.SameTarget))
            Debug.LogWarning($"[{name}] SelfArea는 attackType=Area, targetMode=SameTarget일 때만 자기중심 AOE 분기를 탑니다. 지금 설정으로는 데미지가 들어가지 않습니다.", this);
    }
#endif
}

public enum AttackType { Single, Area }
public enum TargetMode { SameTarget, DifferentEnemies }
public enum AreaShape { Diamond, Square, Line, Chain }

[System.Serializable]
public struct BuffEffect
{
    public StatType statType;
    public ModifierType modifierType;
    public float value;
    public float duration;
    public int maxStacks;
}

[System.Serializable]
public struct TargetDebuffRef
{
    public DebuffSO debuff;
    [Tooltip("0이면 debuff 자체 duration을 사용")]
    public float durationOverride;
    [Tooltip("0 이하면 1(배율 없음)로 취급")]
    public float scale;
}
