using System.Collections.Generic;
using UnityEngine;

// 액티브 스킬이 실제로 때리는 범위를 계산해 돌려준다. 표시하지는 않는다.
public static class SkillRangeCalc
{
    public static List<Tile> BuildHitRange(HeroActiveSkill skill, Tile origin)
    {
        ResolveZoneShape(skill.groundZonePrefab, out int radius, out RangeShape shape);
        return TileShapeQuery.GetTiles(origin.Board, origin.Coord, radius, shape);
    }

    private static void ResolveZoneShape(GameObject zonePrefab, out int radius, out RangeShape shape)
    {
        radius = 0;
        shape = RangeShape.Diamond;

        if (HasZonePrefab(zonePrefab))
        {
            ReadZone(zonePrefab, out radius, out shape);
        }
    }

    private static bool HasZonePrefab(GameObject zonePrefab)
    {
        return zonePrefab != null;
    }

    private static void ReadZone(GameObject zonePrefab, out int radius, out RangeShape shape)
    {
        radius = 0;
        shape = RangeShape.Diamond;

        if (zonePrefab.TryGetComponent(out GroundZoneEffect zone))
        {
            radius = zone.radius;
            shape = zone.shape;
        }
    }

    public static List<Tile> BuildHitRange(PlayerSkillSlot skill, Tile origin)
    {
        ResolvePlayerZoneShape(skill?.zonePrefab, out int radius, out RangeShape shape);
        return TileShapeQuery.GetTiles(origin.Board, origin.Coord, radius, shape);
    }

    private static void ResolvePlayerZoneShape(GameObject zonePrefab, out int radius, out RangeShape shape)
    {
        radius = 0;
        shape = RangeShape.Diamond;

        if (zonePrefab != null && zonePrefab.TryGetComponent(out PlayerGroundZoneEffect zone))
        {
            radius = zone.radius;
            shape = zone.shape;
        }
    }
}
