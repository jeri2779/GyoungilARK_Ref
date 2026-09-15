using UnityEngine;
using UnityEngine.EventSystems;

// StageInfoView/StageEnemyInfoView 둘 다 이 인터페이스로 SetPointerOverTooltip을 받는다 -
// StageTooltipPointer가 구체 타입 하나에 묶이지 않도록 뽑아둔 것.
public interface IStageTooltipHost
{
    void SetPointerOverTooltip(bool over);
}

// 툴팁 위에 커서가 있는지를 IStageTooltipHost(StageInfoView/StageEnemyInfoView)에 알린다.
//
// 왜 필요한가: 툴팁 안의 '도감 열기' 버튼을 누르려면 커서를 행에서 툴팁으로 옮겨야 한다.
// 그런데 행을 벗어나는 순간 툴팁이 닫히면 버튼을 누를 수가 없다.
// 그래서 '툴팁 위에 있으면 닫지 않는다'로 막는다.
//
// PointerEnter/Exit는 자식에서 일어나도 부모까지 전달되므로 툴팁 루트에 하나만 있으면 된다.
// 호스트의 Awake가 직접 붙이고 Bind하므로 인스펙터 작업은 필요 없다.
public class StageTooltipPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private IStageTooltipHost view;

    public void Bind(IStageTooltipHost view) => this.view = view;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (view != null) view.SetPointerOverTooltip(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (view != null) view.SetPointerOverTooltip(false);
    }
}
