using UnityEngine;

[DisallowMultipleComponent]
public class StackingMaxEffectTrait : HeroTrait
{
    public StatType stat = StatType.ATK;
    public ModifierType modifierType = ModifierType.Additive;
    public float valuePerStack = 0.05f;
    public int maxStacks = 5;
    public float duration = 6f;
    public GameObject maxStackEffectPrefab;
    public float maxStackEffectLifetime = 1f;

    public override void OnAttackPerformed(AttackDataSO data)
    {
        hero.Buffs.ApplyStackingModifier(hero, stat, modifierType, valuePerStack, duration, maxStacks, this);
        if (maxStackEffectPrefab != null && hero.Buffs.GetStacks(hero, this) == maxStacks)
            hero.SpawnEffect(maxStackEffectPrefab, hero.transform.position, maxStackEffectLifetime);
    }
}
