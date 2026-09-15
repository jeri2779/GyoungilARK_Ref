using UnityEngine;

// 집어 든 유닛 하나와 그 유닛에 딸린 값들.
public readonly struct HeldData
{
    // 판에서 뗀 채 포인터를 따라가는 유닛.
    public readonly GameObject Unit;

    // 집어 든 출발 칸.
    public readonly Tile FromTile;

    // 집어 들기 전에 덮고 있던 자리(취소 시 그대로 되돌리는 데 쓴다).
    public readonly PlacementArea FromArea;

    // 집어 들기 전 유닛의 월드 위치(취소 시 그대로 되돌리는 데 쓴다).
    public readonly Vector3 FromPosition;

    public readonly OccupantKind Kind;

    // 덮고 있던 칸 수.
    public readonly Vector2Int Size;

    public HeldData(
        GameObject unit,
        Tile fromTile,
        PlacementArea fromArea,
        Vector3 fromPosition,
        OccupantKind kind,
        Vector2Int size)
    {
        Unit = unit;
        FromTile = fromTile;
        FromArea = fromArea;
        FromPosition = fromPosition;
        Kind = kind;
        Size = size;
    }
}
