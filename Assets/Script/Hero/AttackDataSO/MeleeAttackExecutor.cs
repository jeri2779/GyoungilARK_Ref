using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Pool;

public class MeleeAttackExecutor : IAttackExecutor
{
    private readonly AnimTriggerPicker triggers = new(nameof(MeleeAttackExecutor));

    public async UniTask Execute(AttackDataSO data, AttackContext ctx, CancellationToken ct)
    {
        float interval = ctx.sc[StatType.AS] > 0f ? 1f / ctx.sc[StatType.AS] : 1f;
        AttackAnimSpeedUtil.SetSpeed(ctx.anim, AttackAnimSpeedUtil.ComputeScale(data, interval));

        ctx.anim.SetTrigger(triggers.Pick(data));

        if (data.groundZonePrefab != null && ctx.target != null)
            ctx.hero.SpawnGroundZone(data.groundZonePrefab, ctx.target.position);

        try
        {
            float windowDuration = AttackAnimSpeedUtil.ComputeWindowDuration(data, interval);
            await AttackEventWindow.RunHits(ctx.animEvents, windowDuration, async token =>
            {
                ctx.hero.SpawnEffect(data.attackEffect, ctx.self.position, ctx.self.rotation, data.attackEffectLifetime);
                FireVisualProjectile(data, ctx);
                await AttackDamageUtil.ApplyInstantDamage(data, ctx, token, playSound: true);
            }, ct);
        }
        finally
        {
            AttackAnimSpeedUtil.SetSpeed(ctx.anim, 1f);
        }
    }

    // 근접 공격 데이터에 projectilePrefab이 채워져 있으면(예: 투척형 근접 스킬) 데미지와는 별개로
    // 시전자→타겟으로 날아가는 순수 비주얼 투사체를 하나 띄운다. 데미지는 여전히 ApplyInstantDamage가
    // 즉시 처리한다 — RangedAttackExecutor의 Area+Line 분기(LaunchVisualOnly)와 동일한 절충.
    private void FireVisualProjectile(AttackDataSO data, AttackContext ctx)
    {
        if (data.projectilePrefab == null || ctx.target == null) return;

        IObjectPool<Projectile> pool = ctx.hero.GetProjectilePool(data.projectilePrefab);
        Projectile p = pool.Get();
        p.transform.SetPositionAndRotation(ctx.MuzzleOrSelf.position, ctx.MuzzleOrSelf.rotation);
        p.LaunchVisualOnly(AttackDamageUtil.EffectPosition(ctx.target), pool, ctx.hero);
    }
}
