using UnityEngine;

// 포인터가 가리키는 자리를 찾아준다. 계산만 하고 판을 바꾸지 않는다.
public class PlaceFinder
{
    private readonly PointerPick pointerPick;
    private readonly PlacePalette palette;
    private readonly float yOffset;

    public PlaceFinder(
        PointerPick pointerPick,
        PlacePalette palette,
        float yOffset)
    {
        this.pointerPick = pointerPick;
        this.palette = palette;
        this.yOffset = yOffset;
    }

    // 종류와 크기로 자리를 찾는다. 가리키는 자리가 없으면 false.
    public bool TryResolve(
        OccupantKind kind,
        Vector2Int size,
        out PlaceData data)
    {
        PlacementArea area = pointerPick.GetArea(size);
        Vector3 position = AreaPlace.Position(area, kind, yOffset, out bool canPlace);
        data = new PlaceData(area, position, canPlace);
        return true;
    }

    // 팔레트가 고른 슬롯으로 자리를 찾는다. 슬롯이 비었으면 false.
    public bool TryResolveSlot(
        out Placeable slot,
        out PlaceData data)
    {
        data = default;

        if (!palette.TryCurrentSlot(out slot))
        {
            return false;
        }

        return TryResolve(slot.kind, PlaceSize.GetSize(slot), out data);
    }
}
