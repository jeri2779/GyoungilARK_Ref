using System;
using System.Collections.Generic;

// 클래스별(근거리/원거리) 업그레이드 진행도(레벨)를 들고 있는 런타임 싱글턴.
// HeroTierUpgradeState와 동일하게, 레벨은 영웅 개체가 아니라 HeroType에 속하므로
// 영웅 GameObject가 파괴/재생성돼도(제거·재배치, 합성 등) 리셋되지 않는다.
public class HeroClassUpgradeState
{
    private readonly HeroClassUpgradeConfig config;
    private readonly ResourcesManager resourcesManager;
    private readonly UpgradeState upgradeState;
    private readonly Dictionary<int, int> levels = new();

    public event Action<int> LevelChanged; // 인자: heroType

    public int MaxLevel => config.maxLevel;

    // 강화된 클래스와 레벨을 그대로 열거한다 (세이브 전용 조회)
    public IReadOnlyDictionary<int, int> ClassLevels => levels;

    private float CostDiscount => upgradeState.GetTotalEffect(config.UpgradeCostUpgrades);

    public HeroClassUpgradeState(HeroClassUpgradeConfig config, ResourcesManager resourcesManager, UpgradeState upgradeState)
    {
        this.config = config;
        this.resourcesManager = resourcesManager;
        this.upgradeState = upgradeState;
    }

    public int GetLevel(int heroType) => levels.TryGetValue(heroType, out int lvl) ? lvl : 0;

    public bool CanLevelUp(int heroType)
    {
        var entry = config.GetEntry(heroType);
        return entry != null && GetLevel(heroType) < config.maxLevel - 1;
    }

    public (ProductionType Type, int Amount)[] GetNextLevelCost(int heroType)
        => GetCostForLevel(heroType, GetLevel(heroType));

    public (ProductionType Type, int Amount)[] GetCostForLevel(int heroType, int level)
        => config.GetEntry(heroType)?.GetCostForLevel(level).ApplyDiscount(CostDiscount) ?? Array.Empty<(ProductionType, int)>();

    public IReadOnlyList<HeroStatGain> GetStatGains(int heroType)
        => (IReadOnlyList<HeroStatGain>)config.GetEntry(heroType)?.statGains ?? Array.Empty<HeroStatGain>();

    public bool TryLevelUp(int heroType)
    {
        if (!CanLevelUp(heroType)) return false;
        var cost = GetNextLevelCost(heroType);
        if (!resourcesManager.CheckResources(cost)) return false;

        resourcesManager.ProductChanged(cost);
        levels[heroType] = GetLevel(heroType) + 1;
        LevelChanged?.Invoke(heroType);
        return true;
    }

    // 세이브 데이터로 특정 클래스의 강화 레벨을 비용 차감 없이 그대로 지정한다 (로드 복원 전용)
    public void RestoreLevel(int heroType, int level)
    {
        levels[heroType] = level;
        LevelChanged?.Invoke(heroType);
    }
}
