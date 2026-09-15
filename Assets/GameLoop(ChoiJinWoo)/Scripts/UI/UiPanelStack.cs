using System.Collections.Generic;

// 거점 UI(지역 오버뷰/디테일/건설 선택/건물 정보/중앙 허브)가 공유하는 열림 순서 스택.
// 패널은 열릴 때(OnEnable) Push, 닫힐 때(OnDisable) Remove만 하면 된다. ESC/바깥클릭은 각 패널이
// 자기 Update()에서 IsTop(this)로 "내가 제일 위인지"를 직접 확인하고 스스로 닫는다 - 한 곳(예:
// 특정 패널의 Update)에만 ESC 감지를 몰아두면 그 패널이 비활성 상태일 때 스택 전체가 ESC에
// 반응하지 않게 되므로, 각자 책임지는 방식으로 통일했다.
public class UiPanelStack
{
    private readonly List<IClosablePanel> stack = new();

    // ESC로 메뉴를 열지 말지 판단할 때 쓴다(UiManager) - 이 스택에 뭐라도 떠 있으면 ESC는
    // 메뉴를 여는 대신 그 패널부터 닫아야 하므로.
    public bool HasAny => stack.Count > 0;

    public void Push(IClosablePanel panel)
    {
        stack.Remove(panel); // 이미 있으면 맨 위로 옮긴다(중복 방지)
        stack.Add(panel);
    }

    public void Remove(IClosablePanel panel)
    {
        stack.Remove(panel);
    }

    // 스택 제일 위에 있는 패널인지 - 각 패널이 자기 Update()에서 "나만 ESC/바깥클릭에 반응해야
    // 하는지"를 판단하는 데 쓴다(내 위에 다른 패널이 떠 있으면 그쪽이 먼저 닫혀야 한다).
    public bool IsTop(IClosablePanel panel)
    {
        return stack.Count > 0 && ReferenceEquals(stack[^1], panel);
    }
}
