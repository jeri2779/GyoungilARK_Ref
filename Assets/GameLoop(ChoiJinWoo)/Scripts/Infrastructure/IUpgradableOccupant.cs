using System;

// RegionFacilitySlots에 지어질 수 있는 점유물(ProductionFacility/House) 중 "레벨업" 개념을
// 공유하는 부분만 뽑은 인터페이스. BuildingPanel/RegionDetailPanel이 둘을 구분 없이 다루게 한다.
public interface IUpgradableOccupant
{
    int UpgradeCount { get; }
    string NextUpgradeInfo { get; }
    int MaxUpgrade { get; }
    (ProductionType Type, int Amount)[] UpgradeCostCopy { get; }
    event Action Changed;
    bool CheckCanUpgrade();
    void Upgrade();
}
