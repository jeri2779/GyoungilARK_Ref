using UnityEngine;

// timeToTrigger초가 지날 때마다 stanceDuration초 동안 basePattern[0]을 stanceAttackData로 교체했다가
// 자동 복구한다. AuraTrait와 동일하게 코루틴 없이 OnPassiveTick 필드 타이머로 처리.
[DisallowMultipleComponent]
public class TimedStanceTrait : HeroTrait
{
    public float timeToTrigger = 10f;

    [Header("Stance")]
    public AttackDataSO stanceAttackData;
    public float stanceDuration = 3f;

    private float elapsedTime;
    private bool stanceActive;
    private float stanceTimer;
    private AttackDataSO previousAttackData;

    public override void OnPassiveTick(float deltaTime)
    {
        if (stanceActive)
        {
            stanceTimer += deltaTime;
            if (stanceTimer >= stanceDuration)
            {
                hero.SwapPrimaryAttackData(previousAttackData);
                stanceActive = false;
            }
            return;
        }

        elapsedTime += deltaTime;
        if (elapsedTime >= timeToTrigger) Trigger();
    }

    private void Trigger()
    {
        previousAttackData = hero.SwapPrimaryAttackData(stanceAttackData);
        stanceActive = true;
        stanceTimer = 0f;
        elapsedTime = 0f;
    }

    public override void OnDayStart()
    {
        if (stanceActive)
        {
            hero.SwapPrimaryAttackData(previousAttackData);
            stanceActive = false;
            stanceTimer = 0f;
        }
        elapsedTime = 0f;
    }
}
