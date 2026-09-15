using System.Collections.Generic;
using System.Text;

// 원본 데이터와 입력 조건을 받아 표 한 줄(HeroSimResult)을 조립하는 담당.
// 스탯·피해·비용 계산은 HeroSimCalc에 맡기고 여기서는 값을 모아 담기만 한다.
public static class HeroSimBuilder
{
    private const string TraitSuffix = "Trait";
    private const string NoTraitLabel = "없음";
    private const string UnmodeledMark = "⚠";
    private const float PercentScale = 100f;
    private const float BaselineZero = 0f;
    private const string ZeroGrowthLabel = "0%";
    private const string FixedGrowthFormat = "+{0:N1}(고정)";
    private const string PercentGrowthFormat = "+{0:N0}%";
    private const string GrowthRateSeparator = " · ";
    private const string PercentPerLevelFormat = "{0:N0}%/lv";
    private const string FlatPerLevelFormat = "+{0:N1}/lv";
    private const string NoRateLabel = "-/lv";
    private const string NoEnemySelectedLabel = "-";
    private const string TimeToKillFormat = "{0:N2}초";
    private const float ZeroDamagePerSecond = 0f;

    private static readonly List<HeroStatGain> EmptyGains = new();

    // 영웅 한 명의 표 한 줄을 만든다. enemyDefense는 상대 적을 골랐을 때만 0보다 크다.
    public static HeroSimResult BuildResult(HeroSimEntry entry, HeroUpgradeConfig tierConfig,
        HeroClassUpgradeConfig classConfig, HeroSimInput input, float titleBonus, int enemyDefense, float enemyHp)
    {
        HeroTierUpgradeEntry tierEntry = tierConfig.GetEntry(entry.HeroData.Tier);
        HeroClassUpgradeEntry classEntry = classConfig.GetEntry(entry.HeroData.HeroType);
        IReadOnlyList<HeroStatGain> classGains = ClassGainsOf(classEntry);

        Dictionary<StatType, float> stats = HeroSimCalc.CalculateStats(entry.StatData,
            TierGainsOf(tierEntry), input.TierUpgradeCount,
            classGains, input.ClassUpgradeCount, titleBonus);
        Dictionary<StatType, float> baseline = HeroSimCalc.BaselineStats(entry.StatData);

        List<HeroSimHeavyChance> heavies = HeroSimCalc.ResolveHeavyRatios(entry);
        float attackPower = stats[StatType.ATK];
        float average = HeroSimCalc.CalculateAverageHit(entry.BasePattern, heavies, attackPower, enemyDefense);
        float damagePerSecond = average * stats[StatType.AS];

        return new HeroSimResult
        {
            Icon = entry.HeroData.Icon,
            HeroName = entry.HeroData.HeroName,
            Tier = entry.HeroData.Tier,
            MaxHp = stats[StatType.HP],
            AttackPower = attackPower,
            Defence = stats[StatType.DEF],
            AttackSpeed = stats[StatType.AS],
            BlockCount = stats[StatType.BLK],
            HpGrowth = BuildGrowthCell(stats[StatType.HP], baseline[StatType.HP], classGains, StatType.HP),
            AttackGrowth = BuildGrowthCell(stats[StatType.ATK], baseline[StatType.ATK], classGains, StatType.ATK),
            DefenceGrowth = BuildGrowthCell(stats[StatType.DEF], baseline[StatType.DEF], classGains, StatType.DEF),
            AttackSpeedGrowth = BuildGrowthCell(stats[StatType.AS], baseline[StatType.AS], classGains, StatType.AS),
            NormalHitDamage = HeroSimCalc.CalculateNormalHit(entry.BasePattern, attackPower, enemyDefense),
            AverageHitDamage = average,
            DamagePerSecond = damagePerSecond,
            CumulativeCost = SumUpgradeCost(tierEntry, classEntry, input),
            TraitNote = BuildTraitNote(entry, heavies),
            TimeToKill = BuildTimeToKillText(enemyHp, damagePerSecond),
        };
    }

