using UnityEngine;

[DisallowMultipleComponent]
public class HeavyRechanceTrait : HeroTrait
{
    public AttackDataSO heavyAttackData;
    [Range(0f, 1f)] public float chance = 0.7f;
    public bool allowRecursive = false;

    public override void OnHit(GameObject target, int amount, bool isCrit)
    {
        if (hero.LastUsedAttackData != heavyAttackData) return;   // 강공으로 맞았을 때만 재발동 판정
        if (!allowRecursive && hero.LastAttackWasProc) return;    // 재귀 재발동 금지 옵션
        if (Random.value < chance) hero.QueueNextAttackOverride(heavyAttackData, isProc: true);
    }
}
