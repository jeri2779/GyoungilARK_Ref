// 타이틀에서 고른 슬롯 번호와 새 게임 여부

public static class SelectedSaveSlot
{
    public const int NoSelectedDay = 0;

    public static int SlotId { get; private set; } = 1;
    public static bool IsNewGame { get; private set; } = true;
    public static int SelectedDay { get; private set; } = NoSelectedDay;

    // 새 게임으로 진입할 슬롯을 지정한다.
    public static void SetNewGame(int slotId)
    {
        SlotId = slotId;
        IsNewGame = true;
        SelectedDay = NoSelectedDay;
    }

    // 불러오기로 진입할 슬롯을 지정한다 (최신 저장본 기준).
    public static void SetLoad(int slotId)
    {
        SlotId = slotId;
        IsNewGame = false;
        SelectedDay = NoSelectedDay;
    }

    // 불러오기로 진입할 슬롯과 과거 일차를 지정한다.
    public static void SetLoadDay(int slotId, int dayCount)
    {
        SlotId = slotId;
        IsNewGame = false;
        SelectedDay = dayCount;
    }
}