    // 상대 적의 체력을 초당 피해로 나눠 처치 시간을 만든다. 적을 안 골랐으면 "-"를 준다.
    private static string BuildTimeToKillText(float enemyHp, float damagePerSecond)
    {
        if (enemyHp <= BaselineZero) return NoEnemySelectedLabel;
        if (damagePerSecond <= ZeroDamagePerSecond) return NoEnemySelectedLabel;

        return string.Format(TimeToKillFormat, enemyHp / damagePerSecond);
    }

    // 강화 0단계 대비 지금 스탯 증가율에, 직업 강화 레벨당 증가치를 이어 붙인다.
    private static string BuildGrowthCell(float current, float baseline, IReadOnlyList<HeroStatGain> classGains, StatType statType)
    {
        return BuildGrowthText(current, baseline) + GrowthRateSeparator + ClassRateText(classGains, statType);
    }

    // 강화 0단계 대비 지금 스탯이 몇 % 늘었는지 문구로 만든다. 기준값이 0이면 %를 못 구하니 증가분만 적는다.
    private static string BuildGrowthText(float current, float baseline)
    {
        if (baseline == BaselineZero && current == BaselineZero) return ZeroGrowthLabel;
        if (baseline == BaselineZero) return string.Format(FixedGrowthFormat, current);

        float percent = (current - baseline) / baseline * PercentScale;
        return string.Format(PercentGrowthFormat, percent);
    }

    // 직업 강화표에서 이 스탯의 레벨당 증가치를 찾는다. Additive면 %로, 그 외엔 원래 수치 그대로 적는다.
    private static string ClassRateText(IReadOnlyList<HeroStatGain> classGains, StatType statType)
    {
        for (int index = 0; index < classGains.Count; index++)
        {
            HeroStatGain gain = classGains[index];
            if (gain.statType != statType) continue;
            if (gain.modifierType == ModifierType.Additive) return string.Format(PercentPerLevelFormat, gain.amountPerLevel * PercentScale);
            return string.Format(FlatPerLevelFormat, gain.amountPerLevel);
        }
        return NoRateLabel;
    }

    // 티어 강화와 직업 강화에 지금까지 들어간 자원 총합.
    private static int SumUpgradeCost(HeroTierUpgradeEntry tierEntry, HeroClassUpgradeEntry classEntry, HeroSimInput input)
    {
        int total = 0;
        if (tierEntry != null)
            total += HeroSimCostCalc.CalculateCumulativeCost(tierEntry.GetCostForLevel, input.TierUpgradeCount);
        if (classEntry != null)
            total += HeroSimCostCalc.CalculateCumulativeCost(classEntry.GetCostForLevel, input.ClassUpgradeCount);
        return total;
    }

    // 티어 강화 증가치 목록. 설정이 없는 티어면 빈 목록을 준다.
    private static IReadOnlyList<HeroStatGain> TierGainsOf(HeroTierUpgradeEntry tierEntry)
    {
        if (tierEntry == null) return EmptyGains;
        return tierEntry.statGains;
    }

    // 직업 강화 증가치 목록. 설정이 없는 직업이면 빈 목록을 준다.
    private static IReadOnlyList<HeroStatGain> ClassGainsOf(HeroClassUpgradeEntry classEntry)
    {
        if (classEntry == null) return EmptyGains;
        return classEntry.statGains;
    }

    // 트레잇 요약 문구. 계산에 넣은 강공 비율과 넣지 못한 트레잇을 함께 적는다.
    private static string BuildTraitNote(HeroSimEntry entry, List<HeroSimHeavyChance> heavies)
    {
        float heavyRatio = 0f;
        for (int index = 0; index < heavies.Count; index++)
        {
            heavyRatio += heavies[index].SwingRatio;
        }

        var note = new StringBuilder();
        if (heavyRatio > 0f) note.Append($"강공 {heavyRatio * PercentScale:N0}%");

        for (int index = 0; index < entry.UnmodeledTraitNames.Count; index++)
        {
            if (note.Length > 0) note.Append(' ');
            note.Append(UnmodeledMark);
            note.Append(entry.UnmodeledTraitNames[index].Replace(TraitSuffix, string.Empty));
        }

        if (note.Length == 0) return NoTraitLabel;
        return note.ToString();
    }
}
