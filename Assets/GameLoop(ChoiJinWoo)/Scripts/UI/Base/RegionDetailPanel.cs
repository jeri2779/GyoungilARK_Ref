using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

// 지역 하나의 상세 패널(사이드 패널) - 구조물 그리드 + 인구 로우.
// 빈 칸 클릭 -> FacilityBuildChoicePanel, 지어진 칸 클릭 -> BuildingPanel(인력/업그레이드).
public class RegionDetailPanel : MonoBehaviour, IClosablePanel
{
    [SerializeField] private TextMeshProUGUI regionNameText;
    [SerializeField] private List<FacilitySlotView> slotViews; // 인스펙터에서 최대 슬롯 개수만큼 미리 배치
    [SerializeField] private FacilityBuildChoicePanel buildChoicePanel;
    [SerializeField] private BuildingPanel buildingPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private RegionOverviewPanel overviewPanel; // 지역 노드 버튼 클릭은 "바깥 클릭"이 아니다

    private RegionFacilitySlots region;
    private IUpgradableOccupant openOccupant;
    private UiPanelStack panelStack;
    private ClickOutsideCloser outsideCloser;
    private PanelReveal panelReveal;

    // 지금 열려서 보여주고 있는 지역 - 같은 지역 노드를 다시 눌렀는지 오버뷰가 판단하는 데 쓴다.
    public RegionFacilitySlots CurrentRegion => gameObject.activeSelf ? region : null;

    // RegionNodeHeldLink가 CurrentRegion을 매 프레임 폴링하는 대신 구독할 수 있도록 Open/Close/OnDisable에서 발화한다.
    public event System.Action RegionChanged;

