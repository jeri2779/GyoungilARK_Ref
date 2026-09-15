using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

public class RegionOverviewPanel : MonoBehaviour, IClosablePanel, IExclusiveUiPanel, IPersistentAcrossExclusivePanels
{
    [SerializeField] private MapRegistry registry;
    [SerializeField] private RegionDetailPanel detailPanel;
    [SerializeField] private CenterHubPanel hubPanel;
    [SerializeField] private List<RegionNodeView> nodes;
    [SerializeField] private List<RegionFacilitySlots> regions; // 인스펙터에서 지역 오브젝트들을 직접 연결
    [SerializeField] private Button openButton; // 거점 화면을 여는 버튼 - 밤에는 비활성화
    [SerializeField] private GameObject redDot; // 새로 해금된 지역이 있으면 openButton 위에 표시
    [SerializeField] private Key openBaseKey = Key.B; // 거점 화면을 여는 단축키

    private ClickOutsideCloser outsideCloser;
    // ESC가 눌린 시점에 메뉴/가이드 같은 다른 배타 패널이 이미 떠 있었는지의 스냅샷 - UiManager의
    // hadEscapeCloseTargetLastFrame과 같은 이유(Update 실행 순서 무관하게 만들기 위함)로 한 프레임 전
    // 값을 쓴다. 이게 없으면 ESC 한 번에 메뉴/가이드와 거점 화면이 같은 프레임에 동시에 닫혀버린다.
    private bool hadOtherExclusivePanelLastFrame;

    // RegionDetailPanel이 자기 바깥-클릭 판정에서 지역 노드 버튼만 제외하는 데 쓴다
    // (오버뷰 전체가 아니라 노드들만 - 오버뷰는 화면 전체를 덮고 있어서 전체를 제외하면 바깥 클릭이 아예 안 잡힌다).
    public IReadOnlyList<RegionNodeView> Nodes => nodes;
    public IReadOnlyList<RegionFacilitySlots> Regions => regions;

    private UiPanelStack panelStack;
    private GameManager gameManager;
    private EnviromentManager enviromentManager;
    private InputAction openHotkeyAction;
    private bool isNight;
    // 지역 해금 알림을 확인했는지 담는 저장 원본
    private bool regionUnlockNoticeSeen = true;

    // 지역 해금 알림을 이미 확인했는지 알려준다
    public bool RegionUnlockNoticeSeen => regionUnlockNoticeSeen;

    [Inject]
    private void Construct(UiPanelStack panelStack, GameManager gameManager, EnviromentManager enviromentManager)
    {
        this.panelStack = panelStack;
        this.gameManager = gameManager;
        this.enviromentManager = enviromentManager;

        gameManager.ChangeToNight += OnNight;
        enviromentManager.OnDay += OnDayStart;

        // 열기 단축키는 패널이 닫혀있는 동안(=이 오브젝트가 비활성인 동안) 감지되어야 하는데,
        // 이 오브젝트는 씬에서 처음부터 비활성 상태라 Update()가 전혀 돌지 않는다(레드닷 구독과 동일한 이유).
        // InputAction의 performed 콜백은 GameObject 활성 여부와 무관하게 발화하므로 매 프레임 폴링 없이도 동작한다.
        openHotkeyAction = new InputAction("OpenRegionOverview", binding: Keyboard.current[openBaseKey].path);
        openHotkeyAction.performed += OnOpenHotkeyPerformed;
        openHotkeyAction.Enable();

        outsideCloser = new ClickOutsideCloser((RectTransform)transform, openButton != null ? openButton.transform : null);

        SubscribeModulesDeferred().Forget();
    }

    private async UniTaskVoid SubscribeModulesDeferred()
    {
        await UniTask.Yield();
        foreach (var module in registry.AllModules.Values)
        {
            module.OnStateChanged += OnAnyModuleUnlocked;
        }
    }

    private void OnDestroy()
    {
        gameManager.ChangeToNight -= OnNight;
        enviromentManager.OnDay -= OnDayStart;

        openHotkeyAction.performed -= OnOpenHotkeyPerformed;
        openHotkeyAction.Disable();
        openHotkeyAction.Dispose();

        foreach (var module in registry.AllModules.Values)
        {
            module.OnStateChanged -= OnAnyModuleUnlocked;
        }
    }

