using System.Collections.Generic;
using UnityEngine;

// 시뮬레이터의 계산 담당. 런타임 상태를 읽지 않고 넘겨받은 값만으로 결과를 만든다.
// 곱셈 순서는 새로 만들지 않고 런타임과 같은 Stat 클래스에 그대로 맡긴다.
public static class HeroSimCalc
{
    private const float PermanentDuration = 0f;
    private const float FullSwingRatio = 1f;
    private const float MinimumRemainRatio = 0f;
    private const int MinimumHitDamage = 1;

    private static readonly StatType[] TrackedStats =
        { StatType.HP, StatType.ATK, StatType.DEF, StatType.BLK, StatType.AS };

    // 기본 스탯에 타이틀·티어·직업 강화를 얹어 최종 스탯 5종을 만든다.
    public static Dictionary<StatType, float> CalculateStats(StatDataSO statData,
        IReadOnlyList<HeroStatGain> tierGains, int tierUpgradeCount,
        IReadOnlyList<HeroStatGain> classGains, int classUpgradeCount, float titleBonus)
    {
        var stats = new Dictionary<StatType, Stat>
        {
            [StatType.HP] = new Stat(statData.maxHp),
            [StatType.ATK] = new Stat(statData.attackPower),
            [StatType.DEF] = new Stat(statData.defence),
            [StatType.BLK] = new Stat(statData.blockCount),
            [StatType.AS] = new Stat(statData.attackSpeed),
        };

        AddTitleBonus(stats, titleBonus);
        AddUpgradeGains(stats, tierGains, tierUpgradeCount);
        AddUpgradeGains(stats, classGains, classUpgradeCount);
        return ResolveStats(stats);
    }

    // 강화 0단계(원본 StatDataSO)의 스탯 5종. 증가율 표기의 기준값으로 쓴다.
    public static Dictionary<StatType, float> BaselineStats(StatDataSO statData)
    {
        return new Dictionary<StatType, float>
        {
            [StatType.HP] = statData.maxHp,
            [StatType.ATK] = statData.attackPower,
            [StatType.DEF] = statData.defence,
            [StatType.BLK] = statData.blockCount,
            [StatType.AS] = statData.attackSpeed,
        };
    }

    // 타이틀 강화 합계를 ATK·DEF의 장착 계층에 얹는다. 런타임 HeroStatManager와 같은 자리다.
    private static void AddTitleBonus(Dictionary<StatType, Stat> stats, float titleBonus)
    {
        stats[StatType.ATK].AddModifier(new Modifier(ModifierType.Additive, titleBonus, PermanentDuration, StatLayer.Equip, TrackedStats));
        stats[StatType.DEF].AddModifier(new Modifier(ModifierType.Additive, titleBonus, PermanentDuration, StatLayer.Equip, TrackedStats));
    }

    // 강화 증가치를 레벨 수만큼 곱해 한 번에 얹는다.
    private static void AddUpgradeGains(Dictionary<StatType, Stat> stats, IReadOnlyList<HeroStatGain> gains, int upgradeCount)
    {
        // 강화 항목 개수는 티어·직업 저작에 따라 달라지는 동적 컬렉션이다.
        foreach (HeroStatGain gain in gains)
        {
            stats[gain.statType].AddModifier(new Modifier(gain.modifierType,
                gain.amountPerLevel * upgradeCount, PermanentDuration, StatLayer.Equip, gain));
        }
    }

    // Stat 통에서 최종 수치만 꺼내 담는다.
    private static Dictionary<StatType, float> ResolveStats(Dictionary<StatType, Stat> stats)
    {
        var resolved = new Dictionary<StatType, float>();
        for (int index = 0; index < TrackedStats.Length; index++)
        {
            resolved[TrackedStats[index]] = stats[TrackedStats[index]].Value;
        }
        return resolved;
    }

    // 해금한 타이틀 강화 칸 수만큼 effectAmount를 더한다.
    public static float CalculateTitleBonus(List<BaseUpgradeData> upgrades, int unlockCount)
    {
        float total = 0f;
        int usable = Mathf.Min(unlockCount, upgrades.Count);
        for (int index = 0; index < usable; index++)
        {
            total += upgrades[index].effectAmount;
        }
        return total;
    }

