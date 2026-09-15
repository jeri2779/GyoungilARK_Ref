using Cysharp.Threading.Tasks;
using UnityEngine;

public class MageAttackState : HeroAttackState
{
    private Mage mage;
    private HeroAttackRunner runner;

    public MageAttackState(Mage mage, HeroStateMachine stateMachine)
        : base(mage, stateMachine)
    {
        this.mage = mage;
    }

    public override bool IsBusy => runner != null && runner.IsExecuting;

    public override void Enter()
    {
        base.Enter();
        runner ??= new HeroAttackRunner(mage, new RangedAttackExecutor());

        float interval = mage.SC[StatType.AS] > 0f ? 1f / mage.SC[StatType.AS] : 1f;
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
        runner.ExecuteNext(mage.Context, attackCts.Token).Forget();
    }
}
