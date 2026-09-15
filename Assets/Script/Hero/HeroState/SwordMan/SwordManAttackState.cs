using Cysharp.Threading.Tasks;
using UnityEngine;

public class SwordManAttackState : HeroAttackState
{
    private SwordMan swordMan;
    private HeroAttackRunner runner;

    public SwordManAttackState(SwordMan swordMan, HeroStateMachine stateMachine)
        : base(swordMan, stateMachine)
    {
        this.swordMan = swordMan;
    }

    public override bool IsBusy => runner != null && runner.IsExecuting;

    public override void Enter()
    {
        base.Enter();
        runner ??= new HeroAttackRunner(swordMan, new MeleeAttackExecutor());

        float interval = swordMan.SC[StatType.AS] > 0f ? 1f / swordMan.SC[StatType.AS] : 1f;
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
        runner.ExecuteNext(swordMan.Context, attackCts.Token).Forget();
    }
}
