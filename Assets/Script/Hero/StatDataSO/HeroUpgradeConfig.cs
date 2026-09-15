using System;
using System.Collections.Generic;
using UnityEngine;

// 티어별 업그레이드 기준 비용/증가율/스탯 증가치를 설정하는 애셋. 모든 영웅이 하나를 공유하며,
// 레벨은 개체가 아니라 HeroTierUpgradeState가 티어 단위로 관리한다.
[CreateAssetMenu(fileName = "HeroUpgradeConfig", menuName = "HeroData/HeroUpgradeConfig")]
public class HeroUpgradeConfig : ScriptableObject
{
    public int maxLevel = 99;
    public List<HeroTierUpgradeEntry> tierEntries;

    [SerializeField] private List<BaseUpgradeData> upgradeCostUpgrades;
    public IReadOnlyList<BaseUpgradeData> UpgradeCostUpgrades => upgradeCostUpgrades;

    public HeroTierUpgradeEntry GetEntry(int tier) => tierEntries.Find(e => e.tier == tier);
}

[Serializable]
public class HeroTierUpgradeEntry
{
    public int tier;
    public List<ResourceCost> baseCost;
    [Tooltip("레벨당 비용 증가량(고정값). 예: 10 = 레벨당 +10")]
    public int costGrowthPerLevel = 10;
    [Tooltip("이 레벨 구간마다(예: 10) 비용 증가율이 milestoneGrowthMultiplier배로 커진다")]
    public int milestoneLevelInterval = 10;
    [Tooltip("milestoneLevelInterval 레벨에 도달할 때마다 costGrowthPerLevel에 곱해지는 배수")]
    public float milestoneGrowthMultiplier = 2f;
    public List<HeroStatGain> statGains;

    public (ProductionType Type, int Amount)[] GetCostForLevel(int currentLevel)
    {
        // 이미 보유한 레벨(1..currentLevel) 하나하나를 그 레벨이 속한 구간의 배율로 계산해 합산한다.
        // 과거 구간(이미 지난 N레벨 단위)은 재계산하지 않으므로 배율이 소급 적용되지 않는다.
        float growthAmount = 0f;
        for (int ownedLevel = 1; ownedLevel <= currentLevel; ownedLevel++)
        {
            int tier = milestoneLevelInterval > 0 ? ownedLevel / milestoneLevelInterval : 0;
            growthAmount += costGrowthPerLevel * Mathf.Pow(milestoneGrowthMultiplier, tier);
        }

        var result = new (ProductionType, int)[baseCost.Count];
        for (int i = 0; i < baseCost.Count; i++)
            result[i] = (baseCost[i].Type, -(baseCost[i].Amount + Mathf.RoundToInt(growthAmount)));
        return result;
    }
}

[Serializable]
public class HeroStatGain
{
    public StatType statType;
    public ModifierType modifierType = ModifierType.Flat;
    public float amountPerLevel;
}
