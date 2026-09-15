using UnityEngine;

// 지정한 패널이 열려 있는 동안 이 버튼을 눌린 모양(+선택색)으로 붙잡아 둔다.
public class ButtonHeldLink : HeldLinkBase
{
    [SerializeField] private GameObject watchedPanel;
    private PanelActivityNotifier notifier;

    protected override void Awake()
    {
        base.Awake();
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
}
