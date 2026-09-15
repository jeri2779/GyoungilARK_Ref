using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

// 채널링형 지속 공격 — executor(근접/원거리/힐)를 호출하지 않고, 애니메이션 이벤트 윈도우 대신 자체
// tick 루프로 continuousDuration 동안 continuousTickInterval마다 피해를 적용한다. 매 틱 시작
// 전에 hero.Target으로 "지금도 유효한 타겟인가"를 다시 확인하고(넘겨받은 ctx는 struct 복사본이라
// Hero.CheckTargetStillInRange가 타겟을 null로 바꿔도 갱신되지 않는다), hero.Context로 매번 새
// 스냅샷을 떠서 데미지를 적용한다 — 그렇지 않으면 채널링 도중 타겟이 죽는 순간 NRE가 난다.
// data.continuousDelivery == Beam이면 attackCount개의 빔을 동시에 들고(TickBeams 참고 — SameTarget은
// 전부 같은 타겟, DifferentEnemies는 targetCount종에게 분배하고 죽은 슬롯은 사거리 내 다른 적으로
// 보충) 각자 즉시 데미지(data.beamEffectPrefab이 있으면 캐스터→타겟을 잇는 이펙트를 채널링 내내
// 스폰 시 1회 — 두 점 추적은 Hero.TrackLinkEndpoints로 등록해 이후 매 프레임 스스로 갱신), ProjectileVolley면 매 tick 실제 투사체를
// 발사하는 방식으로 갈린다(attackCount/targetCount로 매 tick 몇 발을 어디에 쏠지 결정 —
// RangedAttackExecutor.FireVolley와 동일한 타겟팅 규칙). SelfArea면 타겟 잠금 없이 매 tick
// AttackDamageUtil의 자기중심 AOE 분기(attackType=Area, targetMode=SameTarget)를 그대로 호출해
// ctx.self 주변을 때리므로 특정 적 하나가 죽어도 끊기지 않는다(제자리 회전 근접 채널링 용도) —
// data.beamEffectPrefab이 있으면 캐스터→타겟 라인이 아니라 자기 위치에 붙는 이펙트 하나만 채널링
// 내내 따라다닌다. continuousDuration>0이면 그 시간 동안 무조건 돌고, continuousDuration<=0("무제한")
// 이면 Beam/ProjectileVolley와 동일하게 "유효 대상 없으면 종료" 계약을 따른다 — 여기서는 자기 위치
// areaRange 안에 적이 하나도 없어지는 순간이 그 종료 조건이다.
public class ContinuousBeamStrategy : IAttackDeliveryStrategy
{
    public async UniTask Deliver(Hero hero, AttackDataSO data, AttackContext ctx, IAttackExecutor executor, CancellationToken ct)
    {
        // 캐스팅 포즈는 Trigger가 아니라 Bool 파라미터로 유지된다 — animTriggers[0]을 그 파라미터
        // 이름으로 재사용한다(채널링 한 번에 이름 하나만 필요하므로 Sequential/Random 선택은 불필요).
        string animParam = (data.animTriggers != null && data.animTriggers.Length > 0) ? data.animTriggers[0] : null;
        if (string.IsNullOrEmpty(animParam))
            Debug.LogError($"[ContinuousBeamStrategy] '{data.name}' 의 animTriggers가 비어 있습니다.");
        else
            ctx.anim.SetBool(animParam, true);

        bool isBeam = data.continuousDelivery == ContinuousDelivery.Beam;
        bool isSelfArea = data.continuousDelivery == ContinuousDelivery.SelfArea;
        // attackCount개 빔을 동시에 든다 — SameTarget이면 전부 hero.Target(데미지 배수), DifferentEnemies면
        // targetCount종에게 라운드로빈 분배(FireProjectileVolley와 동일 규칙). 슬롯 타겟은 DifferentEnemies
        // 쪽만 저장해두고 매 틱 생존 확인 — SameTarget은 hero.Target을 매번 그대로 읽어서 재타겟팅에도
        // 자연스럽게 따라간다(고정 저장하면 하나 죽지 않고 다른 적으로 바뀌는 경우 오작동함).
        List<(GameObject target, GameObject beamGo)> beamSlots = new();
        if (isBeam && data.beamEffectPrefab != null)
        {
            Transform muzzle = ctx.MuzzleOrSelf;
            foreach (GameObject t in ResolveBeamTargets(hero, data, ctx))
            {
                GameObject beamGo = hero.SpawnPersistentEffect(data.beamEffectPrefab, muzzle.position);
                Hero.TrackLinkEndpoints(beamGo, () => muzzle.position, BeamTargetProvider(hero, data, t, muzzle));
                beamSlots.Add((t, beamGo));
            }
        }

        // SelfArea 전용 — 캐스터→타겟 라인이 아니라 캐스터 위치에 붙는 단일 이펙트(회전 이펙트 등)이므로
        // 빔 슬롯처럼 여러 개/UpdateLinkEndpoints가 필요 없다. 매 틱 self 위치로만 갱신한다.
        GameObject selfEffectGo = isSelfArea && data.beamEffectPrefab != null
            ? hero.SpawnPersistentEffect(data.beamEffectPrefab, ctx.self.position)
            : null;

        try
        {
            float elapsed = 0f;
            while (data.continuousDuration <= 0f || elapsed < data.continuousDuration)
            {
                if (isBeam)
                {
                    if (!await TickBeams(hero, data, ctx, beamSlots, ct)) break; // 남은 빔 없음 — 채널링 종료
                }
                else if (isSelfArea)
                {
                    // 특정 적 하나의 생사와는 무관하게 계속 돈다(SameTarget처럼 잠긴 타겟이 없으므로).
                    // 다만 continuousDuration<=0("무제한")일 때는 Beam/ProjectileVolley와 동일한 계약 —
                    // "유효 대상이 없으면 종료" — 을 자기중심 AOE에 맞게 적용한다: 자기 위치 areaRange
                    // 안에 적이 하나도 없으면 여기서 끝낸다. duration>0(고정 시간)이면 이 체크 없이 끝까지 돈다.
                    if (data.continuousDuration <= 0f)
                    {
                        RangeShape aoeShape = AttackDamageUtil.ResolveAoeShape(data);
                        bool anyEnemyNearby = hero.GetObjectsInRange(ctx.self.position, data.areaRange, aoeShape, RangeQueryAffinity.Enemy, data.AreaUnattackableTarget).Count > 0;
                        if (!anyEnemyNearby) break;
                    }
                    if (selfEffectGo != null) selfEffectGo.transform.position = ctx.self.position;
                    await AttackDamageUtil.ApplyInstantDamage(data, hero.Context, ct, playSound: true);
                }
                else
                {
                    if (hero.Target == null) break; // 채널링 도중 타겟이 죽거나 벗어남 — 여기서 끊는다
                    await FireProjectileVolley(hero, data, ctx, ct);
                }

                // 공격속도(AS) 디버프/버프가 채널링 도중 걸리거나 풀릴 수 있으므로 매 tick마다
                // 다시 계산한다 — 기준(버프/디버프 적용 전) AS 대비 현재 AS 비율만큼 tick 간격을
                // 늘리거나 줄인다. 디버프/버프가 전혀 없으면 asScale=1이라 기획자가 세팅한
                // continuousTickInterval 값이 그대로 유지된다.
                float baseAS = ctx.sc.GetBaseValue(StatType.AS);
                float currentAS = ctx.sc[StatType.AS];
                float asScale = (baseAS > 0f && currentAS > 0f) ? baseAS / currentAS : 1f;
                float interval = Mathf.Max(0.05f, data.continuousTickInterval * asScale);

                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: ct);
                elapsed += interval;
            }
        }
        finally
        {
            // 정상 종료/타겟 소실(break)/취소(OperationCanceledException) 어떤 경로로 빠져나가도
            // Bool이 켜진 채로 남지 않도록 반드시 꺼준다.
            if (!string.IsNullOrEmpty(animParam)) ctx.anim.SetBool(animParam, false);
            hero.AimOverrideTarget = null;
            foreach (var slot in beamSlots)
                hero.DespawnEffect(data.beamEffectPrefab, slot.beamGo);
            if (selfEffectGo != null) hero.DespawnEffect(data.beamEffectPrefab, selfEffectGo);
        }
    }

    // beamSlots를 갱신: 무효해진 슬롯(DifferentEnemies에서 타겟이 사거리를 벗어나거나 죽음)은 소멸시켜
    // 리스트에서 제거하고, 빈 자리는 사거리 내 아직 안 붙잡힌 다른 적으로 다시 채운다(살아있는 슬롯은
    // 그대로 둬서 매 틱 재셔플되어 빔이 튀지 않게 함). 반환값 false면 사거리 안에 유효 타겟이 하나도
    // 없다는 뜻 — 호출자가 채널링 자체를 끝낸다.
    private async UniTask<bool> TickBeams(Hero hero, AttackDataSO data, AttackContext ctx, List<(GameObject target, GameObject beamGo)> slots, CancellationToken ct)
    {
        if (slots.Count == 0) return false;

        if (data.targetMode == TargetMode.SameTarget)
        {
            if (hero.Target == null) return false;
            // 끝점 갱신은 스폰 시 등록한 Track이 매 프레임 알아서 하므로 여기서 다시 부를 필요가 없다.
            if (!string.IsNullOrEmpty(data.attackSoundKey)) EnemySoundManager.Play(data.attackSoundKey, at: ctx.self.position);
            for (int i = 0; i < slots.Count; i++)
                await AttackDamageUtil.ApplyInstantDamage(data, hero.Context, ct);
            return true;
        }

        List<GameObject> candidates = hero.GetObjectsInRange(ctx.self.position, data.range, data.rangeShape, RangeQueryAffinity.TargetableEnemy);
        // 유효성 판정은 실제 사거리보다 한 칸 넓게 봐서, 타겟이 경계를 살짝 넘나들 때마다
        // 빔 인스턴스를 통째로 반납·재생성하지 않게 한다 — 반납·재생성마다 SpawnPersistentEffect가
        // Flash 파티클을 Clear+Play로 리셋해서, 경계 근처에서 판정이 흔들리면 빔이 안 보이는 것처럼 된다.
        HashSet<GameObject> stillValid = new(hero.GetObjectsInRange(ctx.self.position, data.range + 1, data.rangeShape, RangeQueryAffinity.TargetableEnemy));
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].target != null && stillValid.Contains(slots[i].target)) continue;
            hero.DespawnEffect(data.beamEffectPrefab, slots[i].beamGo);
            slots.RemoveAt(i);
        }

        int need = data.attackCount - slots.Count;
        if (need > 0)
        {
            HashSet<GameObject> held = new();
            foreach (var slot in slots) held.Add(slot.target);

            List<GameObject> fresh = new();
            foreach (GameObject c in candidates)
                if (!held.Contains(c)) fresh.Add(c);

            int distinctBudget = Mathf.Max(0, data.targetCount - held.Count);
            List<GameObject> pool = new(held);
            for (int i = 0; i < Mathf.Min(distinctBudget, fresh.Count); i++)
                pool.Add(fresh[i]);

            if (pool.Count > 0)
                for (int i = 0; i < need; i++)
                {
                    GameObject t = pool[i % pool.Count];
                    GameObject beamGo = hero.SpawnPersistentEffect(data.beamEffectPrefab, ctx.MuzzleOrSelf.position);
                    Hero.TrackLinkEndpoints(beamGo, () => ctx.MuzzleOrSelf.position, AttackDamageUtil.TrackingPosition(t));
                    slots.Add((t, beamGo));
                }
        }
        if (slots.Count == 0) return false;

        // 기존 슬롯은 스폰 시 등록한 Track이 매 프레임 알아서 끝점을 갱신하므로 여기서 다시 부를 필요가 없다.
        if (!string.IsNullOrEmpty(data.attackSoundKey)) EnemySoundManager.Play(data.attackSoundKey, at: ctx.self.position);
        foreach (var (target, _) in slots)
        {
            AttackContext slotCtx = hero.Context;
            slotCtx.target = target.transform;
            await AttackDamageUtil.ApplyInstantDamage(data, slotCtx, ct);
        }
        return true;
    }

    // 빔 슬롯의 "도착 지점" 제공자. SameTarget은 재타겟팅으로 hero.Target 자체가 바뀔 수 있어 매 프레임
    // 다시 읽고, DifferentEnemies는 슬롯이 고정한 특정 대상 하나를 TrackingPosition으로 계속 추적한다.
    private static Func<Vector3> BeamTargetProvider(Hero hero, AttackDataSO data, GameObject slotTarget, Transform muzzle)
    {
        if (data.targetMode == TargetMode.SameTarget)
            return () => hero.Target != null ? AttackDamageUtil.EffectPosition(hero.Target) : muzzle.position;
        return AttackDamageUtil.TrackingPosition(slotTarget);
    }

    // FireProjectileVolley와 동일한 타겟팅 규칙 — DifferentEnemies는 targetCount종의 적에게 attackCount개
    // 슬롯을 라운드로빈 분배(AttackTargetSelector), SameTarget은 잠긴 타겟에게 attackCount개 슬롯 전부.
    // 채널링 시작 시 1회만 호출 — 이후 생존 확인은 TickBeams가 담당.
    private static List<GameObject> ResolveBeamTargets(Hero hero, AttackDataSO data, AttackContext ctx)
    {
        if (data.targetMode == TargetMode.DifferentEnemies)
        {
            List<GameObject> enemies = hero.GetObjectsInRange(ctx.self.position, data.range, data.rangeShape, RangeQueryAffinity.TargetableEnemy);
            return AttackTargetSelector.SelectTargets(enemies, data.attackCount, data.targetCount);
        }
        if (hero.Target == null) return new List<GameObject>();
        List<GameObject> list = new(data.attackCount);
        for (int i = 0; i < data.attackCount; i++)
            list.Add(hero.Target);
        return list;
    }

    // RangedAttackExecutor.FireVolley와 동일한 타겟팅 규칙 — DifferentEnemies는 AttackTargetSelector로
    // 최대 targetCount종의 적에게 attackCount발을 분배(라운드로빈), SameTarget은 잠긴 타겟에게
    // attackCount발 전부. 데미지는 즉시 적용되지 않고 각 투사체가 도착했을 때 Projectile.Hit()이
    // 적용한다.
    private async UniTask FireProjectileVolley(Hero hero, AttackDataSO data, AttackContext ctx, CancellationToken ct)
    {
        List<GameObject> targets;
        if (data.targetMode == TargetMode.DifferentEnemies)
        {
            List<GameObject> enemies = hero.GetObjectsInRange(ctx.self.position, data.range, data.rangeShape, RangeQueryAffinity.TargetableEnemy);
            targets = AttackTargetSelector.SelectTargets(enemies, data.attackCount, data.targetCount);
        }
        else
        {
            if (hero.Target == null) return;
            targets = new List<GameObject>(data.attackCount);
            for (int i = 0; i < data.attackCount; i++)
                targets.Add(hero.Target);
        }
        if (targets.Count == 0) return; // 사거리 내 유효 타겟 없음 — 이번 tick은 스킵

        int damage = (int)(ctx.sc[StatType.ATK] * data.attackPer);
        IObjectPool<Projectile> pool = hero.GetProjectilePool(data.projectilePrefab);

        for (int i = 0; i < targets.Count; i++)
        {
            hero.AimOverrideTarget = targets[i].transform;
            FireOneProjectile(hero, data, ctx, pool, targets[i], damage);
            if (i < targets.Count - 1)
                await UniTask.Delay(TimeSpan.FromSeconds(data.shotInterval), cancellationToken: ct);
        }
    }

    private void FireOneProjectile(Hero hero, AttackDataSO data, AttackContext ctx, IObjectPool<Projectile> pool, GameObject target, int damage)
    {
        Projectile arrow = pool.Get();
        arrow.transform.SetPositionAndRotation(ctx.MuzzleOrSelf.position, ctx.MuzzleOrSelf.rotation);
        if (!string.IsNullOrEmpty(data.attackSoundKey)) EnemySoundManager.Play(data.attackSoundKey, at: ctx.MuzzleOrSelf.position);

        arrow.Launch(target.transform, damage, pool,
            ProjectileAoEConfig.From(data, hero, ctx.sc, ctx.buffManager, ctx.self.position));
    }
}
