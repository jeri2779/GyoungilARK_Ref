using UnityEngine;

// 제거 모드가 켜져 있는 동안 이 버튼을 눌린 모양(+선택색)으로 붙잡아 둔다.
public class RemoveHeldLink : HeldLinkBase
{
    [SerializeField] private MapView view;

    protected override void OnEnable()
    {
        base.OnEnable();
        view.OnStateChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        view.OnStateChanged -= Refresh;
    }

    // 제거 모드 여부로 눌림 여부를 판단한다
    protected override bool CheckHeld()
    {
        return view.IsRemoving;
    }
}
