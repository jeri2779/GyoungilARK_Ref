using System.Collections.Generic;
using UnityEngine;

// 생산 시설 전반에 공통 적용되는 업그레이드 트랙(건설비 할인/업그레이드비 할인/생산량 보너스).
// 예전엔 ProductionFacility 프리팹마다 같은 SO 참조 3묶음을 그대로 복사해 직렬화했다(Wood/Stone 등 전부 동일 참조) -
// 어차피 같은 값이라 자산 하나로 모았다.
[CreateAssetMenu(fileName = "ProductionEconomyConfig", menuName = "Scriptable Objects/ProductionEconomyConfig")]
public class ProductionEconomyConfig : ScriptableObject
{
    [SerializeField] private List<BaseUpgradeData> constructCostUpgrades;
    [SerializeField] private List<BaseUpgradeData> upgradeCostUpgrades;
    [SerializeField] private List<BaseUpgradeData> productAmountUpgrades;

    public IReadOnlyList<BaseUpgradeData> ConstructCostUpgrades => constructCostUpgrades;
    public IReadOnlyList<BaseUpgradeData> UpgradeCostUpgrades => upgradeCostUpgrades;
    public IReadOnlyList<BaseUpgradeData> ProductAmountUpgrades => productAmountUpgrades;
}
