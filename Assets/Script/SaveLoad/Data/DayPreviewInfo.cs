
// 일차 한 줄의 미리보기 정보.
public readonly struct DayPreviewInfo
{
    public readonly int DayCount;
    public readonly float PlayTime;
    public readonly string SaveTime;
    public readonly int HeroCount;

    public DayPreviewInfo(int dayCount, float playTime, string saveTime, int heroCount)
    {
        DayCount = dayCount;
        PlayTime = playTime;
        SaveTime = saveTime;
        HeroCount = heroCount;
    }
}
