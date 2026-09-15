using UnityEngine;

// 지역 한 칸의 상태(비어있음/건물 하나)만 담는다. 위치·좌표 개념은 없다.
// Occupant는 ProductionFacility 또는 House(둘 다 POCO) - GameObject가 아니다.
public class RegionFacilitySlot
{
    public object Occupant { get; private set; }
    public Sprite Icon { get; private set; }
    public bool IsEmpty => Occupant == null;

    // 표시 이름은 여기서 굽지 않는다 - Occupant(BasicValue/Config)에서 매번 StringTable로 다시
    // 조회해야 언어를 바꿨을 때도 최신 번역으로 보인다.
    public void Assign(object occupant, Sprite icon)
    {
        Occupant = occupant;
        Icon = icon;
    }

    public void Clear()
    {
        Occupant = null;
        Icon = null;
    }
}
