using UnityEngine;

public abstract class HeroTrait : MonoBehaviour
{
    protected Hero hero;
    protected virtual void Awake() => hero = GetComponent<Hero>();

    public virtual void OnAttackPerformed(AttackDataSO data) { }
    public virtual void OnAttackResolved(AttackDataSO data) { }
    public virtual void OnHit(GameObject target, int amount, bool isCrit) { }
    public virtual void OnKill(GameObject target) { }
    public virtual void OnPassiveTick(float deltaTime) { }
    public virtual void OnDayStart() { }
}
