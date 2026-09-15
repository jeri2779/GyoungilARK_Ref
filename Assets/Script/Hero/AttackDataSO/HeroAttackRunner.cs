using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public class HeroAttackRunner
{
    private readonly Hero hero;
    private readonly IAttackExecutor executor;
    private readonly IAttackDeliveryStrategy discrete = new DiscreteAttackStrategy();
    private readonly IAttackDeliveryStrategy continuous = new ContinuousBeamStrategy();
    private int hitCount;

    public bool IsExecuting { get; private set; }

    public HeroAttackRunner(Hero hero, IAttackExecutor executor)
    {
        this.hero = hero;
        this.executor = executor;
    }

    public async UniTask ExecuteNext(AttackContext ctx, CancellationToken ct)
    {
        if (IsExecuting) return;
        IsExecuting = true;
        try
        {
            AttackDataSO overrideData = hero.ConsumeAttackOverride(out bool isProc);
            AttackDataSO data = overrideData ?? hero.BasePattern[hitCount % hero.BasePattern.Count];
            if (overrideData == null) isProc = false;
            hitCount++;

            // 트레잇의 OnAttackPerformed가 QueueNextAttackOverride로 강공(N타)/재발동(proc)을 걸어둘 수
            // 있으므로, 훅 실행 직후에도 한 번 더 확인해 이번 공격에 즉시 반영한다.
            hero.NotifyAttackPerformed(data);
            AttackDataSO postHookOverride = hero.ConsumeAttackOverride(out bool postIsProc);
            if (postHookOverride != null) { data = postHookOverride; isProc = postIsProc; }

            // OnAttackPerformed는 강공 교체 여부를 "판단"하기 위해 교체 전 데이터로 불렸으므로,
            // 이번 스윙에 실제로 나가는 최종 데이터는 별도 훅으로 한 번 더 통지한다.
            hero.NotifyAttackResolved(data);
            hero.SetLastUsedAttack(data, isProc);

            IAttackDeliveryStrategy strategy = data.timingMode == AttackTimingMode.Continuous ? continuous : discrete;
            await strategy.Deliver(hero, data, ctx, executor, ct);
        }
        catch (OperationCanceledException)
        {
            // HeroSkillState 진입 등으로 HeroAttackState.Exit()이 attackCts를 취소하면 위 await 중
            // 하나가 여기로 던진다. ExecuteNext는 .Forget()으로 fire-and-forget되므로 안 잡으면
            // UniTask가 처리되지 않은 예외로 로그를 남긴다 — 정상적인 중단이므로 조용히 삼킨다.
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
