using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using VContainer;
using VContainer.Unity;

[Serializable]
public struct ResourceIcon
{
    public ProductionType type;
    public Sprite icon;
}

public class HeroUpgradeMenuUI : MonoBehaviour
{
    private ClickOutsideCloser outsideCloser;
    [SerializeField] private List<Sprite> panelImageList;
    [SerializeField] private List<Sprite> upgradeIconList;

    [SerializeField] private Image banner;
    [SerializeField] private Image upgradeIcon;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private HeroUpgradeResourcesUI resourceInfoPrefab;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private List<ResourceIcon> resourceIcons;
    [SerializeField] private TextMeshProUGUI upgradeTierText;
    [SerializeField] private TextMeshProUGUI currentLevelText;

    private HeroTierUpgradeState upgradeState;
    private ResourcesManager resourcesManager;
    private int tier;
    private Dictionary<ProductionType, HeroUpgradeResourcesUI> resourceInfoMap = new();
    private Dictionary<ProductionType, Sprite> resourceIconMap = new();

    [Inject]
    private void Construct(HeroTierUpgradeState upgradeState, ResourcesManager resourcesManager)
    {
        this.upgradeState = upgradeState;
        this.resourcesManager = resourcesManager;
    }

    private void Awake()
    {
        foreach (var resourceIcon in resourceIcons)
        {
            resourceIconMap[resourceIcon.type] = resourceIcon.icon;
        }
        upgradeButton.onClick.AddListener(() => upgradeState.TryLevelUp(tier));
    }

    private void Start()
    {
        resourcesManager.ProductUpdate += RefreshUpgradeButton;
        upgradeState.LevelChanged += HandleLevelChanged;
        UpdateResourceInfo(upgradeState.GetLevel(tier));
        RefreshUpgradeButton();
    }

    private void OnDestroy()
    {
        resourcesManager.ProductUpdate -= RefreshUpgradeButton;
        upgradeState.LevelChanged -= HandleLevelChanged;
        upgradeButton.onClick.RemoveAllListeners();
    }

    // 이 UI가 담당하는 티어의 레벨이 바뀌면(레벨업·로드 복원 공통) 화면을 다시 그린다
    private void HandleLevelChanged(int changedTier)
    {
        if (changedTier != tier) return;
        UpdateResourceInfo(upgradeState.GetLevel(tier));
        RefreshUpgradeButton();
    }

    public void Set(int tier)
    {
        this.tier = tier;
        banner.sprite = panelImageList[tier - 1];
        upgradeIcon.sprite = upgradeIconList[tier - 1];
        upgradeTierText.text = $"Upgrade Tier {tier}";
    }

    private void RefreshUpgradeButton()
    {
        upgradeButton.interactable = upgradeState.CanLevelUp(tier)
            && resourcesManager.CheckResources(upgradeState.GetNextLevelCost(tier));
    }

    public void UpdateResourceInfo(int currentLevel)
    {
        var costs = upgradeState.GetCostForLevel(tier, currentLevel);
        currentLevelText.text = currentLevel >= upgradeState.MaxLevel - 1 ? "Max Level" : $"LV.{currentLevel + 1}";
        //foreach (var resourceInfo in resourceInfoMap.Values)
        //{
        //    resourceInfo.gameObject.SetActive(false);
        //}
        foreach (var cost in costs)
        {
            if (!resourceInfoMap.TryGetValue(cost.Type, out var resourceInfo))
            {
                resourceInfo = Instantiate(resourceInfoPrefab, resourcePanel.transform);
                //resourceInfo.SetIcon(ResourceIconProvider.GetIcon(cost.Type));
                resourceInfo.SetIcon(resourceIconMap[cost.Type]);
                resourceInfoMap[cost.Type] = resourceInfo;
            }
            resourceInfo.SetAmount(-cost.Amount);
            // resourceInfo.gameObject.SetActive(true);
        }
    }
}
