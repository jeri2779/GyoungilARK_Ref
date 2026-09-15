using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 범위 내 아군(자기 포함)에게 데미지 경감 쉴드를 부여. 큰 돔 비주얼을 duration 동안 표시.
// 필드 매핑(SkillDataSO): range=범위(칸), value=데미지 고정 감소 수치, duration=지속시간(초), cooldown=재시전 주기.
[CreateAssetMenu(menuName = "Data/Skill/ShieldSkill")]
public class ShieldSkillDataSO : UtilitySkillDataSO
{
    [Tooltip("돔 보호막 비주얼 프리팹(옵션). 없으면 효과만 적용.")]
    public GameObject domePrefab;
    [Tooltip("돔 크기 배율. 실제 지름 ≈ 2 × range × CellSize × 이 값 (프리팹이 단위 구(지름1)일 때 기준).")]
    public float domeScaleMul = 1f;
    public string shieldStateName = "Shield";
    public float animTimeout = 5f;

    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.IsDead) return;
        float flatReduce = Mathf.Max(0f, value); // value = 데미지 고정 감소 수치
        int cellRange = range > 0f ? Mathf.RoundToInt(range) : 1;
        Vector3 center = owner.transform.position;

        // 시전 시점에 범위 안에 있는 아군(자기 포함)에게 스냅샷으로 부여 — Heal 스킬과 동일 패턴.
        foreach (var ally in EnemyRegistry.Alive)
        {
            if (ally == null || ally.IsDead) continue;
            if (!EnemyTargeting.InRange(center, ally.transform.position, cellRange)) continue;
            ally.ApplyDamageReductionShield(flatReduce, duration);
        }

        if (domePrefab == null) { await UniTask.CompletedTask; return; }

        // 돔 비주얼: 시전자를 따라다니며 duration 동안 표시 후 제거.
        GameObject dome = PoolManager.Instance.Spawn(domePrefab, center, Quaternion.identity);
        if (owner.Board != null)
        {
            float diameter = 2f * cellRange * owner.Board.CellSize * Mathf.Max(0.01f, domeScaleMul);
            dome.transform.localScale = Vector3.one * diameter; // 보호 범위에 맞춰 스케일
        }
        if (owner != null) owner.MovementSuspended = true; // 시전 모션 동안만 정지
        try
        {
            if (owner.animator != null)
            {
                owner.animator.SetTrigger("Shield");
                await WaitForAnimationEnd(owner, shieldStateName, animTimeout, token);
            }

            if (owner != null) owner.MovementSuspended = false; // 시전 끝 → 지속시간 동안엔 정상 이동(걷기)

            float elapsed = 0f;
            while (elapsed < duration && owner != null && !owner.IsDead)
            {
                if (dome != null) dome.transform.position = owner.transform.position; // 부모 없이 위치만 추종(풀링 안전)
                elapsed += Time.deltaTime;
                await UniTask.Yield(token); // 파괴/비활성 시 취소돼 파괴된 오브젝트 접근 방지
            }
        }
        catch (OperationCanceledException) { /* 디스폰/사망으로 취소 — 정상 */ }
        finally
        {
            if (owner != null) owner.MovementSuspended = false; // 안전망: 타임아웃/취소로 빠져나가도 반드시 해제
            if (dome != null) PoolManager.Instance.Despawn(dome);
        }
    }
    private static async UniTask WaitForAnimationEnd(EnemyBase owner, string stateName, float timeout, CancellationToken token)
    {
        var anim = owner.animator;
        const int layer = 0;

        float elapsed = 0f;
        while (owner != null && !anim.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            if (owner.IsDead) return;
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"ShieldSkill: Animator에서 '{stateName}' 스테이트를 찾지 못함 — shieldStateName/전이 확인.", owner);
                return;
            }
            await UniTask.Yield(token); // 파괴/비활성 시 취소돼 파괴된 오브젝트 접근 방지
        }
        if (owner == null) return;

        var info = anim.GetCurrentAnimatorStateInfo(layer);
        float wait = info.length / Mathf.Max(0.01f, anim.speed);
        await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
    }
}