    [Inject]
    private void Construct(UiPanelStack panelStack)
    {
        this.panelStack = panelStack;
        
        Transform[] nodeTransforms = null;
        if (overviewPanel != null)
        {
            var nodes = overviewPanel.Nodes;
            nodeTransforms = new Transform[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) nodeTransforms[i] = nodes[i].transform;
        }
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, nodeTransforms);
    }

    private void Awake()
    {
        panelReveal = GetComponent<PanelReveal>();

        for (int i = 0; i < slotViews.Count; i++)
        {
            slotViews[i].SetIndex(i);
            slotViews[i].BindClick(OnSlotClicked);
        }

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        buildChoicePanel.gameObject.SetActive(false);
        buildingPanel.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        panelStack.Push(this);
        outsideCloser.MarkOpened();
        LocalizeTextManager.OnLanguageChanged += Refresh;
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
    }

    // buildChoicePanel/buildingPanel도 같은 UiPanelStack에 Push되는 패널이라, 둘 중 하나가 열리면
    // 그게 스택 맨 위가 되어 IsTop(this)가 자연히 false가 된다 - 그쪽이 먼저 처리하고 여긴 쉰다.
    private void HandleCloseCheck()
    {
        if (panelStack.IsTop(this) && outsideCloser.ShouldClose()) Close();
    }

    public void Open(RegionFacilitySlots target)
    {
        if (region != null) region.OnSlotsChanged -= Refresh;

        // 다른 지역으로 옮겨가는 거라, 이전 지역 슬롯에 물려있던 팝업은 정리한다.
        buildChoicePanel.Close();
        buildingPanel.gameObject.SetActive(false);
        UnsubscribeOpenOccupant();

        region = target;
        region.OnSlotsChanged += Refresh;

        if (panelReveal != null) panelReveal.Show();
        else gameObject.SetActive(true);
        outsideCloser.MarkOpened();
        Refresh();
        RegionChanged?.Invoke();
    }

    // RegionChanged는 여기서 바로 발화하지 않는다 - panelReveal.Hide()는 축소 애니메이션이 끝난 뒤에야
    // 실제로 SetActive(false)를 부르므로(OnDisable에서 발화), CurrentRegion(gameObject.activeSelf 기준)이
    // 실제로 바뀌는 시점과 맞추기 위해서다.
    public void Close()
    {
        if (panelReveal != null) panelReveal.Hide();
        else gameObject.SetActive(false);
        buildChoicePanel.gameObject.SetActive(false);
        buildingPanel.gameObject.SetActive(false);
    }

    private void UnsubscribeOpenOccupant()
    {
        if (openOccupant != null)
        {
            openOccupant.Changed -= Refresh;
            openOccupant = null;
        }
    }

    private void OnDisable()
    {
        panelStack.Remove(this);
        LocalizeTextManager.OnLanguageChanged -= Refresh;
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;

        UnsubscribeOpenOccupant();

        if (region != null)
        {
            region.OnSlotsChanged -= Refresh;
            region = null;
        }

        buildChoicePanel.gameObject.SetActive(false);
        buildingPanel.gameObject.SetActive(false);
        RegionChanged?.Invoke();
    }

    private void Refresh()
    {
        if (region == null) return;

        regionNameText.text = region.RegionName;

        for (int i = 0; i < slotViews.Count; i++)
        {
            if (i >= region.Slots.Count)
            {
                slotViews[i].gameObject.SetActive(false);
                continue;
            }

            slotViews[i].gameObject.SetActive(true);
            var slot = region.Slots[i];
            if (slot.IsEmpty)
            {
                slotViews[i].ShowEmpty();
                continue;
            }

            string label = "";
            string level = "";
            string workers = "";
            if (slot.Occupant is ProductionFacility facility)
            {
                label = facility.BasicValue.FacilityDisplayName;
                level = string.Format(DataTableManager.StringTable.Get("Ui_LevelFormat"), facility.UpgradeCount);
                workers = $"{facility.WorkerAmount}/{facility.MaxWorker}";
            }
            else if (slot.Occupant is House house)
            {
                label = house.Config.HouseDisplayName;
                level = string.Format(DataTableManager.StringTable.Get("Ui_LevelFormat"), house.UpgradeCount);
            }
            slotViews[i].ShowBuilt(slot.Icon, label, level, workers);
        }
    }

    private void OnSlotClicked(int index)
    {
        // 튜토리얼이 영웅 배치 대기 중(맵 클릭용으로 화면을 전부 풀어둔 상태)일 땐 이 슬롯 클릭으로
        // BuildingPanel/FacilityBuildChoicePanel이 열리면 그게 맵을 덮어 배치를 끝낼 수 없게 되어
        // 튜토리얼이 멈춘다 - TutorialInputGate.cs 참고.
        if (TutorialInputGate.BlockPanelOpen) return;
        if (region == null || index >= region.Slots.Count) return;

        var slot = region.Slots[index];
        if (slot.IsEmpty)
        {
            // 이미 이 칸의 선택 팝업이 열려있는 채로 같은 칸을 또 누르면 닫는다(토글).
            if (buildChoicePanel.gameObject.activeSelf && buildChoicePanel.SlotIndex == index)
            {
                buildChoicePanel.Close();
                return;
            }

            buildingPanel.gameObject.SetActive(false); // 지어진 칸용 패널이 열려있었다면 정리
            UnsubscribeOpenOccupant();

            buildChoicePanel.Open(region, index);
            return;
        }

        OpenBuiltSlot(index);
    }

    // 지어진 칸을 BuildingPanel로 연다 - 슬롯 클릭과, FacilityBuildChoicePanel에서 건설을 막 끝낸
    // 직후(선택 팝업을 닫으면서 그 칸을 이어서 보여주는 용도) 둘 다에서 쓴다.
    public void OpenBuiltSlot(int index)
    {
        if (region == null || index >= region.Slots.Count) return;

        var slot = region.Slots[index];
        if (slot.Occupant is not IUpgradableOccupant occupant) return;

        buildChoicePanel.Close(); // 빈 칸용 패널이 열려있었다면 정리

        UnsubscribeOpenOccupant();
        openOccupant = occupant;
        openOccupant.Changed += Refresh;

        buildingPanel.Open(slot.Occupant, region, index);
    }
}
