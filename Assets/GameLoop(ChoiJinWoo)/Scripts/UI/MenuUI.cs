#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

public class MenuUI : MonoBehaviour, IExclusiveUiPanel
{
    [SerializeField] private SettingUI settingPanel;
    [SerializeField] private GameObject QuitAlert;
    [SerializeField] private Button openButton;

    private ClickOutsideCloser outsideCloser;
    private SaveManager saveManager;
    private PanelReveal panelReveal;

    // 나가기 직전 저장을 맡길 저장 관리자를 받아 둔다
    [Inject]
    private void Construct(SaveManager saveManager)
    {
        this.saveManager = saveManager;
    }

    private void Awake()
    {
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, openButton != null ? openButton.transform : null);
        panelReveal = GetComponent<PanelReveal>();
    }

    // 단축키/버튼/바깥클릭 중 무엇으로 열고 닫히든 SetActive는 결국 여기를 거치므로,
    // ExclusiveUiCoordinator 등록 지점으로 쓴다.
    private void OnEnable()
    {
        ExclusiveUiCoordinator.NotifyOpened(this);
        outsideCloser.MarkOpened();
        // 같은 Canvas 안에서는 그리기 순서가 하이러키의 형제 순서를 따른다 - ConfirmPopup.ShowPopup()과
        // 같은 방식으로 맨 뒤로 보내 다른 UI보다 항상 위에 그려지게 한다.
        transform.SetAsLastSibling();
        // settingPanel은 이 메뉴의 내용물이라, 그 안의 X 버튼(SettingUI.OnClose)으로 설정만 닫히고
        // 메뉴 자체는 빈 채로 열려있는 채 남는 걸 막는다 - 설정이 닫히면 메뉴도 같이 닫는다.
        // SetActive(true)보다 먼저 구독해야 한다 - SettingUI.OnEnable()이 이 SetActive 호출 중에
        // 곧바로 실행되는데, 그쪽에서 "내가 상위 패널에 담겨 있는지"를 이 Closed 구독자 유무로
        // 판단하기 때문이다(임베드 상태면 자기 ExclusiveUiCoordinator 등록을 건너뛴다).
        settingPanel.Closed += OnCloseButton;
        settingPanel.gameObject.SetActive(true);
        PanelPopIn.Play((RectTransform)settingPanel.transform);
        QuitAlert.SetActive(false);
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;

        // 행/컬럼 크기를 매번 재계산하던 중첩 ContentSizeFitter는 크기를 고정값으로 박고 제거했다
        // (ContentSizeFitterFreezer.cs 참고) - 이제 SetActive 직후 남은 LayoutGroup들이 고정된
        // 크기 안에서 자식 위치만 정렬하면 되므로, 한 패스만 강제로 즉시 확정해도 충분하다.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)settingPanel.transform);
    }

    private void OnDisable()
    {
        ExclusiveUiCoordinator.NotifyClosed(this);
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;
        settingPanel.Closed -= OnCloseButton;
    }

    public void RequestClose() => OnCloseButton();

    private void HandleCloseCheck()
    {
        if (outsideCloser.ShouldClose()) OnCloseButton();
    }

    // 여는 쪽(아이콘의 UIButtonHeld.Toggle -> PanelReveal.Show)은 이미 서서히 열리는데, 닫는 쪽만
    // SetActive(false)로 즉시 꺼버리면 닫힐 때만 연출이 안 먹힌다 - 열기와 대칭으로 Hide()를 거친다.
    public void OnCloseButton()
    {
        if (panelReveal != null) panelReveal.Hide();
        else gameObject.SetActive(false);
    }

    public void OnQuitButton()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif

        Application.Quit();
    }

    public void OnQuitAlert()
    {
        QuitAlert.SetActive(true);
    }

    public void OnCancelQuit()
    {
        if (QuitAlert.activeSelf)
            QuitAlert.SetActive(false);
    }

    public void OnTitle()
    {
        saveManager.SaveDayActive();
        Time.timeScale = 1f;
        SceneManager.LoadScene("Title");
    }
}
