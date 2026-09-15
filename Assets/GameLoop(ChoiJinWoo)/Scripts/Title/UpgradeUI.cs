using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UpgradeUI : MonoBehaviour, IExclusiveUiPanel
{
    [SerializeField] private List<BaseUpgradeData> SystemUpgradeData;
    [SerializeField] private GameObject SystemUpgradeParent;
    [SerializeField] private List<BaseUpgradeData> FacilityUpgradeData;
    [SerializeField] private GameObject FacilityUpgradeParent;
    [SerializeField] private List<BaseUpgradeData> HeroUpgradeData;
    [SerializeField] private GameObject HeroUpgradeParent;
    [SerializeField] private UpgradeInfoUI upgradeInfoPanel;
    [SerializeField] private BaseUpgradeButton buttonPrefab;
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private RectTransform openButtonRect; // 이 패널을 여는 버튼 — 바깥 클릭 판정에서 제외
    [SerializeField] private int cheatAddPointsAmount = 100;

    private UpgradeState upgradeState;
    private ClickOutsideCloser outsideCloser;
    private PanelReveal panelReveal;
    private readonly Dictionary<BaseUpgradeData, BaseUpgradeButton> nodes = new();

    private void Awake()
    {
        upgradeState = new UpgradeState();
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, openButtonRect);
        panelReveal = GetComponent<PanelReveal>();
    }

    private void OnEnable()
    {
        outsideCloser.MarkOpened();
        ExclusiveUiCoordinator.NotifyOpened(this);
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
    }

    private void OnDisable()
    {
        ExclusiveUiCoordinator.NotifyClosed(this);
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;
    }

    private void HandleCloseCheck()
    {
        if (outsideCloser.ShouldClose())
        {
            Close();
        }
    }

    // ExclusiveUiCoordinator가 다른 배타 패널이 열렸을 때 이 패널을 닫으라고 부르는 창구.
    public void RequestClose() => Close();

    private void Close()
    {
        if (panelReveal != null) panelReveal.Hide();
        else gameObject.SetActive(false);
    }

    private void Start()
    {
        CreateButton(SystemUpgradeData, SystemUpgradeParent);
        CreateButton(FacilityUpgradeData, FacilityUpgradeParent);
        CreateButton(HeroUpgradeData, HeroUpgradeParent);
        upgradeInfoPanel.ConfirmClicked += OnConfirmUnlock;
        RefreshAll();
        OnNodeClicked(nodes.First().Key);
    }

    private void CreateButton(List<BaseUpgradeData> datas, GameObject parents)
    {
        foreach (var data in datas)
        {
            var node = Instantiate(buttonPrefab, parents.transform);
            node.Clicked += OnNodeClicked;
            nodes[data] = node;
        }
    }

    private void OnNodeClicked(BaseUpgradeData data)
    {
        bool alreadyUnlocked = upgradeState.IsUnlocked(data.id);
        upgradeInfoPanel.Show(data, !alreadyUnlocked && CanUnlock(data));
    }

    private void OnConfirmUnlock(BaseUpgradeData data)
    {
        if (!CanUnlock(data)) return;
        if (!upgradeState.TrySpendPoints(data.cost)) return;

        upgradeState.Unlock(data.id);
        RefreshAll();
        upgradeInfoPanel.Show(data, false); // 방금 해금했으니 확정 버튼은 다시 비활성화
    }

    private bool CanUnlock(BaseUpgradeData data) =>
        !upgradeState.IsUnlocked(data.id) &&
        upgradeState.CanAfford(data.cost) &&
        data.prerequisites.All(p => upgradeState.IsUnlocked(p.id));

    private void RefreshAll()
    {
        foreach (var kv in nodes)
            kv.Value.Set(kv.Key, upgradeState.IsUnlocked(kv.Key.id), kv.Key.prerequisites.All(p => upgradeState.IsUnlocked(p.id)));

        if (pointsText != null)
            pointsText.text = upgradeState.Points.ToString();
    }

    // 리셋 버튼의 OnClick에 연결 — 해금됐던 업그레이드들의 cost를 환불하고 전부 리셋(리스펙)
    public void OnResetButton()
    {
        upgradeState.ResetAll(nodes.Keys);
        RefreshAll();
        upgradeInfoPanel.OnReset(CanUnlock(upgradeInfoPanel.Current));
    }

    public void OnClose()
    {
        Close();
    }
}
