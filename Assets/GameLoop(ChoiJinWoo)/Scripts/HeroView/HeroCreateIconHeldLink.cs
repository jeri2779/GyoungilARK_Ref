using UnityEngine;

// HeroCreateAmountPanel처럼 근접/원거리 아이콘 2개가 패널 하나를 같이 쓸 때,
// 패널이 켜져 있는지뿐 아니라 그 패널을 연 게 나 자신인지까지 함께 확인해서 눌린 모양을 유지한다.
// 패널 하나 : 버튼 하나뿐인 일반 케이스는 ButtonHeldLink를 그대로 쓴다.
public class HeroCreateIconHeldLink : HeldLinkBase
{
    [SerializeField] private GameObject watchedPanel;
    [SerializeField] private HeroSetPanel ownerSource;
    [SerializeField] private HeroCreateIcon ownerIdentity;
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
        ownerSource.OpenIconChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        notifier.ActiveChanged -= OnWatchedActiveChanged;
        ownerSource.OpenIconChanged -= Refresh;
    }

    private void OnWatchedActiveChanged(bool active) => Refresh();

    // 패널이 켜져 있고, 그 패널을 연 게 나 자신인지 함께 확인한다
    protected override bool CheckHeld()
    {
        return watchedPanel.activeSelf && ownerSource.OpenIcon == ownerIdentity;
    }
}
