using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using VContainer;

public class HeroClassUpgradeMenuUI : MonoBehaviour
{
    [SerializeField] private List<Sprite> panelImageList; // index = heroType (0=근거리, 1=원거리)
    [SerializeField] private List<Sprite> upgradeIconList; // index = heroType

    [SerializeField] private Image banner;
    [SerializeField] private Image upgradeIcon;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private HeroUpgradeResourcesUI resourceInfoPrefab;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private List<ResourceIcon> resourceIcons;
    [SerializeField] private LocalizeText upgradeTierText;
    [SerializeField] private TextMeshProUGUI currentLevelText;

    private HeroClassUpgradeState upgradeState;
    private ResourcesManager resourcesManager;
    private int heroType;
    private Dictionary<ProductionType, HeroUpgradeResourcesUI> resourceInfoMap = new();
    private Dictionary<ProductionType, Sprite> resourceIconMap = new();

    [Inject]
    private void Construct(HeroClassUpgradeState upgradeState, ResourcesManager resourcesManager)
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
        upgradeButton.onClick.AddListener(() => upgradeState.TryLevelUp(heroType));
    }

    private void Start()
    {
        resourcesManager.ProductUpdate += RefreshUpgradeButton;
        upgradeState.LevelChanged += HandleLevelChanged;
        UpdateResourceInfo(upgradeState.GetLevel(heroType));
        RefreshUpgradeButton();
    }

    private void OnDestroy()
    {
        resourcesManager.ProductUpdate -= RefreshUpgradeButton;
        upgradeState.LevelChanged -= HandleLevelChanged;
        upgradeButton.onClick.RemoveAllListeners();
    }

    // 이 UI가 담당하는 클래스의 레벨이 바뀌면(레벨업·로드 복원 공통) 화면을 다시 그린다
    private void HandleLevelChanged(int changedHeroType)
    {
        if (changedHeroType != heroType) return;
        UpdateResourceInfo(upgradeState.GetLevel(heroType));
        RefreshUpgradeButton();
    }

    public void Set(int heroType)
    {
        this.heroType = heroType;
        banner.sprite = panelImageList[heroType];
        upgradeIcon.sprite = upgradeIconList[heroType];
        //
        upgradeTierText.SetKey(heroType == 0 ? "Hero_Melee_Upgrade" : "Hero_Ranged_Upgrade");
    }

    private void RefreshUpgradeButton()
    {
        upgradeButton.interactable = upgradeState.CanLevelUp(heroType)
            && resourcesManager.CheckResources(upgradeState.GetNextLevelCost(heroType));
    }

    public void UpdateResourceInfo(int currentLevel)
    {
        var costs = upgradeState.GetCostForLevel(heroType, currentLevel);
        currentLevelText.text = currentLevel >= upgradeState.MaxLevel - 1 ? "Max Level" : $"LV.{currentLevel + 1}";
        foreach (var cost in costs)
        {
            if (!resourceInfoMap.TryGetValue(cost.Type, out var resourceInfo))
            {
                resourceInfo = Instantiate(resourceInfoPrefab, resourcePanel.transform);
                resourceInfo.SetIcon(resourceIconMap[cost.Type]);
                resourceInfoMap[cost.Type] = resourceInfo;
            }
            resourceInfo.SetAmount(-cost.Amount);
        }
    }
}
