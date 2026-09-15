using UnityEngine;

// 사막 지대 전용 — 고지·유닛·가림막 셋을 합쳐 최종 노출 여부를 판정합니다.
public static class DesertShelterQuery
{
    // 셋 중 하나라도 막으면 보호입니다. 셋 다 막지 못했을 때만 노출입니다.
    public static bool IsUnsheltered(
        WindShelterData terrain,
        UnitShelter units,
        WindwallData walls,
        Tile tile,
        Vector2Int wind)
    {
        WindShelter tileShelter = terrain.ReadShelter(tile);
        bool highShelter = WindShelterQuery.IsSheltered(tileShelter, wind);
        bool unitBlock = units.IsSheltered(tile, wind);
        bool wallBlock = walls.HasArm(wind, tile.Coord);
        return !highShelter && !unitBlock && !wallBlock;
    }
}
