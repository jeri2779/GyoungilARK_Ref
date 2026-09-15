using System;

// 영웅 로스터 엔트리 — Guid·영웅ID·시민비용·대기/배치 상태
[Serializable]
public class HeroSave
{
    public string rosterId;              // 이 영웅 개체의 고유 번호
    public int unitId;                   // 어떤 종류의 영웅인지
    public int citizenCost;              // 이 영웅이 차지하는 시민 수
    public HeroRosterState rosterState;  // 대기 중인지 맵에 배치됐는지
}
