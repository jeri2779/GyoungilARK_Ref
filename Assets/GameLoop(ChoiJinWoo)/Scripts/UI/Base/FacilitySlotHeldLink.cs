using UnityEngine;

// 건설 슬롯 버튼을 그 슬롯의 BuildingPanel/ChoicePanel이 열려있는 동안 눌린 모양으로 유지한다.
public class FacilitySlotHeldLink : HeldLinkBase
{
    [SerializeField] private BuildingPanel buildingPanel;
    [SerializeField] private FacilityBuildChoicePanel choicePanel;
    [SerializeField] private FacilitySlotView slotView;

    protected override void OnEnable()
    {
        base.OnEnable();
        buildingPanel.StateChanged += Refresh;
        choicePanel.StateChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        buildingPanel.StateChanged -= Refresh;
        choicePanel.StateChanged -= Refresh;
    }

    // 지어진 칸 상세창이나 빈 칸 건설 선택창 중 이 슬롯 번호로 열린 게 있는지로 판단한다
    protected override bool CheckHeld()
    {
        if (buildingPanel.gameObject.activeSelf && buildingPanel.SlotIndex == slotView.Index) return true;
        if (choicePanel.gameObject.activeSelf && choicePanel.SlotIndex == slotView.Index) return true;
        return false;
    }
}
