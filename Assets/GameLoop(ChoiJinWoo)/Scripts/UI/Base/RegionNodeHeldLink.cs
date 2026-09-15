using UnityEngine;

// 지역 노드 버튼 하나를 자기 지역이 RegionDetailPanel에 열려있는 동안만 눌린 모양(+선택색)으로 유지한다.
// 여러 노드가 같은 RegionDetailPanel을 공유하므로, UIButtonHeld처럼 패널의 activeSelf만 보면
// 아무 지역이나 열려있을 때 모든 노드가 같이 선택된 것처럼 보인다 - 그래서 moduleId를 직접 비교한다.
public class RegionNodeHeldLink : HeldLinkBase
{
    [SerializeField] private RegionDetailPanel detailPanel;
    [SerializeField] private RegionNodeView node;

    protected override void OnEnable()
    {
        base.OnEnable();
        detailPanel.RegionChanged += Refresh;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        detailPanel.RegionChanged -= Refresh;
    }

    protected override bool CheckHeld()
        => detailPanel.CurrentRegion != null && detailPanel.CurrentRegion.ModuleId == node.ModuleId;
}
