using System;

// 영웅 티어별 공용 강화 레벨
[Serializable]
public class TierSave
{
    public int heroTier;    // 몇 번째 티어인지
    public int tierLevel;   // 그 티어가 몇 단계 강화됐는지
}