    // 확률형 트레잇들이 실제로 나오는 비율을 정리한다. 나중에 붙은 트레잇이 앞의 것을 덮어쓴다.
    public static List<HeroSimHeavyChance> ResolveHeavyRatios(HeroSimEntry entry)
    {
        var resolved = new List<HeroSimHeavyChance>();
        float laterShare = FullSwingRatio;

        for (int index = entry.HeavyChances.Count - 1; index >= 0; index--)
        {
            HeroSimHeavyChance raw = entry.HeavyChances[index];
            resolved.Add(new HeroSimHeavyChance
            {
                HeavyAttack = raw.HeavyAttack,
                SwingRatio = raw.SwingRatio * laterShare,
            });
            laterShare *= FullSwingRatio - raw.SwingRatio;
        }

        resolved.Reverse();
        ApplyRechance(entry, resolved);
        return resolved;
    }

    // 강공 재발동 트레잇이 있으면 같은 강공의 비율을 늘린다. 적 1마리 기준으로 한 번만 굴린다고 본다.
    private static void ApplyRechance(HeroSimEntry entry, List<HeroSimHeavyChance> resolved)
    {
        if (entry.RechanceAttack == null) return;

        float multiplier = RechanceMultiplier(entry.RechanceRecursive, entry.RechanceChance);
        float used = 0f;

        for (int index = 0; index < resolved.Count; index++)
        {
            if (resolved[index].HeavyAttack != entry.RechanceAttack) continue;
            resolved[index].SwingRatio *= multiplier;
        }

        for (int index = 0; index < resolved.Count; index++)
        {
            used += resolved[index].SwingRatio;
        }

        ClampToFullSwing(resolved, used);
    }

    // 재발동 배수. 재귀를 허용하면 등비합, 아니면 한 번만 더 나온다.
    private static float RechanceMultiplier(bool allowRecursive, float chance)
    {
        if (allowRecursive) return FullSwingRatio / (FullSwingRatio - chance);
        return FullSwingRatio + chance;
    }

    // 강공 비율 합계가 1을 넘으면 전체를 같은 비율로 줄여 평타 비율이 음수가 되지 않게 한다.
    private static void ClampToFullSwing(List<HeroSimHeavyChance> resolved, float used)
    {
        if (used <= FullSwingRatio) return;

        for (int index = 0; index < resolved.Count; index++)
        {
            resolved[index].SwingRatio /= used;
        }
    }

    // 공격 하나가 한 번 나갈 때 들어가는 총 피해. 런타임(EnemyBase.TakeDamage)과 같이
    // 한 발마다 정수로 자르고, 적 방어력을 뺀 뒤 최소 1은 보장한다.
    public static float CalculateHitDamage(AttackDataSO attack, float attackPower, int enemyDefense)
    {
        int damagePerShot = (int)(attackPower * attack.attackPer);
        int hitDamage = Mathf.Max(MinimumHitDamage, damagePerShot - enemyDefense);
        return hitDamage * attack.attackCount;
    }

    // 평타 로테이션 전체의 1타 평균 피해.
    public static float CalculateNormalHit(List<AttackDataSO> basePattern, float attackPower, int enemyDefense)
    {
        if (basePattern.Count == 0) return 0f;

        float total = 0f;
        for (int index = 0; index < basePattern.Count; index++)
        {
            total += CalculateHitDamage(basePattern[index], attackPower, enemyDefense);
        }
        return total / basePattern.Count;
    }

    // 평타와 강공이 섞여 나올 때의 평균 1타 피해.
    public static float CalculateAverageHit(List<AttackDataSO> basePattern,
        List<HeroSimHeavyChance> heavies, float attackPower, int enemyDefense)
    {
        float normalRatio = FullSwingRatio;
        float total = 0f;

        for (int index = 0; index < heavies.Count; index++)
        {
            if (heavies[index].HeavyAttack == null) continue;
            total += CalculateHitDamage(heavies[index].HeavyAttack, attackPower, enemyDefense) * heavies[index].SwingRatio;
            normalRatio -= heavies[index].SwingRatio;
        }

        normalRatio = Mathf.Max(MinimumRemainRatio, normalRatio);
        return total + CalculateNormalHit(basePattern, attackPower, enemyDefense) * normalRatio;
    }
}
