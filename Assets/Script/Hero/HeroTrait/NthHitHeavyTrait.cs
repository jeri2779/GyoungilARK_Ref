using UnityEngine;

// 기존 NthHitSelectorSO는 SO라서 이 트레잇을 쓰는 모든 Hero가 같은 에셋을 공유했다(문제는 없었지만
// hitCount 자체를 selector가 아니라 HeroAttackRunner가 들고 있어서 다행히 새지 않았음). Component는
// Hero 프리팹당 하나씩 붙으므로 인스턴스 필드(count) 하나로 동일한 결과를 더 단순하게 얻는다.
[DisallowMultipleComponent]
public class NthHitHeavyTrait : HeroTrait
{
    public int everyN = 4;
    public AttackDataSO watchedAttack; // null이면 모든 공격 카운트 (기존 동작 유지)
    public AttackDataSO heavyAttackData;

    private int count;

    public override void OnAttackPerformed(AttackDataSO data)
    {
        if (watchedAttack != null && data != watchedAttack) return;

        count++;
        if (count >= everyN)
        {
            count = 0;
            hero.QueueNextAttackOverride(heavyAttackData);
        }
    }
}
