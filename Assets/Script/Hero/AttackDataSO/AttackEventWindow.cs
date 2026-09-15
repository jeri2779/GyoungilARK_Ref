using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 공격 애니메이션 하나에서 발생하는 타격 이벤트(hitEvent)를 몇 개든 순서대로 받아
// 종료 이벤트(endEvent)가 오거나 maxDuration이 지나면 닫히는 윈도우.
// 재구독을 반복하지 않고 한 번만 구독해 이벤트를 놓치는 레이스를 피한다.
// maxDuration은 endEvent가 클립에 없거나 빠져도 히어로가 영구히 멈추지 않도록 하는 안전장치(deadline)다.
public sealed class AttackEventWindow : System.IDisposable
{
    private readonly HeroAnimEvents animEvents;
    private readonly string hitEvent;
    private readonly string endEvent;
    private readonly System.Action hitHandler;
    private readonly System.Action endHandler;
    private readonly float deadline;
    private int hitPending;
    private bool ended;

    public AttackEventWindow(HeroAnimEvents animEvents, string hitEvent, string endEvent, float maxDuration)
    {
        this.animEvents = animEvents;
        this.hitEvent = hitEvent;
        this.endEvent = endEvent;
        deadline = Time.time + maxDuration;
        hitHandler = () => hitPending++;
        endHandler = () => ended = true;
        animEvents.Subscribe(hitEvent, hitHandler);
        animEvents.Subscribe(endEvent, endHandler);
    }

    // 대기 중인 타격 이벤트가 있으면 하나 소비하고 true.
    // 없고 종료 이벤트가 왔거나 deadline을 넘겼으면 false. (쌓인 타격을 종료보다 먼저 모두 소비)
    public async UniTask<bool> MoveNextHit(CancellationToken ct)
    {
        await UniTask.WaitUntil(() => hitPending > 0 || ended || Time.time >= deadline, cancellationToken: ct);
        if (hitPending > 0)
        {
            hitPending--;
            return true;
        }
        
        return false;
    }

    public void Dispose()
    {
        animEvents.Unsubscribe(hitEvent, hitHandler);
        animEvents.Unsubscribe(endEvent, endHandler);
    }

    // Melee/Ranged/HealAttackExecutor 3곳이 동일하게 복제하던 루프. "Attack"/"Recovery" 이벤트
    // 이름과 hits==0 최소 1회 보장 폴백(고AS에서 애니메이터가 Attack 이벤트를 유실하면 데미지가
    // 통째로 사라지는 문제)의 지식을 여기 한 곳에만 둔다. ct 취소 시 MoveNextHit이 던지므로
    // 폴백은 실행되지 않는다 — 기존과 동일한 취소 의미.
    public static async UniTask RunHits(HeroAnimEvents animEvents, float windowDuration,
        Func<CancellationToken, UniTask> onHit, CancellationToken ct)
    {
        using var window = new AttackEventWindow(animEvents, "Attack", "Recovery", windowDuration);
        int hits = 0;
        while (await window.MoveNextHit(ct)) { await onHit(ct); hits++; }
        if (hits == 0) await onHit(ct);
    }
}
