using UnityEngine;

// 한 번 놓는 데 필요한 값 묶음. 아무것도 하지 않고 들고만 있는다.
// 미리보기와 확정이 같은 값을 읽는다 — 각자 계산하면 둘이 어긋난다.
public readonly struct PlaceData
{
    // 덮는 칸들.
    public readonly PlacementArea Area;

    // 유닛이 설 자리. 받는 쪽은 그대로 대입한다.
    public readonly Vector3 Position;

    // 지형·점유 판정. 자원·낮밤·로스터 게이트는 담지 않는다.
    public readonly bool CanPlace;

    public PlaceData(
        PlacementArea area,
        Vector3 position,
        bool canPlace)
    {
        Area = area;
        Position = position;
        CanPlace = canPlace;
    }
}
