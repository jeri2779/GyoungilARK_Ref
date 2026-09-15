using UnityEngine;

// 슬롯 선택 패널이 "불러오기 모드"로 열려 있는 동안 이 버튼을 눌린 모양(+선택색)으로 붙잡아 둔다.
// 시작/불러오기 버튼이 같은 패널 하나를 같이 쓰기 때문에, 패널이 켜져 있는지뿐 아니라
// 지금 모드가 불러오기인지까지 함께 확인한다.
public class LoadHeldLink : HeldLinkBase
{
    [SerializeField] private SlotSelectPanel slotSelectPanel;

    protected override void OnEnable()
    {
        base.OnEnable();
        slotSelectPanel.ModeChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        slotSelectPanel.ModeChanged -= Refresh;
    }

    // 패널이 켜져 있고, 지금 모드가 불러오기인지 함께 확인한다
    protected override bool CheckHeld()
    {
        return slotSelectPanel.gameObject.activeSelf && slotSelectPanel.Mode == SlotSelectMode.Load;
    }
}
