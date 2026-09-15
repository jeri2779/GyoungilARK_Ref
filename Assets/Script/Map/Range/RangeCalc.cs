using System.Collections.Generic;
using UnityEngine;

// 타일 위에 선 유닛이 닿는 칸들을 계산해 돌려준다. 보관하거나 표시하지는 않는다.
public class RangeCalc
{
    private readonly RangeInfo rangeInfo;

    public RangeCalc(RangeInfo rangeInfo)
    {
        this.rangeInfo = rangeInfo;
    }

    public bool TryGetRange(Tile unitTile, out List<Tile> range)
    {
        range = new List<Tile>();

        if (!HasTile(unitTile))
        {
            return false;
        }

        if (unitTile.IsCampfire)
        {
            return TryGetCampfireRange(unitTile, out range);
        }

        if (unitTile.IsWindwall)
        {
            return TryGetWindwallRange(unitTile, out range);
        }

        return TryGetRangeAtCenter(unitTile.OccupantObject, unitTile, out range);
    }

    // 클릭한 칸의 범위를 무엇으로 그릴지 가른다. 유닛 사거리처럼 채워 그릴 범위는 None이다.
    public static RangeEdgeKind ResolveEdgeKind(Tile tile)
    {
        if (tile.IsCampfire)
        {
            return RangeEdgeKind.Campfire;
        }

        if (tile.IsWindwall)
        {
            return RangeEdgeKind.Windwall;
        }

        return RangeEdgeKind.None;
    }

    // 모닥불 칸이면 유닛을 찾지 않고, 그 모닥불이 미리 계산해 둔 자기 범위를 그대로 가져온다.
    private static bool TryGetCampfireRange(Tile campfireTile, out List<Tile> range)
    {
        range = new List<Tile>();
        IceZone iceZone = campfireTile.Board.GetComponent<IceZone>();

        if (iceZone == null)
        {
            return false;
        }

        return iceZone.CampfireData.TryGetRange(campfireTile, out range);
    }

    // 가림막 칸이면 유닛을 찾지 않고, 그 가림막이 미리 계산해 둔 자기 범위를 그대로 가져온다.
    private static bool TryGetWindwallRange(Tile windwallTile, out List<Tile> range)
    {
        range = new List<Tile>();
        DesertZone desertZone = windwallTile.Board.GetComponent<DesertZone>();

        if (desertZone == null)
        {
            return false;
        }

        return desertZone.WindwallData.TryGetRange(desertZone.WindDirection, windwallTile.Coord, out range);
    }

    // 아직 타일에 놓이지 않은 유닛(배치 프리뷰)도 중심 타일을 따로 받아 계산한다.
    public bool TryGetRangeAtCenter(GameObject unit, Tile center, out List<Tile> range)
    {
        range = new List<Tile>();

        if (rangeInfo.TryGet(unit, out int reach, out RangeShape shape))
        {
            range = TileShapeQuery.GetTiles(center.Board, center.Coord, reach, shape);
            return true;
        }

        return false;
    }

    private static bool HasTile(Tile tile)
    {
        return tile != null;
    }
}
