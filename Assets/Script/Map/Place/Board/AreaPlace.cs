using UnityEngine;

// 계산된 자리를 실제 판에 반영하는 담당.
public static class AreaPlace
{
    public static bool CanPlace(
        PlacementArea area,
        OccupantKind kind)
    {
        foreach (Vector2Int cell in area.Cells)
        {
            if (!area.Board.CanPlace(cell, kind))
            {
                return false;
            }
        }

        return true;
    }

    // 유닛 몸통이 설 월드 지점. 이 자리에 놓을 수 있는지도 같은 순회에서 함께 알아낸다.
    public static Vector3 Position(
        PlacementArea area,
        OccupantKind kind,
        float yOffset,
        out bool canPlace)
    {
        Vector3 position = area.Center;
        position.y = TopY(area, kind, out canPlace) + yOffset;
        return position;
    }

    // 이 칸의 윗면. 놓을 수 없으면 자리 한가운데 높이로 대신한다.
    private static float TopY(PlacementArea area, OccupantKind kind, out bool canPlace)
    {
        canPlace = area.Board.CanPlace(area.Origin, kind);
        if (!canPlace)
        {
            return area.Center.y;
        }
        return area.Board.Cells[area.Origin].WorldTop.y;
    }

    // 그 칸을 채우고 유닛을 한가운데 세운다.
    public static void Place(
        PlaceData data,
        GameObject unit,
        OccupantKind kind)
    {
        Tile tile = data.Area.Board.Cells[data.Area.Origin];
        tile.SetOccupant(unit, kind);
        FireReceiver.ReceiveEntry(tile, unit.transform);

        unit.transform.position = data.Position;
    }

    // 그 칸을 비운다.
    public static void Remove(PlacementArea area)
    {
        Tile tile = area.Board.Cells[area.Origin];
        GameObject unit = tile.ClearOccupant();
        FireReceiver.ReceiveExit(tile, unit.transform);
    }
}
