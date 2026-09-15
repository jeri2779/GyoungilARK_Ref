using System.Collections.Generic;

// 단축키로 열리는 최상위 패널(메뉴/도감/가이드 등)이 서로의 존재를 몰라도 배타적으로 동작하게 해준다.
// 패널은 열릴 때 NotifyOpened, 닫힐 때 NotifyClosed만 호출하면 되고, 다른 패널을 인스펙터로
// 서로 참조해 닫아줄 필요가 없다. UiPanelStack과 달리 "누가 열렸는지"만 보고 즉시 나머지를
// 전부 닫는 단순 배타 그룹이다(우선순위 스택이 아님).
public interface IExclusiveUiPanel
{
    void RequestClose();
}

// 메뉴/가이드 같은 오버레이가 열려도 자기는 닫히지 않아야 하는 패널(거점 화면 등)이 구현한다.
// 이 패널은 계속 openPanels에 등록되어 있으므로 "자기가 열릴 때 다른 패널들을 닫는" 동작은 그대로 하되,
// "다른 패널이 열릴 때 자기가 닫히는" 동작만 면제된다.
public interface IPersistentAcrossExclusivePanels
{
}

public static class ExclusiveUiCoordinator
{
    private static readonly List<IExclusiveUiPanel> openPanels = new();

    public static void NotifyOpened(IExclusiveUiPanel self)
    {
        // ToArray로 스냅샷을 떠서 순회한다 - RequestClose 안에서 NotifyClosed가 호출되며
        // openPanels 리스트 자체가 변경되므로, 원본을 그대로 순회하면 예외가 난다.
        foreach (IExclusiveUiPanel panel in openPanels.ToArray())
        {
            if (ReferenceEquals(panel, self)) continue;
            if (panel is IPersistentAcrossExclusivePanels) continue; // 얘는 다른 패널이 열려도 안 닫힌다
            panel.RequestClose();
        }

        if (!openPanels.Contains(self)) openPanels.Add(self);
    }

    public static void NotifyClosed(IExclusiveUiPanel self) => openPanels.Remove(self);

    // self를 제외하고 다른 배타 패널이 하나라도 열려있는지 - IPersistentAcrossExclusivePanels 패널이
    // "내 위에 메뉴/가이드 같은 게 떠 있는 동안은 ESC/바깥클릭으로 스스로 안 닫히기"를 판단하는 데 쓴다.
    public static bool HasOtherOpen(IExclusiveUiPanel self)
    {
        foreach (IExclusiveUiPanel panel in openPanels)
            if (!ReferenceEquals(panel, self)) return true;
        return false;
    }
}
