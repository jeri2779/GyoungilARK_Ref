using UnityEngine;
using UnityEngine.Playables;

public class HeroStateMachine
{
    public HeroState CurrentState { get; private set; }

    public void Initialize(HeroState startState)
    {
        CurrentState = startState;
        CurrentState.Enter();
    }

    public void ChangeState(HeroState newState)
    {
        CurrentState.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }
}
