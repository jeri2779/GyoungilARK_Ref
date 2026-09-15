// DayStart / NightReady / DayActive 세 저장 단계를 구분하는 enum
public enum SavePhase
{
    DayStart,    // 밤이 끝나고 새 낮이 시작된 직후
    NightReady,  // 낮 준비를 마치고 밤으로 넘어가기 직전
    DayActive    // 생산이 끝나고 낮 활동을 진행하던 도중
}
