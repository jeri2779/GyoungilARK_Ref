using UnityEngine;
using UnityEngine.Playables;

public abstract class HeroState
{
    protected Hero hero;
    protected HeroStateMachine stateMachine;

    protected HeroState(Hero hero, HeroStateMachine stateMachine)
    {
        this.hero = hero;
        this.stateMachine = stateMachine;
    }

    public abstract void Enter();
    public abstract void Update();
    public abstract void Exit();
}
