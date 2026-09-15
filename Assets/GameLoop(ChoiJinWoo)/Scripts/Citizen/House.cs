using System;

// 맵 배치가 사라지면서 GameObject/Transform이 필요 없어져 일반 클래스로 전환했다.
// 예전 Start()/OnDestroy()가 하던 일을 Init()/Release()로 명시적으로 호출한다.
// 강화(Upgrade) 로직은 ProductionFacility.Upgrade()/CheckCanUpgrade()와 같은 패턴을 그대로 옮겨온 것.
public class House : IUpgradableOccupant
{
    private readonly HouseConfig config;
    private readonly CitizenManager citizenManager;
    private readonly ResourcesManager resourcesManager;
    private readonly UpgradeState upgradeState;
    private readonly ProductionEconomyConfig economyConfig;

    // 세이브 조회용 — 원본 설정 SO를 그대로 노출한다 (facilityValue의 BasicValue 노출과 대칭)
    public HouseConfig Config => config;

    private float ConstructCostDiscount => upgradeState.GetTotalEffect(economyConfig.ConstructCostUpgrades);
    private float UpgradeCostDiscount => upgradeState.GetTotalEffect(economyConfig.UpgradeCostUpgrades);

    private int maxUpgrade = 4;
    private int upgradeCount = 0;
    private (ProductionType Type, int Amount)[] upgradeCostCopy = Array.Empty<(ProductionType, int)>();
    private (ProductionType Type, int Amount)[] totalUpgradeSpent = Array.Empty<(ProductionType, int)>();
    private (ProductionType Type, int Amount)[] constructCostPaid = Array.Empty<(ProductionType, int)>();

    public string HouseName => config.HouseName;
    public string HouseInfo => config.HouseInfo;
    public (ProductionType Type, int Amount)[] Resources => config.Resources;
    public int UpgradeCount => upgradeCount;
    public (ProductionType Type, int Amount)[] UpgradeCostCopy => upgradeCostCopy;
    private string nextUpgradeInfo = "Ui_UpgradeInfo_MaxCitizen";
    public string NextUpgradeInfo => nextUpgradeInfo;

    public int MaxUpgrade => maxUpgrade;

    // 실제로 낸 건설·강화 비용을 그대로 읽는다 (세이브 전용 조회)
    public (ProductionType Type, int Amount)[] ConstructCostPaid => constructCostPaid;
    public (ProductionType Type, int Amount)[] TotalUpgradeSpent => totalUpgradeSpent;

    public event Action Changed;

    public House(
        HouseConfig config,
        CitizenManager citizenManager,
        ResourcesManager resourcesManager,
        UpgradeState upgradeState,
        ProductionEconomyConfig economyConfig)
    {
        this.config = config;
        this.citizenManager = citizenManager;
        this.resourcesManager = resourcesManager;
        this.upgradeState = upgradeState;
        this.economyConfig = economyConfig;
    }

    // 인스턴스를 만들지 않고도(건설 전 미리보기) 같은 할인 공식으로 건설 비용을 계산한다
    // (ProductionFacility.PreviewConstructCost와 동일 패턴).
    public static (ProductionType Type, int Amount)[] PreviewConstructCost(
        HouseConfig config, ProductionEconomyConfig economyConfig, UpgradeState upgradeState)
    {
        float discount = upgradeState.GetTotalEffect(economyConfig.ConstructCostUpgrades);
        return config.Resources.ApplyDiscount(discount);
    }

    public void Init()
    {
        citizenManager.IncreaseMaxCitizen(config.MaxCitizenAmount);
        constructCostPaid = Resources.ApplyDiscount(ConstructCostDiscount);
        resourcesManager.ProductChanged(constructCostPaid);

        upgradeCostCopy = config.UpgradeCost.ApplyDiscount(UpgradeCostDiscount);
        totalUpgradeSpent = new (ProductionType, int)[upgradeCostCopy.Length];
        for (int i = 0; i < totalUpgradeSpent.Length; i++)
        {
            totalUpgradeSpent[i] = (upgradeCostCopy[i].Type, 0);
        }
    }

    // 철거: 건설 비용 + 그동안 강화에 쓴 비용을 환불한다(ProductionFacility.Release()와 동일 패턴).
    public void Release()
    {
        resourcesManager.ProductChanged(constructCostPaid.BuildRefundWith(totalUpgradeSpent));
    }

    public void Upgrade()
    {
        upgradeCount++;
        citizenManager.IncreaseMaxCitizen(config.CitizenPerUpgrade);

        resourcesManager.ProductChanged(upgradeCostCopy);

        upgradeCostCopy.AccumulateInto(totalUpgradeSpent);

        var baseCost = config.UpgradeCost.ApplyDiscount(UpgradeCostDiscount);
        for (int i = 0; i < upgradeCostCopy.Length; i++)
        {
            upgradeCostCopy[i] = (baseCost[i].Type, baseCost[i].Amount * (upgradeCount + 1));
        }

        Changed?.Invoke();
    }

    public bool CheckCanUpgrade()
    {
        return upgradeCount < maxUpgrade && resourcesManager.CheckResources(upgradeCostCopy);
    }

    // 세이브 데이터로 건설·강화 상태를 자원 차감 없이 그대로 복원한다 (최대 시민은 정상 반영, 로드 복원 전용)
    public void RestoreState(
        int savedUpgradeCount,
        (ProductionType Type, int Amount)[] savedConstructPaid,
        (ProductionType Type, int Amount)[] savedUpgradeSpent)
    {
        upgradeCount = savedUpgradeCount;
        citizenManager.IncreaseMaxCitizen(config.MaxCitizenAmount + config.CitizenPerUpgrade * savedUpgradeCount);

        constructCostPaid = savedConstructPaid;
        totalUpgradeSpent = savedUpgradeSpent;

        var baseCost = config.UpgradeCost.ApplyDiscount(UpgradeCostDiscount);
        upgradeCostCopy = new (ProductionType, int)[baseCost.Length];
        for (int i = 0; i < upgradeCostCopy.Length; i++)
        {
            upgradeCostCopy[i] = (baseCost[i].Type, baseCost[i].Amount * (upgradeCount + 1));
        }
    }
}
