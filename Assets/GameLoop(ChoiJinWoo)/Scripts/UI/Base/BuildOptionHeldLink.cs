using UnityEngine;

// 건설 종류 아이콘을 그 종류가 선택되어 정보창이 열려있는 동안 눌린 모양으로 유지한다.
public class BuildOptionHeldLink : HeldLinkBase
{
    [SerializeField] private FacilityBuildChoicePanel choicePanel;
    [SerializeField] private BuildOptionView optionView;

    protected override void OnEnable()
    {
        base.OnEnable();
        choicePanel.StateChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        choicePanel.StateChanged -= Refresh;
    }

    // 선택 정보창이 열려있고, 그 대상이 이 아이콘인지로 눌림 여부를 판단한다
    protected override bool CheckHeld()
    {
        return choicePanel.gameObject.activeSelf && choicePanel.IsInfoOpen && choicePanel.SelectedIndex == optionView.Index;
    }
}
