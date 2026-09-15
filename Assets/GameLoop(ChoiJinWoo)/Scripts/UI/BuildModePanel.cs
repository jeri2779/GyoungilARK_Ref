using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BuildModePanel : MonoBehaviour
{
    [SerializeField] private GameObject heroPanel;
    //[SerializeField] private GameObject upgradePanel;
    [SerializeField] private GameObject classUpgradePanel;
    //[SerializeField] private GameObject cheatPanel;
    [SerializeField] private HeroArchiveButton heroArchiveButton;
    [SerializeField] private GameObject heroInventory;
    [SerializeField] private HeroCreateAmountController heroCreateAmountPanel;
    [SerializeField] private MapView view;
    [SerializeField] private MapGame game;
    [SerializeField] private BuildPanelSlide panelSlide;
    [SerializeField] private Key upgradeKey = Key.U;
    [SerializeField] private Key replaceKey = Key.R;
    [SerializeField] private Key removeKey = Key.E;
    [SerializeField] private Key inventoryKey = Key.I;
    [SerializeField] private Key createHeroKey = Key.C;
    private ClickOutsideCloser heroPanelCloser;
    private ClickOutsideCloser inventoryCloser;
    private ClickOutsideCloser classUpgradeCloser;
    private PanelActivityNotifier heroPanelNotifier;
    private PanelActivityNotifier heroInventoryNotifier;
    private PanelActivityNotifier classUpgradeNotifier;
    private GameObject lastSelectedGameObject; // 재배치/회수 모드 중 다른 버튼 클릭 감지용
    private bool reopenInventoryAfterMode; // 재배치/제거 모드로 들어가며 인벤토리를 자동으로 닫았는지
    private bool reopenClassUpgradeAfterMode; // 재배치/제거 모드로 들어가며 강화 패널을 자동으로 닫았는지
    private InputAction upgradeAction;
    private InputAction replaceAction;
    private InputAction removeAction;
    private InputAction inventoryAction;
    private InputAction createHeroAction;

    private void Start()
    {
        game.Rule.ChangeToNight += DisablePanels;
        game.EnviromentManager.OnDay += EnablePanel;
        GlobalUiInputSignals.ClickPerformed += HandleOutsideClick;
        GlobalUiInputSignals.EscapePerformed += HandleEscape;
    }

    private void OnDestroy()
    {
        game.Rule.ChangeToNight -= DisablePanels;
        game.EnviromentManager.OnDay -= EnablePanel;
        GlobalUiInputSignals.ClickPerformed -= HandleOutsideClick;
        GlobalUiInputSignals.EscapePerformed -= HandleEscape;

        heroPanelNotifier.ActiveChanged -= OnManagedPanelActiveChanged;
        classUpgradeNotifier.ActiveChanged -= OnManagedPanelActiveChanged;
        heroInventoryNotifier.ActiveChanged -= OnManagedPanelActiveChanged;

        DisposeHotkeyAction(upgradeAction, OnUpgradeHotkey);
        DisposeHotkeyAction(replaceAction, OnReplaceHotkey);
        DisposeHotkeyAction(removeAction, OnRemoveHotkey);
        DisposeHotkeyAction(inventoryAction, OnInventoryHotkey);
        DisposeHotkeyAction(createHeroAction, OnCreateHeroHotkey);
    }

    private static void DisposeHotkeyAction(InputAction action, Action<InputAction.CallbackContext> handler)
    {
        action.performed -= handler;
        action.Disable();
        action.Dispose();
    }

    // 열린 하위 패널을 닫고 빌드 패널의 퇴장 연출을 시작한다.
    private void DisablePanels()
    {
        if (heroPanel.activeSelf) SetPanelOpen(heroPanel, false);
        if (classUpgradePanel.activeSelf) SetPanelOpen(classUpgradePanel, false);
        if (heroInventory.activeSelf) SetPanelOpen(heroInventory, false);
        heroArchiveButton?.Close();
        panelSlide.Close();
    }

    // heroPanel/classUpgradePanel에 PanelReveal이 붙어 있으면 그 스케일 연출로 열고 닫는다(버튼의
    // UIButtonHeld.Toggle()과 같은 규칙). 여기서 안 이러면 클릭과 단축키가 서로 다른 방식으로 여닫게
    // 되고, PanelReveal.Hide()가 스케일을 0으로 남겨둔 채 끝내는데 이후 SetActive(true)만 부르면
    // 오브젝트는 켜지지만 스케일이 0인 채로 남아 안 보이게 된다.
    private static void SetPanelOpen(GameObject panel, bool open)
    {
        PanelReveal reveal = panel.GetComponent<PanelReveal>();
        if (reveal != null)
        {
            if (open) reveal.Show();
            else reveal.Hide();
            return;
        }
        panel.SetActive(open);
    }

    // 낮 전환이 끝난 빌드 패널의 등장 연출을 시작한다.
    private void EnablePanel()
    {
        panelSlide.Open();
    }

    private void Awake()
    {
        heroPanel.SetActive(false);
        heroInventory.SetActive(false);
        classUpgradePanel.SetActive(false);

        // heroPanel/classUpgradePanel/heroInventory는 전용 스크립트가 없는 순수 GameObject라, OnEnable/OnDisable로
        // ExclusiveUiCoordinator에 알려줄 컴포넌트를 여기서 붙여준다(씬/프리팹을 직접 안 건드리기 위해).
        // 셋 다 등록해 둬야, 버튼 클릭이 Toggle()이든 OnHeroButton 등이든 어느 쪽을 거치든 상관없이
        // (OnEnable 기반이라 호출 경로를 안 타므로) 하나가 열리면 나머지가 항상 자동으로 닫힌다.
        if (heroPanel.GetComponent<ExclusivePanelPresence>() == null)
            heroPanel.AddComponent<ExclusivePanelPresence>();
        if (classUpgradePanel.GetComponent<ExclusivePanelPresence>() == null)
            classUpgradePanel.AddComponent<ExclusivePanelPresence>();
        if (heroInventory.GetComponent<ExclusivePanelPresence>() == null)
            heroInventory.AddComponent<ExclusivePanelPresence>();

        // UIButtonHeld.Toggle()처럼 이 세 패널을 BuildModePanel의 OnHeroButton/OnInventoryButton/
        // OnClassUpgradeButton을 거치지 않고 SetActive로 직접 여는 버튼이 있어도, 패널이 실제로 켜지는
        // 순간(OnEnable)에는 항상 반응할 수 있게 여기서도 PanelActivityNotifier를 구독해둔다 - 그래야
        // 그런 버튼을 눌러도 재배치/제거 모드가 정상적으로 종료된다(ExitPlaceModeIfActive와 같은 규칙).
        heroPanelNotifier = GetOrAddNotifier(heroPanel);
        classUpgradeNotifier = GetOrAddNotifier(classUpgradePanel);
        heroInventoryNotifier = GetOrAddNotifier(heroInventory);
        heroPanelNotifier.ActiveChanged += OnManagedPanelActiveChanged;
        classUpgradeNotifier.ActiveChanged += OnManagedPanelActiveChanged;
        heroInventoryNotifier.ActiveChanged += OnManagedPanelActiveChanged;

        // alsoSelf로 이 패널 전체(빌드모드 버튼들)를 넘겨서, 다른 버튼(예: 로스터)을 눌렀을 때
        // 그 클릭이 "바깥 클릭"으로 잡혀 heroPanel이 먼저 닫혔다가 onClick이 다시 여는 깜빡임을 막는다.
        heroPanelCloser = new ClickOutsideCloser((RectTransform)heroPanel.transform, transform,
            heroCreateAmountPanel != null ? (RectTransform)heroCreateAmountPanel.transform : null);
        inventoryCloser = new ClickOutsideCloser((RectTransform)heroInventory.transform, transform, (RectTransform)heroPanel.transform);
        classUpgradeCloser = new ClickOutsideCloser((RectTransform)classUpgradePanel.transform, transform, (RectTransform)classUpgradePanel.transform);

        upgradeAction = CreateHotkeyAction("BuildModeUpgrade", upgradeKey, OnUpgradeHotkey);
        replaceAction = CreateHotkeyAction("BuildModeReplace", replaceKey, OnReplaceHotkey);
        removeAction = CreateHotkeyAction("BuildModeRemove", removeKey, OnRemoveHotkey);
        inventoryAction = CreateHotkeyAction("BuildModeInventory", inventoryKey, OnInventoryHotkey);
        createHeroAction = CreateHotkeyAction("BuildModeCreateHero", createHeroKey, OnCreateHeroHotkey);
    }

    private static PanelActivityNotifier GetOrAddNotifier(GameObject panel)
    {
        PanelActivityNotifier notifier = panel.GetComponent<PanelActivityNotifier>();
        if (notifier == null) notifier = panel.AddComponent<PanelActivityNotifier>();
        return notifier;
    }

    // heroPanel/heroInventory/classUpgradePanel 중 하나가 (누가 열었든) 실제로 켜지는 순간 불린다.
    // 재배치/제거 모드 중이었다면 그 모드부터 정리한다 - CancelPlaceMode()가 아니라 ExitPlaceModeIfActive()를
    // 쓴다: 이 패널은 이미 스스로(또는 방금 켜준 코드가) 원하는 상태로 켜진 뒤이므로, 여기서 또
    // ReopenAfterPlaceMode()까지 돌리면 방금 켠 패널을 자기 자신이 다시 토글해버릴 수 있다.
    private void OnManagedPanelActiveChanged(bool active)
    {
        if (active) ExitPlaceModeIfActive();
    }

    private static InputAction CreateHotkeyAction(string name, Key key, Action<InputAction.CallbackContext> handler)
    {
        var action = new InputAction(name, binding: Keyboard.current[key].path);
        action.performed += handler;
        action.Enable();
        return action;
    }

    private void OnUpgradeHotkey(InputAction.CallbackContext context)
    {
        if (!TutorialInputGate.BlockHotkeys && game.Rule.CanBuild) OnClassUpgradeButton();
    }

    private void OnReplaceHotkey(InputAction.CallbackContext context)
    {
        if (!TutorialInputGate.BlockHotkeys && game.Rule.CanBuild) OnReplaceButton();
    }

    private void OnRemoveHotkey(InputAction.CallbackContext context)
    {
        if (!TutorialInputGate.BlockHotkeys && game.Rule.CanBuild) OnRemoveButton();
    }

    private void OnInventoryHotkey(InputAction.CallbackContext context)
    {
        if (!TutorialInputGate.BlockHotkeys && game.Rule.CanBuild) OnInventoryButton();
    }

    private void OnCreateHeroHotkey(InputAction.CallbackContext context)
    {
        if (!TutorialInputGate.BlockHotkeys && game.Rule.CanBuild) OnHeroButton();
    }

    // ESC로 메뉴를 열지 말지 판단할 때 쓴다(UiManager) - 여기서 취소/닫을 게 있으면 ESC는
    // 메뉴를 여는 대신 그것부터 처리해야 하므로, Update()의 ESC 분기와 조건을 그대로 맞춘다.
    public bool HasEscapeCancelable =>
        view.HasArmedOrSelectedSkill
        || view.IsHolding
        || !view.IsOff
        || heroPanel.activeSelf
        || heroInventory.activeSelf
        || classUpgradePanel.activeSelf
        || (heroArchiveButton != null && heroArchiveButton.IsOpen)
        || (heroCreateAmountPanel != null && heroCreateAmountPanel.gameObject.activeInHierarchy);

    private void HandleOutsideClick()
    {
        if (heroPanel.activeSelf && heroPanelCloser.ClickedOutside())
        {
            SetPanelOpen(heroPanel, false);
        }
        if (classUpgradePanel.activeSelf && classUpgradeCloser.ClickedOutside())
        {
            SetPanelOpen(classUpgradePanel, false);
        }
        if (heroInventory.activeSelf && view.IsOff && inventoryCloser.ClickedOutside())
        {
            SetPanelOpen(heroInventory, false);
        }
    }

    private void HandleEscape()
    {
        if (TutorialInputGate.BlockEscapeClose) return;

        // 영웅 스킬 시전자 선택/플레이어 스킬 무장이 있으면 그것부터 취소한다(우클릭과 동일한 우선순위).
        if (view.HasArmedOrSelectedSkill)
        {
            view.CancelSkillCasts();
        }
        // 영웅을 집은 상태면 재배치 모드는 유지하고 집은 것만 취소한다.
        else if (view.IsHolding)
        {
            view.CancelHold();
        }
        else if (!view.IsOff)
        {
            CancelPlaceMode();
        }
        else if (heroPanel.activeSelf || heroInventory.activeSelf || classUpgradePanel.activeSelf
            || (heroArchiveButton != null && heroArchiveButton.IsOpen))
        {
            SetPanelOpen(heroPanel, false);
            SetPanelOpen(heroInventory, false);
            SetPanelOpen(classUpgradePanel, false);
            heroArchiveButton?.Close();
        }
    }

    // EventSystem의 전역 선택 변화를 이벤트로 받으려면 모든 버튼 프리팹에 ISelectHandler를 추가해야 해서
    // 범위가 크고 검증이 어렵다 - 여기만 폴링으로 남겨둔다(재배치/제거 모드 중 다른 버튼 클릭 감지용).
    private void Update()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastSelectedGameObject) return;

        lastSelectedGameObject = selected;
        if (selected != null && (view.IsReplacing || view.IsRemoving))
        {
            Button clickedButton = selected.GetComponent<Button>();
            if (clickedButton != null
                && clickedButton.GetComponent<ReplaceHeldLink>() == null
                && clickedButton.GetComponent<RemoveHeldLink>() == null
                && clickedButton.GetComponent<UIReplaceHeld>() == null
                && clickedButton.GetComponent<UIRemoveHeld>() == null
                && !TargetsThisPanel(clickedButton))
            {
                CancelPlaceMode();
            }
        }
    }

    // Hero/Inventory/ClassUpgrade 버튼처럼 onClick이 이 스크립트 자신을 대상으로 하는 버튼은 여기서
    // CancelPlaceMode()를 걸지 않는다 - 선택(currentSelectedGameObject)은 포인터 "다운" 시점에 바뀌지만
    // onClick은 포인터 "업" 시점에야 실행되므로, 여기서 먼저 CancelPlaceMode()로 인벤토리를 재오픈해버리면
    // 뒤이어 실행되는 그 버튼의 onClick(OnInventoryButton 등)이 "이미 열려 있다"고 보고 도로 꺼버린다.
    // 이런 버튼들은 이미 자기 onClick 안에서 ExitPlaceModeIfActive()로 모드를 정리하므로 폴링이 안 끼어들어도 된다.
    private bool TargetsThisPanel(Button button)
    {
        var onClick = button.onClick;
        int count = onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (ReferenceEquals(onClick.GetPersistentTarget(i), this)) return true;
        }

        // 인벤토리 버튼처럼 onClick의 persistent call이 아니라 UIButtonHeld.Toggle()(런타임 AddListener)로
        // 직접 자기 패널을 여닫는 버튼도 있다 - 이런 경우 persistent call에는 안 잡히므로 watchedPanel이
        // 이 스크립트가 관리하는 패널(heroPanel/heroInventory/classUpgradePanel)과 같은지로 판단한다.
        UIButtonHeld heldButton = button.GetComponent<UIButtonHeld>();
        if (heldButton != null && IsManagedPanel(heldButton.WatchedPanel)) return true;

        return false;
    }

    private bool IsManagedPanel(GameObject panel)
    {
        return panel == heroPanel || panel == heroInventory || panel == classUpgradePanel;
    }

    // 재배치/제거 모드 중 다른 패널 버튼을 쓰면 그 모드를 끈다 - 클릭은 EventSystem의 선택 변경으로
    // Update()가 감지해 자동으로 꺼지지만, 단축키는 선택을 바꾸지 않아 그 감지를 타지 않는다.
    // 두 입력 경로의 결과가 갈리지 않도록 여기서 직접 꺼준다.
    // 여기서는 ReopenAfterPlaceMode()를 부르지 않는다 - Hero/Inventory/ClassUpgrade 버튼이 이걸 거쳐
    // 다른 패널을 열려는 것이므로, 자동으로 닫아뒀던 패널을 도로 여는 건 그 의도와 어긋난다.
    private void ExitPlaceModeIfActive()
    {
        if (view.IsReplacing || view.IsRemoving) view.ClearMode();
    }

    // ESC/토글 버튼/다른 버튼 클릭 등으로 재배치·제거 모드를 취소할 때 공통으로 쓴다 - 모드를 끄고,
    // CloseForPlaceMode()가 자동으로 닫아뒀던 인벤토리·강화 패널이 있으면 다시 연다.
    private void CancelPlaceMode()
    {
        view.ClearMode();
        ReopenAfterPlaceMode();
    }

    public void OnHeroButton()
    {
        ExitPlaceModeIfActive();

        if (heroPanel.activeSelf)
        {
            SetPanelOpen(heroPanel, false);
        }
        else
        {
            SetPanelOpen(heroPanel, true);
            heroPanelCloser.MarkOpened();
            if (heroInventory.activeSelf) SetPanelOpen(heroInventory, false);
            if (classUpgradePanel.activeSelf)
            {
                SetPanelOpen(classUpgradePanel, false);
            }
        }
    }

    public void OnRemoveButton()
    {
        if (view.IsRemoving)
        {
            CancelPlaceMode();
        }
        else
        {
            CloseForPlaceMode();
            view.SetRemove();
        }
    }

    public void OnReplaceButton()
    {
        if (view.IsReplacing)
        {
            CancelPlaceMode();
        }
        else
        {
            CloseForPlaceMode();
            view.SetReplace();
        }
    }

    // 재배치/제거 모드로 들어가는 동안 인벤토리·강화 패널이 열려 있었다면 닫아두고, 모드가 끝나면
    // 다시 열 수 있게(ReopenAfterPlaceMode) 기억해둔다. 항상 지금 상태로 덮어써야 한다 - Hero/Inventory/
    // ClassUpgrade 버튼으로 모드를 빠져나간 직전 시도(ExitPlaceModeIfActive, 다시 열지 않음)가 플래그를
    // true로 남겨뒀을 수 있으므로, 매번 새로 진입할 때 실제 상태로 재계산해 그 잔여값을 지운다.
    private void CloseForPlaceMode()
    {
        reopenInventoryAfterMode = heroInventory.activeSelf;
        if (reopenInventoryAfterMode) SetPanelOpen(heroInventory, false);

        reopenClassUpgradeAfterMode = classUpgradePanel.activeSelf;
        if (reopenClassUpgradeAfterMode) SetPanelOpen(classUpgradePanel, false);
    }

    // CloseForPlaceMode()가 닫아뒀던 패널을 재배치/제거 모드가 끝난 시점에 되돌린다.
    private void ReopenAfterPlaceMode()
    {
        if (reopenInventoryAfterMode)
        {
            reopenInventoryAfterMode = false;
            OnInventoryButton();
        }
        if (reopenClassUpgradeAfterMode)
        {
            reopenClassUpgradeAfterMode = false;
            OnClassUpgradeButton();
        }
    }

    public void OnInventoryButton()
    {
        ExitPlaceModeIfActive();

        if (heroInventory.activeSelf)
        {
            SetPanelOpen(heroInventory, false);
        }
        else
        {
            OpenInventory();
        }
    }

    public void OpenInventory()
    {
        if (heroInventory.activeSelf) return;

        // heroInventory에 PanelReveal이 붙어 있으면 그쪽이 이미 0->1 스케일 연출을 담당하므로 PanelPopIn을
        // 또 돌리지 않는다 - 같은 localScale을 두 애니메이션이 동시에 건드리면 서로의 값을 덮어써 버린다.
        bool hasReveal = heroInventory.GetComponent<PanelReveal>() != null;
        SetPanelOpen(heroInventory, true);
        if (!hasReveal) PanelPopIn.Play((RectTransform)heroInventory.transform);
        inventoryCloser.MarkOpened();
        if (classUpgradePanel.activeSelf)
        {
            SetPanelOpen(classUpgradePanel, false);
        }
        if (heroPanel.activeSelf) SetPanelOpen(heroPanel, false);
    }

    public void OnClassUpgradeButton()
    {
        if (TutorialInputGate.BlockHeroUpgradeOpen) return;

        ExitPlaceModeIfActive();

        if (classUpgradePanel.activeSelf)
            SetPanelOpen(classUpgradePanel, false);
        else
        {
            SetPanelOpen(classUpgradePanel, true);
            classUpgradeCloser.MarkOpened();
            if (heroInventory.activeSelf) SetPanelOpen(heroInventory, false);
            if (heroPanel.activeSelf) SetPanelOpen(heroPanel, false);
        }
    }

    public void OnOffButton()
    {
        CancelPlaceMode();
    }

    // 튜토리얼이 재배치/제거 완료를 감지하고 모드를 끝낼 때 쓴다 - placePalette.ClearMode()를 직접
    // 부르면 CloseForPlaceMode()가 닫아둔 인벤토리/강화 패널이 영영 다시 열리지 않으므로, 반드시
    // 이 경로(CancelPlaceMode)를 거쳐 ReopenAfterPlaceMode()가 같이 실행되게 한다.
    public void CancelPlaceModeFromTutorial()
    {
        CancelPlaceMode();
    }
}
