using UnityEngine;
using UnityEngine.UI;

// 지정한 패널이 열려 있는 동안 이 버튼을 눌린 모양(+선택색)으로 붙잡아 두고, 이 버튼 클릭으로
// 그 패널을 직접 열고 닫는다(토글). 패널에 PanelReveal이 붙어 있으면 그 스케일 연출로, 없으면
// SetActive로. ButtonHeldLink의 UI 전용(Animator 없는 새 버튼) 버전.
public class UIButtonHeld : HeldLinkBase
{
    [SerializeField] private GameObject watchedPanel;

    // BuildModePanel.Update()가 재배치/제거 모드 중 "다른 버튼 클릭"을 감지할 때, 이 버튼이 실제로
    // 여닫는 패널이 BuildModePanel 자신이 관리하는 패널(heroPanel/heroInventory/classUpgradePanel)과
    // 같은지 확인하는 데 쓴다 - 이 버튼은 onClick의 persistent call이 아니라 여기서 런타임에 붙이는
    // Toggle()로만 패널을 여닫아서, persistent call 목록만 봐서는 잡히지 않기 때문이다.
    public GameObject WatchedPanel => watchedPanel;

    private Button button;
    private PanelReveal panelReveal;
    private PanelActivityNotifier notifier;

    protected override void Awake()
    {
        base.Awake();
        button = GetComponent<Button>();
        panelReveal = watchedPanel.GetComponent<PanelReveal>();
        if (button != null) button.onClick.AddListener(Toggle);

        // watchedPanel은 전용 스크립트가 없는 순수 GameObject라, 활성 변화를 이벤트로 받으려면
        // 이 컴포넌트를 자동으로 붙여야 한다 (BuildModePanel.Awake()의 ExclusivePanelPresence와 동일 기법).
        notifier = watchedPanel.GetComponent<PanelActivityNotifier>();
        if (notifier == null) notifier = watchedPanel.AddComponent<PanelActivityNotifier>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        notifier.ActiveChanged += OnWatchedActiveChanged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        notifier.ActiveChanged -= OnWatchedActiveChanged;
    }

    private void OnWatchedActiveChanged(bool active) => Refresh();

    // 패널이 켜져 있는지로 눌림 여부를 판단한다
    protected override bool CheckHeld()
    {
        return watchedPanel.activeSelf;
    }

    // 클릭할 때마다 패널을 토글한다 - 열려 있으면 닫고, 닫혀 있으면 연다.
    public void Toggle()
    {
        bool isOpen = watchedPanel.activeSelf;

        if (panelReveal != null)
        {
            if (isOpen) panelReveal.Hide();
            else panelReveal.Show();
        }
        else
        {
            watchedPanel.SetActive(!isOpen);
        }
    }
}
