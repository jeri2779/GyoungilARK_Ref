using System.Collections.Generic;
using UnityEngine;

// 공중(지형 무시) 격자 길찾기.
// 지상 길찾기와 동일한 A*(Pathfinder)지만 Walkable 제약을 빼서 벽·고지 위로 넘어간다.
// MapBoard는 공개 API(TryGetCell/WorldToCell)만 읽고 절대 수정하지 않는다(맵은 다른 담당 영역).
public static class FlyingPathfinder
{
    /// <summary>startWorld→goalWorld 를 잇는 격자 경로(웨이포인트). 경로 없으면 빈 리스트.
    /// 지형(언덕) 높이를 무시하고 시작 지점 높이 + flightHeight로 Y를 고정 — 공중 유닛이 오르내리지 않게.</summary>
    public static List<Vector3> BuildWaypoints(MapBoard board, Vector3 startWorld, Vector3 goalWorld, float flightHeight = 0f)
    {
        var list = new List<Vector3>();
        if (board == null) return list;

        Vector2Int goalCell = board.WorldToCell(goalWorld);
        if (!board.TryGetCell(board.WorldToCell(startWorld), out Tile startTile)) return list;
        List<Tile> path = Pathfinder.FindPath(
            new[] { startTile },
            tile => tile.Coord == goalCell,
            PassInner,
            tile => GridCalculator.GetDistance(tile.Coord, goalCell));

        if (path == null) return list;
        float flightY = startWorld.y + flightHeight;
        foreach (Tile t in path)
        {
            Vector3 top = t.WorldTop;
            list.Add(new Vector3(top.x, flightY, top.z));
        }
        return list;
    }
    
    // 공중 유닛은 지형(언덕·벽)을 가리지 않지만, 외곽 장식 줄은 판 밖이라 지나지 않는다.
    private static bool PassInner(Tile tile)
    {
        return !tile.IsSpecial;
    }
}
