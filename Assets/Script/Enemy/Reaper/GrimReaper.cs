using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GrimReaper : EnemyBase
{
    [Tooltip("2타(SubAttack) 클립의 원래 길이(초). 1타 길이는 EnemyBase의 attackClipLength에 넣는다 — " +
             "공격 한 번이 두 클립을 이어 재생하므로 배속 보정은 둘을 같이 봐야 공격 간격 안에 들어온다. " +
             "attackClipLength가 0이면(=배속 안 함) 이 값도 무시된다.")]
    [SerializeField, Min(0f)] private float subAttackClipLength = 0f;

    [Tooltip("1타 클립의 어느 지점에서 2타로 넘어가기 시작할지(0~1). 1타의 타격 프레임 바로 뒤로 잡는다 — " +
             "1이면 마무리 동작(자세 복귀)까지 다 재생한 뒤에 2타가 나가서 두 번 벤 게 아니라 끊어 보인다.")]
    [SerializeField, Range(0.1f, 1f)] private float subAttackStart = 0.7f;

    [Tooltip("1타→2타 블렌딩 시간(초). 두 모션이 이만큼 겹쳐 이어진다. 0이면 딱 잘려 튄다. " +
             "너무 길면 2타 시작 자세가 뭉개지므로 0.1 안팎에서 조절.")]
    [SerializeField, Min(0f)] private float subAttackBlend = 0.12f;

    // 배속 보정 기준. 2타는 1타가 subAttackStart 지점에 닿을 때 시작하므로 실제 재생 길이는
    // 1타 전체가 아니라 그 지점까지 + 2타 전체다. base가 0이면 "배속하지 않음" 뜻이라 그대로 0을 넘긴다
    // (안 그러면 attackClipLength를 비워 둔 프리팹이 2타 길이만으로 갑자기 배속되기 시작한다).
    protected override float AttackClipLength =>
        base.AttackClipLength <= 0f ? 0f : base.AttackClipLength * subAttackStart + subAttackClipLength;

    // 공격 한 번이 2타로 나간다 — 타수별 공격력 배율(합 1.0이라 총 피해는 평타 한 방과 같다).
    // 피해를 필드에 캐시하지 않는 이유: AttackPower는 일차 스케일링·버프로 계속 변하므로 때리는 순간에 곱해야 한다.
    private static readonly float[] HitMultipliers = { 0.7f, 0.3f };
    // 이번 공격의 몇 번째 타격인지. 배율을 await 사이에서 뒤집으면 Animation Event가 먼저/늦게 떠 있을 때
    // 엉뚱한 배율로 맞으므로, 값이 아니라 타수를 세서 때리는 쪽에서 고른다.
    private int _hitIndex;

    protected override async UniTask AttackWatchdog(CancellationToken token)
    {
        try
        {
            // Attack()이 .Forget()으로 부르지만 첫 await 전까진 동기 실행이라 어떤 Animation Event보다 먼저 초기화된다.
            _hitIndex = 0;
            // base의 WaitForAttackAnim을 쓰지 않는 이유: 그쪽은 클립 길이만큼 통째로 Delay라
            // 1타의 마무리 동작까지 다 끝난 뒤에야 2타로 넘어간다(= 끊겨 보인다).
            await WaitForStateProgress("Attack", subAttackStart, 5f, token);
            // 죽었으면 2타를 내보내지 않는다. 이 검사가 없으면 사망 애니가 통째로 날아간다 —
            // Die()가 SetTrigger("Die")로 시작한 전이가 아직 진행 중일 때(0.25초) 아래 CrossFade가
            // 그것을 덮어써 SubAttack으로 끌고 가고, Die 트리거는 이미 소비돼 다시 걸리지 않는다.
            // 그러면 WaitForDeathAnim이 5초 타임아웃까지 "Die 스테이트를 찾지 못함"으로 헛돈다.
            // (스킬 토큰 skillCts는 OnDisable에서야 취소되므로 사망만으로는 이 워치독이 안 끊긴다.)
            if (IsDead) return;
            // 트리거 대신 CrossFade — 전이를 컨트롤러에 그리지 않아도 되고, 겹치는 시간을 여기서 직접 정한다.
            // (트리거 방식은 전이가 언제 시작될지가 Exit Time에 묶여 이 시점 제어가 안 된다.)
            if (animator != null) animator.CrossFadeInFixedTime("SubAttack", subAttackBlend, 0);
            await WaitForStateProgress("SubAttack", 1f, 5f, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            // 아래 세 줄은 base.AttackWatchdog과 같게 유지한다 — 사망 시에도 그대로 돌지만
            // Die()가 speed를 이미 1로 돌려놨고, _move.Resume()은 Update의 _move.Tick(!IsDead && ...)이
            // 막아 주므로 무해하다. 여기만 IsDead로 갈라 두면 베이스가 버그처럼 보인다.
            if (animator != null) animator.speed = 1f; // 공격 배속 원복(전역 speed이므로 이동/사망 애니에 안 새게)
            _attacking = false; // 2타까지 끝난 뒤에야 스킬/다음 공격 허용
            _move.Resume();
        }
    }

    /// <summary>stateName 스테이트가 normalized(0~1) 지점을 지날 때까지 기다린다.
    /// 클립 길이를 재서 Delay하는 대신 매 프레임 재생 위치를 보므로, 전이·배속·클립 교체에 그대로 따라간다.
    /// timeout 안에 그 스테이트에 못 들어가면 경고를 남기고 빠져나온다(이벤트/전이 누락 시 영구 정지 방지).</summary>
    private async UniTask WaitForStateProgress(string stateName, float normalized, float timeout, CancellationToken token)
    {
        const int layer = 0;
        float elapsed = 0f;
        bool entered = false;
        while (animator != null)
        {
            // 죽으면 즉시 빠진다. 사망은 연출을 이기므로 2타를 기다릴 이유가 없고,
            // 여기서 계속 돌면 타임아웃 경고까지 겹쳐 나온다.
            if (IsDead) return;

            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.IsName(stateName))
            {
                entered = true;
                if (info.normalizedTime >= normalized) return;
            }
            // 타임아웃은 스테이트에 "들어가기까지"만 잰다 — 들어간 뒤엔 재생 위치가 알아서 끝에 닿는다.
            else if (!entered)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= timeout)
                {
                    Debug.LogWarning($"[{name}] Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", this);
                    return;
                }
            }
            else return; // 들어갔다가 다른 스테이트로 빠져나감(사망·기절 등) → 더 기다리지 않는다
            await UniTask.Yield(token);
        }
    }

    // Attack·SubAttack 두 클립의 타격 프레임에 각각 걸어 준다. 걸린 순서대로 배율이 적용된다.
    public override void AnimEvent_AttackHit()
    {
        if (IsDead) return;
        float mul = HitMultipliers[Mathf.Min(_hitIndex++, HitMultipliers.Length - 1)];
        GameObject target = FindAttackTarget();
        if (target != null && target.GetComponentInParent<IDamageAble>() is IDamageAble dmg)
        {
            EnemySoundAttack();
            dmg.TakeDamage(Mathf.RoundToInt(AttackPower * mul));
            string key ="DebuffBleed";
            EnemySoundManager.Play(key, at: transform.position);
        }
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("GhostDie", at: transform.position);
    }
}
