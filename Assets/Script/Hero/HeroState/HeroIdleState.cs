using UnityEngine;

public class HeroIdleState : HeroState
{
    public HeroIdleState(Hero hero, HeroStateMachine stateMachine) : base(hero, stateMachine)
    {
    }

    public override void Enter()
    {
    }

    public override void Exit()
    {
    }

    public override void Update()
    {
        if (hero.Target != null)
        {
            stateMachine.ChangeState(hero.AttackState);
        }
    }
}
