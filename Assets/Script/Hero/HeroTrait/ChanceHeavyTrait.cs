using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class ChanceHeavyTrait : HeroTrait
{
    public float chance = 0.25f;
    public AttackDataSO watchedAttack; // null이면 모든 공격 대상 (기존 동작 유지)
    public AttackDataSO heavyAttackData;

    public override void OnAttackPerformed(AttackDataSO data)
    {
        if (watchedAttack != null && data != watchedAttack) return;

        if (Random.value < chance)
        {
            hero.QueueNextAttackOverride(heavyAttackData);
        }
    }
}