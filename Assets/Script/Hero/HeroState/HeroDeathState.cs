using UnityEngine;

public class HeroDeathState : HeroState
{
    public HeroDeathState(Hero hero, HeroStateMachine stateMachine) : base(hero, stateMachine)
    {
    }

    public override void Enter()
    {
        hero.Anim.SetTrigger(HeroAnimHash.death);
    }

    public override void Exit()
    {
        
    }

    public override void Update()
    {
        if (!hero.IsDead)
        {
            hero.Anim.SetBool(HeroAnimHash.idle, true);
            stateMachine.ChangeState(hero.IdleState);
        }
    }
}
