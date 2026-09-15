using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

// 지역 오버뷰 가운데 성 - 총 자원 생산량 확인 + 시민 생성(AddCitizen) 진입점.
// 6개 지역과 달리 ModuleLogic/해금 개념이 없는 고정 허브라 RegionNodeView와 별도로 둔다.
public class CenterHubPanel : MonoBehaviour, IClosablePanel
{
    [SerializeField] private List<ResourceAmountRow> resourceRows; // 자원별 아이콘+수량 한 줄씩, 인스펙터에서 구성
    [SerializeField] private AddCitizen addCitizenPanel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button tradeWoodButton;
    [SerializeField] private Button tradeIronButton;
    [SerializeField] private Button tradeFoodButton;
    [SerializeField] private Button tradeGoldButton;
    [SerializeField] private Button tradeStoneButton;
    [SerializeField] private TextMeshProUGUI tradeWoodText;
    [SerializeField] private TextMeshProUGUI tradeIronText;
    [SerializeField] private TextMeshProUGUI tradeFoodText;
    [SerializeField] private TextMeshProUGUI tradeGoldText;
    [SerializeField] private TextMeshProUGUI tradeStoneText;
    [SerializeField] private RegionDetailPanel detailPanel;

    private FacilityManager facilityManager;
    private UiPanelStack panelStack;
    private ClickOutsideCloser outsideCloser;
    private ResourcesManager resourcesManager;

    [Inject]
    private void Construct(FacilityManager facilityManager, UiPanelStack panelStack, ResourcesManager resourcesManager)
    {
        this.facilityManager = facilityManager;
        this.panelStack = panelStack;
        this.resourcesManager = resourcesManager;
    }

    private void Awake()
    {
        addCitizenPanel.gameObject.SetActive(false);
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, openButton != null ? openButton.transform : null);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        panelStack.Push(this);
        outsideCloser.MarkOpened();
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
        // 허브 패널이 열릴 땐 지역 상세 패널을 닫는다. 실제 SetActive(true)는 openButton에 붙은
        // UIButtonHeld가 처리하므로(이 컴포넌트가 아님), 클릭 리스너 실행 순서에 기대지 않도록
        // 이 컴포넌트가 켜질 때마다 항상 실행되는 OnEnable에서 처리한다.
        if (detailPanel != null) detailPanel.Close();

        if (tradeFoodButton != null)
            tradeFoodButton.onClick.AddListener(() => {
                resourcesManager.TradeResource(ProductionType.Food);
                RefreshButton();
                ClearButtonFocus();
            });
        if (tradeIronButton != null)
            tradeIronButton.onClick.AddListener(() => {
                resourcesManager.TradeResource(ProductionType.Iron);
                RefreshButton();
                ClearButtonFocus();
            });
        if (tradeWoodButton != null)
            tradeWoodButton.onClick.AddListener(() => {
                resourcesManager.TradeResource(ProductionType.Wood);
                RefreshButton();
                ClearButtonFocus();
            });
        if (tradeGoldButton != null)
            tradeGoldButton.onClick.AddListener(() => {
                resourcesManager.TradeResource(ProductionType.Gold);
                RefreshButton();
                ClearButtonFocus();
            });
        if (tradeStoneButton != null)
            tradeStoneButton.onClick.AddListener(() => {
                resourcesManager.TradeResource(ProductionType.Stone);
                RefreshButton();
                ClearButtonFocus();
            });

        if (tradeWoodText != null) tradeWoodText.text = $"{resourcesManager.TradeAmount}";
        if (tradeIronText != null) tradeIronText.text = $"{resourcesManager.TradeAmount}";
        if (tradeFoodText != null) tradeFoodText.text = $"{resourcesManager.TradeAmount}";
        if (tradeGoldText != null) tradeGoldText.text = $"{resourcesManager.TradeAmount}";
        if (tradeStoneText != null) tradeStoneText.text = $"{resourcesManager.TradeAmount}";

        RefreshButton();
        LocalizeTextManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        panelStack.Remove(this);
        LocalizeTextManager.OnLanguageChanged -= Refresh;
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;

        if (tradeFoodButton != null) tradeFoodButton.onClick.RemoveAllListeners();
        if (tradeIronButton != null) tradeIronButton.onClick.RemoveAllListeners();
        if (tradeWoodButton != null) tradeWoodButton.onClick.RemoveAllListeners();
        if (tradeGoldButton != null) tradeGoldButton.onClick.RemoveAllListeners();
        if (tradeStoneButton != null) tradeStoneButton.onClick.RemoveAllListeners();
    }

    // addCitizenPanel은 하이러키상 자식이 아니라 필드로만 참조되는 별도 패널이라, 열려있는 동안엔
    // 그 안의 클릭을 이 패널의 바깥 클릭으로 오판하지 않도록 판정을 쉰다.
    private void HandleCloseCheck()
    {
        if (addCitizenPanel != null && addCitizenPanel.gameObject.activeSelf) return;
        if (panelStack.IsTop(this) && outsideCloser.ShouldClose()) Close();
    }

    // Selected 상태가 Pressed와 같은 클립이라, 포커스를 안 풀면 눌린 모양이 계속 남는다
    private void ClearButtonFocus()
    {
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void RefreshButton()
    {
        tradeStoneButton.interactable = resourcesManager.Special > 0;
        tradeGoldButton.interactable = resourcesManager.Special > 0;
        tradeIronButton.interactable = resourcesManager.Special > 0;
        tradeWoodButton.interactable = resourcesManager.Special > 0;
        tradeFoodButton.interactable = resourcesManager.Special > 0;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        if (addCitizenPanel != null) addCitizenPanel.Close();
    }

    private void Refresh()
    {
        var totals = facilityManager.GetTotalProduction();

        // 생산량이 0인 자원도 표시한다 - 생산 중인 것만 나오면 아예 안 만든 자원인지 0인지 구분이 안 된다.
        foreach (var row in resourceRows)
        {
            int amount = 0;
            foreach (var product in totals)
            {
                if (product.Type == row.type)
                {
                    amount = product.Amount;
                    break;
                }
            }
            if (row.amountText != null) row.amountText.text = $"{amount}{DataTableManager.StringTable.Get("Ui_PerDay")}";
        }
    }

    public void OnCreateCitizen()
    {
        if (addCitizenPanel != null) addCitizenPanel.OpenPanel();
    }
}
