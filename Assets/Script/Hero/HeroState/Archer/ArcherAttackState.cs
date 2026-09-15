using Cysharp.Threading.Tasks;
using UnityEngine;

public class ArcherAttackState : HeroAttackState
{
    private Archer archer;
    private HeroAttackRunner runner;

    public ArcherAttackState(Archer archer, HeroStateMachine stateMachine)
        : base(archer, stateMachine)
    {
        this.archer = archer;
    }

    public override bool IsBusy => runner != null && runner.IsExecuting;

    public override void Enter()
    {
        base.Enter();
        runner ??= new HeroAttackRunner(archer, new RangedAttackExecutor());

        float interval = archer.SC[StatType.AS] > 0f ? 1f / archer.SC[StatType.AS] : 1f;
        float elapsed = Time.time - lastAttackTime;
        bool skipAsGate = hero.LastUsedAttackData != null
            && hero.LastUsedAttackData.timingMode == AttackTimingMode.Continuous;
        if (elapsed >= interval || skipAsGate)
        {
            RotateToTarget();
            TryExecuteCurrentStep();
        }
        else
            timer = elapsed;
    }

    protected override void TryExecuteCurrentStep()
    {
        if (runner.IsExecuting) return;
        lastAttackTime = Time.time;
        runner.ExecuteNext(archer.Context, attackCts.Token).Forget();
    }
}
