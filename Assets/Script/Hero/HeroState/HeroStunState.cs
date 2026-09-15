using UnityEngine;

public class HeroStunState : HeroState
{
    public HeroStunState(Hero hero, HeroStateMachine stateMachine) : base(hero, stateMachine)
    {
    }

    public override void Enter()
    {
        hero.Anim.SetBool(HeroAnimHash.stun, true);
    }

    public override void Exit()
    {
        hero.Anim.SetBool(HeroAnimHash.stun, false);
    }

    public override void Update()
    {
        if (!hero.IsStunned)
            stateMachine.ChangeState(hero.Target != null ? hero.AttackState : hero.IdleState);
    }
}
