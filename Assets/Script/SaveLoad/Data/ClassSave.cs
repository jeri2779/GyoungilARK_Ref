using System;

// 클래스별(근접/원거리) 공용 강화 레벨
[Serializable]
public class ClassSave
{
    public int heroType;    // 0=근접, 1=원거리
    public int classLevel;  // 그 클래스가 몇 단계 강화됐는지
}
