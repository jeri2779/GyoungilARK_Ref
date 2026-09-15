// 기반시설 UI에서 곧장 건물을 짓는 담당. 맵 배치가 없고(PlacementArea/MapBoard 불필요),
// ProductionFacility/House가 이제 POCO라 풀링/프리팹 인스턴스화도 필요 없다 - BuildableFacility의
// 설정 SO(facilityValue/houseConfig)로 곧장 만든다.
public class BaseConstructor
{
    private readonly ResourcesManager resourcesManager;
    private readonly CitizenManager citizenManager;
    private readonly FacilityManager facilityManager;
    private readonly UpgradeState upgradeState;
    private readonly ProductionEconomyConfig economyConfig;

    // 튜토리얼이 "집을 지었는지"만 골라 판정할 수 있도록(built is House) 건설 성공 시 결과물을 그대로 흘려보낸다.
    public event System.Action<object> Built;

    public BaseConstructor(
        ResourcesManager resourcesManager,
        CitizenManager citizenManager,
        FacilityManager facilityManager,
        UpgradeState upgradeState,
        ProductionEconomyConfig economyConfig)
    {
        this.resourcesManager = resourcesManager;
        this.citizenManager = citizenManager;
        this.facilityManager = facilityManager;
        this.upgradeState = upgradeState;
        this.economyConfig = economyConfig;
    }

    public bool CanBuild(BuildableFacility option)
    {
        switch (option.kind)
        {
            case OccupantKind.Resource:
                return option.facilityValue != null &&
                    resourcesManager.CheckResources(ProductionFacility.PreviewConstructCost(option.facilityValue, economyConfig, upgradeState));
            case OccupantKind.Building:
                return option.houseConfig != null &&
                    resourcesManager.CheckResources(House.PreviewConstructCost(option.houseConfig, economyConfig, upgradeState));
            default:
                return false;
        }
    }

    public bool TryBuild(BuildableFacility option, RegionFacilitySlots region, int slotIndex, out object built)
    {
        built = null;
        if (slotIndex < 0 || slotIndex >= region.Slots.Count || !region.Slots[slotIndex].IsEmpty) return false;
        if (!CanBuild(option)) return false;

        built = option.kind == OccupantKind.Resource ? BuildFacility(option) : BuildHouse(option);
        if (built == null) return false;

        region.TryAssign(slotIndex, built, option.icon);
        Built?.Invoke(built);
        return true;
    }

    private ProductionFacility BuildFacility(BuildableFacility option)
    {
        var facility = new ProductionFacility(option.facilityValue, economyConfig, resourcesManager, citizenManager, facilityManager, upgradeState);
        facility.Init();
        return facility;
    }

    private House BuildHouse(BuildableFacility option)
    {
        var house = new House(option.houseConfig, citizenManager, resourcesManager, upgradeState, economyConfig);
        house.Init();
        return house;
    }

    // 세이브 데이터로 기반시설을 자원 차감 없이 복원해 슬롯에 채운다 (로드 복원 전용)
    public object RestoreBuild(BuildableFacility option, RegionFacilitySlots region, BuildSave save)
    {
        object built = option.kind == OccupantKind.Resource
            ? RestoreFacility(option, save)
            : RestoreHouse(option, save);

        region.TryAssign(save.slotIndex, built, option.icon);
        return built;
    }

    private ProductionFacility RestoreFacility(BuildableFacility option, BuildSave save)
    {
        var facility = new ProductionFacility(option.facilityValue, economyConfig, resourcesManager, citizenManager, facilityManager, upgradeState);
        facility.RestoreState(save.upgradeCount, save.workerAmount, save.productAmount, save.maxWorker,
            save.amountUpgrade, save.citizenUpgrade, save.nextUpgradeInfo, ToPairs(save.constructPaid), ToPairs(save.upgradePaid));
        return facility;
    }

    private House RestoreHouse(BuildableFacility option, BuildSave save)
    {
        var house = new House(option.houseConfig, citizenManager, resourcesManager, upgradeState, economyConfig);
        house.RestoreState(save.upgradeCount, ToPairs(save.constructPaid), ToPairs(save.upgradePaid));
        return house;
    }

    // CostSave 배열을 ProductionFacility/House가 쓰는 튜플 배열로 바꾼다
    private static (ProductionType Type, int Amount)[] ToPairs(CostSave[] costs)
    {
        (ProductionType Type, int Amount)[] pairs = new (ProductionType, int)[costs.Length];
        for (int i = 0; i < costs.Length; i++)
        {
            pairs[i] = (costs[i].costType, costs[i].costAmount);
        }
        return pairs;
    }

    public void Demolish(RegionFacilitySlots region, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= region.Slots.Count) return;

        var occupant = region.Slots[slotIndex].Occupant;
        if (occupant == null) return;

        region.TryClear(slotIndex);

        if (occupant is ProductionFacility facility) facility.Release();
        else if (occupant is House house) house.Release();
    }
}
