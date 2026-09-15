using System.Threading;
using Cysharp.Threading.Tasks;

// "결정된 AttackDataSO 한 방을 어떤 시간적 형태로 집행하는가"를 결정하는 전략.
// IAttackExecutor(근접/원거리/힐 — 무엇으로 때리는가)와는 독립된 축이다. HeroAttackRunner가
// AttackDataSO.timingMode로 이 전략을 고르고, 전략이 다시 executor를 호출(또는 직접 tick 처리)한다.
// hero를 함께 받는 이유: AttackContext는 struct라 HeroAttackRunner가 호출 시점에 넘긴 ctx는
// "그 순간의 스냅샷"으로 고정된다. Discrete는 애니메이션 윈도우(~1초 내)만 쓰므로 기존에도 문제
// 없었지만, Continuous는 몇 초씩 도는 도중 타겟이 죽거나 벗어나면 Hero.CheckTargetStillInRange가
// hero.Context.target을 null로 바꾼다 — 그 변경은 Hero 쪽 필드에만 반영되고, 이미 넘겨받은 ctx
// 복사본은 갱신되지 않는다. hero를 직접 들고 있어야 매 틱 "지금 살아있는" 타겟을 다시 확인할 수 있다.
public interface IAttackDeliveryStrategy
{
    UniTask Deliver(Hero hero, AttackDataSO data, AttackContext ctx, IAttackExecutor executor, CancellationToken ct);
}
