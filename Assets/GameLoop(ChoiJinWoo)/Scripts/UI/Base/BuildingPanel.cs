using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public class BuildingPanel : MonoBehaviour, IClosablePanel
{
    [SerializeField] private TextMeshProUGUI workerText;
    [SerializeField] private Image productIcon;
    [SerializeField] private TextMeshProUGUI perProductText;
    [SerializeField] private List<CostAmountView> upgradeCostRows; // 최대 개수만큼 미리 배치, 남는 칸은 자동으로 숨김
    [SerializeField] private TextMeshProUGUI FacilityLevelText;
    [SerializeField] private TextMeshProUGUI NextLevelInfoText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button demolishButton; // House는 철거를 지원하지 않아 비활성화한다
    [SerializeField] private GameObject workerButtonsContainer; // +/- 인력 버튼을 감싸는 오브젝트 - House에는 없는 개념이라 통째로 숨김
    [SerializeField] private RegionDetailPanel parentPanel; // 이 패널을 여는 쪽 - 그 안의 슬롯 버튼 클릭은 "바깥 클릭"이 아니다
    [SerializeField] private GameObject UpgradeResources;
    [SerializeField] private Image maxImage;
    [SerializeField] private GameObject demolishCheckPanel;
    private ProductionFacility facility;
    private House house;
    private IUpgradableOccupant Occupant => facility != null ? (IUpgradableOccupant)facility : house;
    private RegionFacilitySlots region;
    private int slotIndex;
    public int SlotIndex => slotIndex;
    private UiPanelStack panelStack;
    private BaseConstructor constructor;
    private ResourceIconSet resourceIconSet;
    private ResourcesManager resourcesManager;
    private ClickOutsideCloser outsideCloser;
    private PanelReveal panelReveal;

    [Inject]
    private void Construct(UiPanelStack panelStack, BaseConstructor constructor, ResourceIconSet resourceIconSet, ResourcesManager resourcesManager)
    {
        this.panelStack = panelStack;
        this.constructor = constructor;
        this.resourceIconSet = resourceIconSet;
        this.resourcesManager = resourcesManager;
    }

    private void Awake()
    {
        panelReveal = GetComponent<PanelReveal>();
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, parentPanel != null ? parentPanel.transform : null);

        // 인스펙터 편집 편의상 활성 상태로 저장돼 있어 스케일이 (1,1,1)로 남아있다 - 여기서
        // 스케일만 0으로 맞춰 첫 Show()가 확대 연출 없이 바로 나타나는 걸 막는다.
        if (panelReveal != null) transform.localScale = Vector3.zero;
    }

    private void OnEnable()
    {
        panelStack.Push(this);
        outsideCloser.MarkOpened();
        LocalizeTextManager.OnLanguageChanged += UpdatePanel;
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
        demolishCheckPanel.gameObject.SetActive(false);
    }

    public void Open(object occupant, RegionFacilitySlots region, int slotIndex)
    {
        // RegionDetailPanel.Awake()가 이 오브젝트를 자기 Awake보다 먼저 SetActive(false)로 꺼버리면
        // 유니티가 이 컴포넌트의 Awake 자체를 얼마간 미뤄서, panelReveal이 아직 null인 채로 첫
        // Open()이 불릴 수 있다 - 그래서 캐시를 못 믿고 매번 여기서 다시 확인한다.
        if (panelReveal == null) panelReveal = GetComponent<PanelReveal>();
        if (panelReveal != null) panelReveal.Show();
        else gameObject.SetActive(true);
        InitOccupant(occupant, region, slotIndex);
    }

    public void Close()
    {
        if (panelReveal == null) panelReveal = GetComponent<PanelReveal>();
        if (panelReveal != null) panelReveal.Hide();
        else gameObject.SetActive(false);
    }

    private void HandleCloseCheck()
    {
        if (panelStack.IsTop(this) && outsideCloser.ShouldClose()) Close();
    }

    public void OnMinusButton()
    {
        if(facility != null)
        {
            facility.DecreaseWorker();
        }

        // Selected 상태가 Pressed와 같은 클립이라, 포커스를 안 풀면 눌린 모양이 계속 남는다
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void OnPlusButton()
    {
        if (facility != null)
        {
            facility.IncreaseWorker();
        }

        // Selected 상태가 Pressed와 같은 클립이라, 포커스를 안 풀면 눌린 모양이 계속 남는다
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void UpdatePanel()
    {
        var occupant = Occupant;
        if (occupant == null || FacilityLevelText == null) return;

        var table = DataTableManager.StringTable;

        bool isFacility = facility != null;
        if (workerText != null) workerText.gameObject.SetActive(isFacility);
        if (productIcon != null) productIcon.gameObject.SetActive(isFacility);
        if (perProductText != null) perProductText.gameObject.SetActive(isFacility);
        if (workerButtonsContainer != null) workerButtonsContainer.SetActive(isFacility);
        if (demolishButton != null) demolishButton.gameObject.SetActive(isFacility);

        if (isFacility)
        {
            if (workerText != null) workerText.text = $"{facility.WorkerAmount}/{facility.MaxWorker}";
            if (productIcon != null) productIcon.sprite = resourceIconSet.GetIcon(facility.ProductionType);
            if (perProductText != null) perProductText.text = $"{facility.ProductAmount * facility.WorkerAmount}{table.Get("Ui_PerDay")}";
        }

        // 자원 부족은 더 이상 버튼을 비활성화하지 않는다 - 눌렀을 때 메시지로 안내한다(OnUpgrade 참고).
        // 최대 레벨일 때는 아래에서 버튼 자체를 SetActive(false)로 숨기므로 이 값은 영향이 없다.
        upgradeButton.interactable = true;

        var costs = occupant.UpgradeCostCopy;
        for (int i = 0; i < upgradeCostRows.Count; i++)
        {
            if (i < costs.Length)
            {
                bool canBuild = resourcesManager.GetAmount(costs[i].Type) >= -costs[i].Amount;
                upgradeCostRows[i].Show(resourceIconSet.GetIcon(costs[i].Type), $"{-costs[i].Amount}", canBuild);
            }
            else
            {
                upgradeCostRows[i].Hide();
            }
        }

        FacilityLevelText.text = string.Format(table.Get("Ui_LevelFormat"), occupant.UpgradeCount);
        if (NextLevelInfoText != null)
        {
            bool isMaxLevel = occupant.UpgradeCount == occupant.MaxUpgrade;
            string currentLevel = string.Format(table.Get("Ui_LevelFormat"), isMaxLevel ? table.Get("Ui_MaxLevel") : occupant.UpgradeCount.ToString());
            string nextLevel = isMaxLevel ? string.Empty : $"→ {string.Format(table.Get("Ui_LevelFormat"), occupant.UpgradeCount + 1)}";
            NextLevelInfoText.text = $"{currentLevel}{nextLevel}\n{table.Get(occupant.NextUpgradeInfo)}";
        }

        if (UpgradeResources != null && occupant.UpgradeCount == occupant.MaxUpgrade)
        {
            UpgradeResources.SetActive(false);
            maxImage.gameObject.SetActive(true);
            upgradeButton.gameObject.SetActive(false);
        }
        else
        {
            UpgradeResources.SetActive(true);
            maxImage.gameObject.SetActive(false);
            upgradeButton.gameObject.SetActive(true);
        }
    }

    public void InitOccupant(object occupant, RegionFacilitySlots region, int slotIndex)
    {
        // 이미 열려있던 채로 다른 칸을 골랐을 수 있으니, 이전 점유물 구독부터 정리한다.
        UnsubscribeOccupant();

        facility = occupant as ProductionFacility;
        house = occupant as House;
        this.region = region;
        this.slotIndex = slotIndex;

        var current = Occupant;
        if (current != null) current.Changed += UpdatePanel;
        UpdatePanel();
        StateChanged?.Invoke();
    }

    private void UnsubscribeOccupant()
    {
        var current = Occupant;
        if (current != null) current.Changed -= UpdatePanel;
    }

    private void OnDisable()
    {
        panelStack.Remove(this);
        LocalizeTextManager.OnLanguageChanged -= UpdatePanel;
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;

        UnsubscribeOccupant();
        facility = null;
        house = null;
        StateChanged?.Invoke();
    }

    // 튜토리얼이 "업그레이드 버튼을 실제로 눌렀는지"만 골라 판정할 수 있도록 알려준다.
    public event System.Action Upgraded;

    // FacilitySlotHeldLink가 "이 패널이 열려있고 + SlotIndex가 자기 슬롯인지"를 매 프레임 폴링하는 대신
    // 구독할 수 있도록, 열림/닫힘(OnEnable·OnDisable)과 같은 슬롯 그룹 안에서 SlotIndex가 바뀌는 지점(InitOccupant)에서 발화한다.
    public event System.Action StateChanged;

    public void OnUpgrade()
    {
        if (Occupant == null) return;

        if (!resourcesManager.CheckResources(Occupant.UpgradeCostCopy))
        {
            CenterFeedbackUi.Instance.Show("UI_Base_NotEnoughResources");
            return;
        }

        Occupant.Upgrade();
        Upgraded?.Invoke();

        // Selected 상태가 Pressed와 같은 클립이라, 포커스를 안 풀면 눌린 모양이 계속 남는다
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void CheckDemolish()
    {
        demolishCheckPanel.gameObject.SetActive(true);
    }

    public void OnDemolish()
    {
        if (region == null || facility == null) return;

        constructor.Demolish(region, slotIndex);
        demolishCheckPanel.gameObject.SetActive(false);
        Close();
    }

    public void OnDemolishCancel()
    {
        demolishCheckPanel.gameObject.SetActive(false);
    }
}