    private void OnAnyModuleUnlocked(ModuleState state)
    {
        if (state == ModuleState.Preparing && redDot != null)
        {
            regionUnlockNoticeSeen = false;
            redDot.SetActive(true);
        }
    }

    // 저장된 확인 여부를 넣고 레드닷 화면을 그 값에 맞춘다
    public void RestoreNoticeSeen(bool seen)
    {
        regionUnlockNoticeSeen = seen;
        if (redDot != null) redDot.SetActive(!seen);
    }

    private void OnNight()
    {
        isNight = true;
        if (openButton != null) openButton.gameObject.SetActive(false);
        Close();
    }

    private void OnDayStart()
    {
        isNight = false;
        if (openButton != null) openButton.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        ExclusiveUiCoordinator.NotifyOpened(this);
        regionUnlockNoticeSeen = true;
        if (redDot != null && redDot.activeSelf) redDot.SetActive(false); // 열었으니 확인한 걸로 치고 끈다
        panelStack.Push(this);
        BindModules();
        Refresh();
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
    }

    private void OnDisable()
    {
        ExclusiveUiCoordinator.NotifyClosed(this);
        panelStack.Remove(this);
        UnbindModules();
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;

        detailPanel.Close();
        if (hubPanel != null) hubPanel.Close();
    }

    private void BindModules()
    {
        foreach (var module in registry.AllModules.Values)
        {
            module.OnStateChanged += OnModuleState;
        }
    }

    private void UnbindModules()
    {
        foreach (var module in registry.AllModules.Values)
        {
            module.OnStateChanged -= OnModuleState;
        }
    }

    private void OnModuleState(ModuleState state)
    {
        Refresh();
    }

    private RegionFacilitySlots FindRegion(int moduleId)
    {
        foreach (var region in regions)
        {
            if (region.ModuleId == moduleId) return region;
        }
        return null;
    }

    private void Refresh()
    {
        foreach (var node in nodes)
        {
            if (!registry.TryGetModuleLogic(node.ModuleId, out var module))
            {
                node.SetLocked(true); // 대응하는 모듈이 없으면 잠긴 것으로 취급
                continue;
            }

            node.SetLocked(!module.IsUnlocked);
        }
    }

    public void OnNodeClicked(RegionNodeView node)
    {
        if (!registry.TryGetModuleLogic(node.ModuleId, out var module) || !module.IsUnlocked) return;

        var region = FindRegion(node.ModuleId);
        if (region == null) return;

        // 이미 이 지역이 열려있는 채로 같은 노드를 또 누르면 닫는다(토글).
        if (detailPanel.CurrentRegion == region)
        {
            detailPanel.Close();
            return;
        }

        if (hubPanel != null) hubPanel.Close();
        detailPanel.Open(region);
    }

    public void OpenPanel()
    {
        if (isNight) return;

        if (TutorialInputGate.BlockPanelOpen) return;

        if (gameObject.activeSelf) return;

        outsideCloser.MarkOpened();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void RequestClose() => Close();

    // 메뉴/가이드 등이 위에 떠 있는 동안은 ESC/바깥클릭에 반응하지 않고 그쪽부터 닫히게 양보한다.
    private void HandleCloseCheck()
    {
        if (hadOtherExclusivePanelLastFrame) return;
        if (panelStack.IsTop(this) && outsideCloser.ShouldClose()) Close();
    }

    private void LateUpdate()
    {
        hadOtherExclusivePanelLastFrame = ExclusiveUiCoordinator.HasOtherOpen(this);
    }

    private void OnOpenHotkeyPerformed(InputAction.CallbackContext context)
    {
        if (TutorialInputGate.BlockHotkeys) return;
        if (isNight) return; // 거점 버튼과 동일하게 밤에는 단축키로도 못 연다(OpenPanel과 동일 규칙)

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
        else
            gameObject.SetActive(true);
    }
}
