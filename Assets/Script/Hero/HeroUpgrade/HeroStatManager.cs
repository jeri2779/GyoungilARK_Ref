using System;
using System.Collections.Generic;

// 영웅 종류(HeroData)별로 "현재 업그레이드 상황(talent tree + 티어 + 클래스)이 반영된 기본 스탯"을
// 미리 계산해 static 딕셔너리에 캐싱해둔다. 개체(Hero)마다 반복 계산하지 않고 여기서 한 번만 계산해
// 같은 종류의 영웅들이 공유한다. 버프처럼 개체마다 달라야 하는 값은 여기서 다루지 않는다 —
// Hero가 이 값을 StatContainer의 baseValue로 받아서 쓰고, 그 위에 개체별 버프 Modifier를 쌓는 건
// 계속 Hero/BuffManager 몫이다.
public class HeroStatManager
{
    private static HeroStatManager instance;
    private static readonly Dictionary<HeroData, Dictionary<StatType, float>> cache = new();

    // Hero가 HeroTierUpgradeState/HeroClassUpgradeState를 직접 몰라도 되도록, 캐시가 무효화될 때마다
    // 여기서 한 번만 알려준다. Hero는 이 이벤트 하나만 구독해서 RefreshBaseStats()를 다시 부른다.
    public static event Action StatsChanged;

    private static readonly StatType[] TrackedStats =
        { StatType.HP, StatType.ATK, StatType.DEF, StatType.BLK, StatType.AS };

    private readonly HeroTierUpgradeState tierUpgradeState;
    private readonly HeroClassUpgradeState classUpgradeState;
    private readonly UpgradeState upgradeState;
    private readonly List<BaseUpgradeData> statUpgrades;

    public HeroStatManager(HeroTierUpgradeState tierUpgradeState, HeroClassUpgradeState classUpgradeState, UpgradeState upgradeState, List<BaseUpgradeData> statUpgrades)
    {
        this.tierUpgradeState = tierUpgradeState;
        this.classUpgradeState = classUpgradeState;
        this.upgradeState = upgradeState;
        this.statUpgrades = statUpgrades;
        instance = this;

        tierUpgradeState.LevelChanged += OnLevelChanged;
        classUpgradeState.LevelChanged += OnLevelChanged;
    }

    // 어느 티어/클래스가 바뀌었는지 추적하지 않고 통째로 지운다 — 업그레이드 구매는 드문 이벤트라
    // 재계산 비용이 문제되지 않는다.
    private void OnLevelChanged(int _)
    {
        cache.Clear();
        StatsChanged?.Invoke();
    }

    public static float GetStat(HeroData heroData, StatType type) => GetAll(heroData)[type];

    public static Dictionary<StatType, float> GetAll(HeroData heroData)
    {
        if (!cache.TryGetValue(heroData, out Dictionary<StatType, float> resolved))
        {
            resolved = instance.Compute(heroData);
            cache[heroData] = resolved;
        }
        return resolved;
    }

    private Dictionary<StatType, float> Compute(HeroData heroData)
    {
        Hero prefabHero = heroData.HeroPrefab.GetComponent<Hero>();
        StatDataSO statData = prefabHero.StatData;

        var stats = new Dictionary<StatType, Stat>
        {
            [StatType.HP] = new Stat(statData.maxHp),
            [StatType.ATK] = new Stat(statData.attackPower),
            [StatType.DEF] = new Stat(statData.defence),
            [StatType.BLK] = new Stat(statData.blockCount),
            [StatType.AS] = new Stat(statData.attackSpeed),
        };

        float talentBonus = upgradeState.GetTotalEffect(statUpgrades);
        if (talentBonus != 0f)
        {
            stats[StatType.ATK].AddModifier(new Modifier(ModifierType.Additive, talentBonus, 0f, StatLayer.Equip, this));
            stats[StatType.DEF].AddModifier(new Modifier(ModifierType.Additive, talentBonus, 0f, StatLayer.Equip, this));
        }

        ApplyGains(stats, tierUpgradeState.GetStatGains(heroData.Tier), tierUpgradeState.GetLevel(heroData.Tier));
        ApplyGains(stats, classUpgradeState.GetStatGains(heroData.HeroType), classUpgradeState.GetLevel(heroData.HeroType));

        var resolved = new Dictionary<StatType, float>();
        foreach (StatType type in TrackedStats)
            resolved[type] = stats[type].Value;
        return resolved;
    }

    private static void ApplyGains(Dictionary<StatType, Stat> stats, IReadOnlyList<HeroStatGain> gains, int extraLevels)
    {
        if (extraLevels <= 0) return;
        foreach (HeroStatGain gain in gains)
            stats[gain.statType].AddModifier(new Modifier(gain.modifierType, gain.amountPerLevel * extraLevels, 0f, StatLayer.Equip, gain));
    }
}
