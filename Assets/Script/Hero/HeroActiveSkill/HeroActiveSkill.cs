using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum SkillTargetScope { Self, AnywhereOnBoard }

// Hero 프리팹에 붙는 액티브 스킬 컴포넌트(Hero당 최대 1개). HeroSkillCastController가 플레이어
// 클릭을 받아 Hero.TryUseActiveSkill로 발동시킨다. 자기 버프+장판+즉발 피해로 구성되는 대부분의
// 스킬은 필드만 채우면 코드 없이 동작하도록 기본 Execute가 처리한다. 지속시간이 있는 특수 스킬
// (예: 스탠스 교체)만 서브클래스에서 Execute를 오버라이드한다.
public class HeroActiveSkill : MonoBehaviour
{
    public SkillTargetScope targetScope = SkillTargetScope.Self;
    public List<BuffEffect> buffList = new();
    [Tooltip("GroundZoneEffect 컴포넌트가 붙은 프리팹 — 없으면 장판 없음")]
    public GameObject groundZonePrefab;

    [Header("즉시 피해(장판 아님)")]
    [Tooltip("0 = 없음. sc[ATK] * instantDamagePer 만큼 대상 칸의 적 전원에게 즉시 피해.")]
    public float instantDamagePer = 0f;
    public GameObject instantHitEffect;
    public float instantHitEffectLifetime = 1f;

    [Header("쿨타임 / 지속시간")]
    [Tooltip("스킬 사용 후 재사용까지 걸리는 시간(초). Stat(CDR)은 아직 적용되지 않음(기존 동작 유지).")]
    public float cooldown = 0f;
    [Tooltip("0 = 즉시 종료(기존 방식과 동일). 양수면 그 시간 동안 HeroSkillState로 전이한다.")]
    public float duration = 0f;
    [Tooltip("true면 duration 동안(또는 duration==0이면 이번 프레임) 기본공격 상태머신을 막는다.")]
    public bool blocksBasicAttack = true;

    protected Hero hero;
    protected virtual void Awake() => hero = GetComponent<Hero>();

    // targetTile: targetScope==AnywhereOnBoard일 때 플레이어가 클릭한 타일. Self일 땐 캐스터 자신의 칸.
    public virtual async UniTask Execute(Tile targetTile, CancellationToken token)
    {
        if (buffList.Count > 0)
            AttackDamageUtil.ApplySelfBuffs(hero, buffList, hero.Buffs, this);

        if (groundZonePrefab != null)
            hero.SpawnGroundZone(groundZonePrefab, targetTile.WorldTop);

        if (instantDamagePer > 0f)
        {
            int dmg = Mathf.RoundToInt(hero.SC[StatType.ATK] * instantDamagePer);
            foreach (GameObject enemy in hero.GetObjectsInRange(targetTile.WorldTop, 0, RangeShape.Diamond))
                if (enemy.GetComponentInParent<IDamageAble>() is IDamageAble d)
                    d.TakeDamage(dmg);
            hero.SpawnEffect(instantHitEffect, targetTile.WorldTop, instantHitEffectLifetime);
        }

        if (duration > 0f)
            await UniTask.Delay(System.TimeSpan.FromSeconds(duration), cancellationToken: token);
    }
}
