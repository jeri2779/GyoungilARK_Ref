using UnityEngine;

// AttackDataSO.lifestealPercent로 기본 흡혈은 이미 커버된다. 이 트레잇은 특정 AttackDataSO와
// 무관하게 항상 붙는 "캐릭터 고유 흡혈"을 표현하고 싶을 때 별도로 추가한다.
[DisallowMultipleComponent]
public class LifestealTrait : HeroTrait
{
    [Range(0f, 1f)] public float percent = 0.1f;

    public override void OnHit(GameObject target, int amount, bool isCrit)
        => hero.Heal(amount * percent);
}
