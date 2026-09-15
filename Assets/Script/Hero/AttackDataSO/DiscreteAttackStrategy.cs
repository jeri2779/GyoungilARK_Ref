using System.Threading;
using Cysharp.Threading.Tasks;

// 기존(지금까지 유일했던) 방식 — executor.Execute 한 번 호출로 애니메이션 1회 재생 + 애니메이션 이벤트
// 윈도우 기반 타격 판정까지 끝난다. MeleeAttackExecutor/RangedAttackExecutor/HealAttackExecutor는
// 전부 이 전략으로만 호출된다. 실행 시간이 애니메이션 윈도우(길어야 1초 남짓) 안으로 짧아 ctx 스냅샷이
// 낡을 위험이 기존에도 없었으므로 hero는 쓰지 않는다.
public class DiscreteAttackStrategy : IAttackDeliveryStrategy
{
    public UniTask Deliver(Hero hero, AttackDataSO data, AttackContext ctx, IAttackExecutor executor, CancellationToken ct)
        => executor.Execute(data, ctx, ct);
}
