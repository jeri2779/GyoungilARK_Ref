using UnityEngine;

// 감시 대상 공격을 hitsToTrigger회 가하면 stanceDuration초 동안 basePattern[0]을 stanceAttackData로
// 교체했다가 자동 복구한다. AttackStanceSkill과 동일한 SwapPrimaryAttackData 메커니즘을 재사용하되,
// 액티브 스킬이 아니라 패시브 트레잇이므로 코루틴/토큰 없이 OnPassiveTick 타이머로 복구를 처리한다.
[DisallowMultipleComponent]
public class HitCountStanceTrait : HeroTrait
{
    public AttackDataSO watchedAttack;
    public int hitsToTrigger = 5;

    [Header("Stance")]
    public AttackDataSO stanceAttackData;
    public float stanceDuration = 3f;

    private int hitCount;
    private bool stanceActive;
    private float stanceTimer;
    private AttackDataSO previousAttackData;

    public override void OnAttackResolved(AttackDataSO data)
    {
        if (stanceActive) return;
        if (watchedAttack != null && data != watchedAttack) return;

        hitCount++;
        if (hitCount >= hitsToTrigger) Trigger();
    }

    public override void OnPassiveTick(float deltaTime)
    {
        if (!stanceActive) return;

        stanceTimer += deltaTime;
        if (stanceTimer >= stanceDuration)
        {
            hero.SwapPrimaryAttackData(previousAttackData);
            stanceActive = false;
        }
    }

    private void Trigger()
    {
        previousAttackData = hero.SwapPrimaryAttackData(stanceAttackData);
        stanceActive = true;
        stanceTimer = 0f;
        hitCount = 0;
    }

    public override void OnDayStart()
    {
        if (stanceActive)
        {
            hero.SwapPrimaryAttackData(previousAttackData);
            stanceActive = false;
            stanceTimer = 0f;
        }
        hitCount = 0;
    }
}
