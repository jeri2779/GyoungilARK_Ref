using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

// 빈 슬롯에 지을 건물 종류를 고르는 팝업. 생산 시설/집은 더 이상 맵 팔레트(PlacePalette)를 안 거치고
// 여기서 직접 BuildableFacility 목록(ProductionValue/HouseConfig 참조)으로 관리한다.
public class FacilityBuildChoicePanel : MonoBehaviour, IClosablePanel
{
    [SerializeField] private List<BuildableFacility> options;
    [SerializeField] private List<BuildOptionView> optionViews; // options와 인덱스가 대응
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private GameObject productionRow; // "생산 자원" 캡션 + productionIcon을 묶은 행 - House는 생산 자원이 없어 통째로 숨긴다
    [SerializeField] private Image productionIcon; // 생산 자원 아이콘
    [SerializeField] private List<CostAmountView> constructCostRows; // 최대 개수만큼 미리 배치, 남는 칸은 자동으로 숨김 (BuildingPanel.upgradeCostRows와 동일 패턴)
    [SerializeField] private RegionDetailPanel parentPanel; // 이 패널을 여는 쪽 - 그 안의 슬롯 버튼 클릭은 "바깥 클릭"이 아니다
    [SerializeField] private RectTransform buildButtonRect; // "건설" 버튼 - 튜토리얼 스포트라이트용 참조

    public RectTransform BuildButtonRect => buildButtonRect;

    private BaseConstructor constructor;
    private ResourcesManager resourcesManager;
    private UpgradeState upgradeState;
    private ProductionEconomyConfig economyConfig;
    private ResourceIconSet resourceIconSet;
    private UiPanelStack panelStack;
    private RegionFacilitySlots region;
    private int slotIndex;
    public int SlotIndex => slotIndex;
    private BuildableFacility currentOption;
    private int selectedIndex = -1;
    public int SelectedIndex => selectedIndex;
    public bool IsInfoOpen => infoPanel.activeSelf;

    // FacilitySlotHeldLink/BuildOptionHeldLink가 SlotIndex/SelectedIndex/IsInfoOpen을 매 프레임 폴링하는
    // 대신 구독할 수 있도록, 이 값들이 바뀌는 지점(OnEnable/OnDisable/Open/OnOption)에서 발화한다.
    public event System.Action StateChanged;

    private ClickOutsideCloser outsideCloser;
    private ClickOutsideCloser infoOutsideCloser;
    private PanelReveal panelReveal;
    private PanelReveal infoPanelReveal;

    [Inject]
    private void Construct(BaseConstructor constructor, ResourcesManager resourcesManager, UpgradeState upgradeState, ProductionEconomyConfig economyConfig, ResourceIconSet resourceIconSet, UiPanelStack panelStack)
    {
        this.constructor = constructor;
        this.resourcesManager = resourcesManager;
        this.upgradeState = upgradeState;
        this.economyConfig = economyConfig;
        this.resourceIconSet = resourceIconSet;
        this.panelStack = panelStack;
    }

    private void Awake()
    {
        panelReveal = GetComponent<PanelReveal>();
        infoPanelReveal = infoPanel.GetComponent<PanelReveal>();

        // 인스펙터에서 버튼마다 고정 인덱스를 손으로 넣으면 실수하기 쉬워 코드로 연결한다.
        for (int i = 0; i < optionViews.Count; i++)
        {
            optionViews[i].SetIndex(i);
            optionViews[i].BindClick(OnOption);
        }


        outsideCloser = new ClickOutsideCloser((RectTransform)transform, parentPanel != null ? parentPanel.transform : null);

        var optionTransforms = new Transform[optionViews.Count];
        for (int i = 0; i < optionViews.Count; i++) optionTransforms[i] = optionViews[i].transform;
        infoOutsideCloser = new ClickOutsideCloser((RectTransform)infoPanel.transform, optionTransforms);

        // 인스펙터 편집 편의상 활성 상태로 저장돼 있어 스케일이 (1,1,1)로 남아있다 - 여기서
        // 스케일만 0으로 맞춰 첫 Show()가 확대 연출 없이 바로 나타나는 걸 막는다.
        if (panelReveal != null) transform.localScale = Vector3.zero;
        if (infoPanelReveal != null) infoPanel.transform.localScale = Vector3.zero;
    }

    private void OnEnable()
    {
        panelStack.Push(this);
        resourcesManager.ProductUpdate += RefreshButtons;
        LocalizeTextManager.OnLanguageChanged += OnLanguageChanged;
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
        infoPanel.SetActive(false);
        RefreshButtons();
        outsideCloser.MarkOpened();
        StateChanged?.Invoke();
    }

    private void OnDisable()
    {
        panelStack.Remove(this);
        resourcesManager.ProductUpdate -= RefreshButtons;
        LocalizeTextManager.OnLanguageChanged -= OnLanguageChanged;
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;
        StateChanged?.Invoke();
    }

    private void OnLanguageChanged()
    {
        RefreshButtons();
        if (infoPanel.activeSelf) RefreshInfoText();
    }

    private void HandleCloseCheck()
    {
        if (!panelStack.IsTop(this)) return;

        if (infoPanel.activeSelf && infoOutsideCloser.ShouldClose())
        {
            if (infoPanelReveal != null) infoPanelReveal.Hide();
            else infoPanel.SetActive(false);
            StateChanged?.Invoke();
            return;
        }

        if (outsideCloser.ShouldClose()) Close();
    }

