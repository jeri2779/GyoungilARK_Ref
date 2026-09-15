using UnityEngine;

public class Mage : Hero
{
    [SerializeField] private Transform muzzle;
    public Transform Muzzle => muzzle;

    protected override void Awake()
    {
        base.Awake();

        attackState = new MageAttackState(this, stateMachine);
        context = new AttackContext
        {
            self = transform,
            target = null,
            anim = Anim,
            animEvents = AnimEvents,
            muzzle = muzzle,
            buffManager = buffManager,
            sc = SC,
            hero = this
        };
        occupantKind = OccupantKind.RangedHero;
    }
}
