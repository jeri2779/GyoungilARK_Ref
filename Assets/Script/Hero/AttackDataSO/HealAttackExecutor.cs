using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class HealAttackExecutor : IAttackExecutor
{
    private readonly AnimTriggerPicker triggers = new(nameof(HealAttackExecutor));

    public async UniTask Execute(AttackDataSO data, AttackContext ctx, CancellationToken ct)
    {
        float interval = ctx.sc[StatType.AS] > 0f ? 1f / ctx.sc[StatType.AS] : 1f;
        AttackAnimSpeedUtil.SetSpeed(ctx.anim, AttackAnimSpeedUtil.ComputeScale(data, interval));

        ctx.anim.SetTrigger(triggers.Pick(data));

        if (data.groundZonePrefab != null)
            ctx.hero.SpawnGroundZone(data.groundZonePrefab, ctx.self.position);

        try
        {
            float windowDuration = AttackAnimSpeedUtil.ComputeWindowDuration(data, interval);
            await AttackEventWindow.RunHits(ctx.animEvents, windowDuration, async token =>
            {
                ctx.hero.SpawnEffect(data.attackEffect, ctx.self.position, data.attackEffectLifetime);
                if (!string.IsNullOrEmpty(data.attackSoundKey)) EnemySoundManager.Play(data.attackSoundKey, at: ctx.self.position);
                await AttackDamageUtil.ApplyInstantHeal(data, ctx, token);
            }, ct);
        }
        finally
        {
            AttackAnimSpeedUtil.SetSpeed(ctx.anim, 1f);
        }
    }
}
