using UnityEngine;

// BuffManager는 RemainingTime<=0이 되면 버프를 무조건 제거한다("무한 지속" 개념이 없음). 그래서
// 오라는 duration을 tickInterval의 2배로 주기적 재적용해 끊기지 않게 유지한다.
[DisallowMultipleComponent]
public class AuraTrait : HeroTrait
{
    public int range = 2;
    public RangeShape shape = RangeShape.Diamond;
    public StatType stat = StatType.ATK;
    public ModifierType modifierType = ModifierType.Additive;
    public float value = 0.1f;
    public float tickInterval = 1f;

    private float timer;

    public override void OnPassiveTick(float deltaTime)
    {
        timer += deltaTime;
        if (timer < tickInterval) return;
        timer = 0f;

        foreach (GameObject go in hero.GetObjectsInRange(hero.transform.position, range, shape, RangeQueryAffinity.Ally))
            if (go.GetComponent<Hero>() is Hero ally)
                hero.Buffs.ApplyStackingModifier(ally, stat, modifierType, value, tickInterval * 2f, 1, this);
    }
}