    public void Open(RegionFacilitySlots target, int index)
    {
        region = target;
        slotIndex = index;
        currentOption = null;
        selectedIndex = -1;
        infoPanel.SetActive(false);

        bool wasActive = gameObject.activeSelf;
        // wasActive든 아니든 항상 Show()를 부른다 - 그래야 Close()가 막 시작해둔 닫힘 애니메이션이
        // 있어도(같은 프레임에 다른 슬롯을 눌러 Close 후 곧바로 Open이 불리는 경우) 확실히 취소되고
        // 열린 채로 유지된다. 이미 완전히 열려있는 상태에서 다시 불러도 Progress01이 1을 읽어 즉시
        // 끝나므로 애니메이션이 보이지 않는다.
        // RegionDetailPanel.Awake()가 이 오브젝트를 자기 Awake보다 먼저 SetActive(false)로 꺼버리면
        // 유니티가 이 컴포넌트의 Awake 자체를 얼마간 미뤄서, panelReveal이 아직 null인 채로 첫
        // Open()이 불릴 수 있다 - 그래서 캐시를 못 믿고 매번 여기서 다시 확인한다.
        if (panelReveal == null) panelReveal = GetComponent<PanelReveal>();
        if (panelReveal != null) panelReveal.Show();
        else gameObject.SetActive(true);
        outsideCloser.MarkOpened();

        // 이미 열려있던 채로 다른 슬롯을 골랐을 때는 OnEnable이 다시 안 불리니 직접 갱신한다.
        if (wasActive) RefreshButtons();
        StateChanged?.Invoke();
    }

    public void Close()
    {
        if (panelReveal == null) panelReveal = GetComponent<PanelReveal>();
        if (panelReveal != null) panelReveal.Hide();
        else gameObject.SetActive(false);
    }

    private void RefreshButtons()
    {
        for (int i = 0; i < optionViews.Count && i < options.Count; i++)
        {
            optionViews[i].SetOption(options[i].icon, options[i].DisplayName);
        }
    }

    public void OnOption(int index)
    {
        if (index < 0 || index >= options.Count) return;

        currentOption = options[index];
        selectedIndex = index;
        infoIcon.sprite = currentOption.icon;
        RefreshInfoText();
        if (infoPanelReveal == null) infoPanelReveal = infoPanel.GetComponent<PanelReveal>();
        if (infoPanelReveal != null) infoPanelReveal.Show();
        else infoPanel.SetActive(true);
        infoOutsideCloser.MarkOpened();
        StateChanged?.Invoke();
    }

    // currentOption 기준으로 infoText를 다시 조립한다 - 언어가 바뀌었을 때도 같은 옵션을 다시 그릴 수 있도록
    // OnOption에서 분리해뒀다.
    private void RefreshInfoText()
    {
        if (currentOption == null) return;

        (ProductionType Type, int Amount)[] cost;
        ProductionType? productionType = null;

        if (currentOption.kind == OccupantKind.Resource && currentOption.facilityValue != null)
        {
            var value = currentOption.facilityValue;
            cost = ProductionFacility.PreviewConstructCost(value, economyConfig, upgradeState);
            productionType = value.Type;
            infoText.text = $"{value.FacilityDisplayName}\n{value.FacilityDisplayInfo}";
        }
        else if (currentOption.houseConfig != null)
        {
            var config = currentOption.houseConfig;
            cost = House.PreviewConstructCost(config, economyConfig, upgradeState);
            infoText.text = $"{config.HouseDisplayName}\n{config.HouseDisplayInfo}";
        }
        else
        {
            return;
        }

        if (productionRow != null) productionRow.SetActive(productionType.HasValue);
        if (productionType.HasValue && productionIcon != null) productionIcon.sprite = resourceIconSet.GetIcon(productionType.Value);

        for (int i = 0; i < constructCostRows.Count; i++)
        {
            if (i < cost.Length)
            {
                bool canBuild = resourcesManager.GetAmount(cost[i].Type) >= -cost[i].Amount;
                constructCostRows[i].Show(resourceIconSet.GetIcon(cost[i].Type), $"{-cost[i].Amount}", canBuild);
            }
            else
            {
                constructCostRows[i].Hide();
            }
        }
    }

    public void OnBuild()
    {
        if (currentOption == null || region == null) return;

        if (!constructor.CanBuild(currentOption))
        {
            CenterFeedbackUi.Instance.Show("UI_Base_NotEnoughResources");
            EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        if (constructor.TryBuild(currentOption, region, slotIndex, out _))
        {
            int builtSlotIndex = slotIndex;
            infoPanel.SetActive(false);
            Close();
            StateChanged?.Invoke();
            if (parentPanel != null) parentPanel.OpenBuiltSlot(builtSlotIndex);
        }
        else
        {
            // 건설 실패 시엔 패널이 안 닫혀서, Selected 상태가 Pressed와 같은 클립이라 안 풀면 눌린 모양이 남는다
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public bool TryFindOption(string buildKey, out BuildableFacility option)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (OptionKey(options[i]) == buildKey)
            {
                option = options[i];
                return true;
            }
        }

        option = null;
        return false;
    }

    // 옵션 하나의 식별 키를 구한다 (facilityValue 또는 houseConfig의 이름)
    private static string OptionKey(BuildableFacility option)
    {
        if (option.kind == OccupantKind.Resource && option.facilityValue != null) return option.facilityValue.FacilityName;
        if (option.houseConfig != null) return option.houseConfig.HouseName;
        return null;
    }
}
