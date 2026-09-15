using Cysharp.Threading.Tasks;
using UnityEngine;

public class HealerAttackState : HeroAttackState
{
    private Healer healer;
    private HeroAttackRunner runner;

    public HealerAttackState(Healer healer, HeroStateMachine stateMachine)
        : base(healer, stateMachine)
    {
        this.healer = healer;
    }

    public override bool IsBusy => runner != null && runner.IsExecuting;

    public override void Enter()
    {
        base.Enter();
        runner ??= new HeroAttackRunner(healer, new HealAttackExecutor());

        float interval = healer.SC[StatType.AS] > 0f ? 1f / healer.SC[StatType.AS] : 1f;
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
        runner.ExecuteNext(healer.Context, attackCts.Token).Forget();
    }
}
