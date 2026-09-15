using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 눈의 정령 — 시전 모션 뒤 사거리 안의 영웅 전원에게 빙결(Frost_Basic)을 건다.
///
/// 빙결은 StatDebuffSO라 BuffManager가 있어야 걸린다(지속시간·스택을 그쪽이 굴린다).
/// 그래서 DebuffApply.To가 아니라 EnemyBase.ApplyDebuffTo를 쓴다 — EnemyBase가 buffManager·gameManager를
/// 주입받아 들고 있어 인수 두 개면 되고, 영웅 쪽 주입 상태에 기대지 않는다(SpiderToxin의 독과 같은 경로).
/// </summary>
public class SpiritofSnow : UtilitySkillDataSO
{
    private string frostId = "Frost_Basic";
    private DebuffSO debuffSO;
    private string skillstate = "Skill";
    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.IsDead || owner.animator == null) return;
        if (owner.Board == null) return;

        // Resources.Load는 내부 캐시가 있어 매 시전 불러도 에셋을 다시 읽지 않는다.
        debuffSO = DebuffLoader.Get(frostId);
        if (debuffSO == null) return;

        // 시전 모션 동안 제자리에 세운다. 안 세우면 걸어가면서 시전 애니가 재생되고,
        // 모션이 끝난 시점의 위치가 시작 위치와 어긋난다.
        owner.MovementSuspended = true;
        try
        {
            owner.animator.SetTrigger(skillstate);
            await WaitForAnimationEnd(owner, skillstate, 3f, token);
            if (owner == null || owner.IsDead || owner.Board == null) return;   // 모션 대기 중 죽었으면 걸지 않는다

            // 사거리 판정은 반드시 모션이 끝난 뒤에 한다 — 대기 전에 좌표와 대상을 굳히면
            // 그 사이 영웅이 죽거나 새로 배치돼도 낡은 기준으로 걸린다.
            var hit = new HashSet<GameObject>();
            // range가 0이면 자기 칸만 잡혀 아무에게도 안 걸린다 — 표에서 빠뜨렸을 때의 하한(다른 스킬들과 같은 처리).
            int cellRange = range > 0f ? Mathf.RoundToInt(range) : 1;
            Vector2Int origin = owner.Board.WorldToCell(owner.transform.position);
            foreach (Tile tile in owner.Board.GetTiles(origin, cellRange))
            {
                if (tile.OccupantObject == null) continue;
                if (!IsHeroOccupant(tile)) continue;
                if (!hit.Add(tile.OccupantObject)) continue;   // 같은 영웅을 두 번 때리지 않는다

                if (tile.OccupantObject.GetComponentInParent<Hero>() is Hero target)
                    owner.ApplyDebuffTo(target, debuffSO);
            }
        }
        finally
        {
            // 취소(사망·디스폰·스턴)로 빠져나가도 반드시 푼다 — 안 풀면 그 적이 영영 멈춘 채로 남는다.
            if (owner != null) owner.MovementSuspended = false;
        }
    }
    private static bool IsHeroOccupant(Tile tile)
    {
        return tile.State.Occupant is OccupantKind.MeleeHero or OccupantKind.RangedHero;
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
                Debug.LogWarning($"SpiritofSnow: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", owner);
                return;
            }
            await UniTask.Yield(token);
        }
        if (owner == null) return;

        var info = anim.GetCurrentAnimatorStateInfo(layer);
        float wait = info.length / Mathf.Max(0.01f, anim.speed);
        await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
    }
}
