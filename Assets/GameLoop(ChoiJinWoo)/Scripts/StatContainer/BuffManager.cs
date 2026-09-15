using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public class BuffManager : ITickable
{
    private readonly List<ActiveBuff> activeBuffs = new();

    public void ApplyStackingModifier(IUnit target, StatType type, ModifierType modType, float value, float duration, int maxStacks, object source)
    {
        maxStacks = Mathf.Max(1, maxStacks);

        var existing = activeBuffs.Find(b =>
        b.Target == target &&
        b.StatType == type &&
        b.Source == source);

        if (existing == null)
        {
            var modifier = CreateAndApplyModifier(target, type, modType, value, duration, source);

            activeBuffs.Add(new ActiveBuff
            {
                Target = target,
                StatType = type,
                Modifiers = new List<Modifier> { modifier },
                Stacks = 1,
                RemainingTime = duration,
                Persistent = duration <= 0f,
                Source = source
            });
            return;
        }

        if (existing.Stacks < maxStacks)
        {
            var modifier = CreateAndApplyModifier(target, type, modType, value, duration, source);
            existing.Modifiers.Add(modifier);
            existing.Stacks++;
        }

        existing.RemainingTime = duration;
    }

    private static Modifier CreateAndApplyModifier(IUnit target, StatType type, ModifierType modType, float value, float duration, object source)
    {
        var modifier = new Modifier(modType, value, duration, StatLayer.Buff, source);
        target.Stats.AddModifier(type, modifier);
        return modifier;
    }

    public void Tick()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = activeBuffs[i];
            if (buff.Persistent) continue;
            buff.RemainingTime -= Time.deltaTime;
            if (buff.RemainingTime <= 0f)
            {
                foreach (var modifier in buff.Modifiers)
                    buff.Target.Stats.RemoveModifier(buff.StatType, modifier);
                activeBuffs.RemoveAt(i);
            }
        }
    }

    // Persistent 버프(장판형 아군 버프 등)의 명시적 해제 — ApplyStackingModifier(진입)의 반대짝(이탈).
    public void RemoveBuff(IUnit target, StatType type, object source)
    {
        int index = activeBuffs.FindIndex(b => b.Target == target && b.StatType == type && b.Source == source);
        if (index < 0) return;

        var buff = activeBuffs[index];
        foreach (var modifier in buff.Modifiers)
            buff.Target.Stats.RemoveModifier(buff.StatType, modifier);
        activeBuffs.RemoveAt(index);
    }

    public void RemoveAllBuffs(IUnit target)
    {
        activeBuffs.RemoveAll(b =>
        {
            if (b.Target == target)
            {
                foreach (var modifier in b.Modifiers)
                    target.Stats.RemoveModifier(b.StatType, modifier);
                return true;
            }
            return false;
        });
    }

    // 대상에게 특정 출처가 건 효과만 제거한다.
    public void RemoveBuffs(IUnit target, object source)
    {
        for (int buffIndex = activeBuffs.Count - 1; buffIndex >= 0; buffIndex--)
        {
            ActiveBuff buff = activeBuffs[buffIndex];
            if (buff.Target != target || buff.Source != source)
            {
                continue;
            }

            for (int modIndex = 0; modIndex < buff.Modifiers.Count; modIndex++)
            {
                target.Stats.RemoveModifier(buff.StatType, buff.Modifiers[modIndex]);
            }

            activeBuffs.RemoveAt(buffIndex);
        }
    }

    // StackingMaxEffectTrait처럼 "지금 스택이 최대치에 도달했는가"를 확인해야 하는 트레잇을 위한 조회.
    // 매칭되는 버프가 없으면 0.
    public int GetStacks(IUnit target, object source)
        => activeBuffs.Find(b => b.Target == target && b.Source == source)?.Stacks ?? 0;
}
