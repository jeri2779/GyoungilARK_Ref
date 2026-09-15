using System;

// 지역(모듈) ID · 상태 · 로컬 스테이지 오프셋
[Serializable]
public class RegionSave
{
    public int moduleId;              // 지역 고유 번호
    public ModuleState moduleState;   // 잠김/해금 상태
    public int stageOffset;
    public int unlockDay;
    public bool gimmickSeen;          // 이 지역 기믹 안내를 이미 봤는지
}
