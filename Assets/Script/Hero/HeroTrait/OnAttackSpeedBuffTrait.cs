using UnityEngine;

[DisallowMultipleComponent]
public class OnAttackSpeedBuffTrait : HeroTrait
{
    public float attackSpeedBonus = 0.1f;
    public float duration = 3f;
    public int maxStacks = 1;

    public override void OnAttackPerformed(AttackDataSO data)
        => hero.Buffs.ApplyStackingModifier(hero, StatType.AS, ModifierType.Additive, attackSpeedBonus, duration, maxStacks, this);
}
