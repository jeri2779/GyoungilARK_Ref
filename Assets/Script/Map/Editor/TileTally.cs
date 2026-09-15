using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모듈에 지형과 배치 허용이 각각 몇 칸인지 센다.
/// 창은 지금 고른 붓의 수를 늘 띄워야 하므로(무슨 모드인지 = 몇 칸인지) 세는 일을 여기로 모은다.
/// </summary>
public static class TileTally
{
    /// <summary>이 지형인 칸 수.</summary>
    public static int CountTerrain(Dictionary<Vector2Int, Tile> cells, TerrainType terrain)
    {
        int found = 0;
        foreach (Tile tile in cells.Values)
        {
            if (tile.Terrain == terrain)
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>적 스폰으로 찍힌 칸 수.</summary>
    public static int CountSpawn(Dictionary<Vector2Int, Tile> cells)
    {
        int found = 0;
        foreach (Tile tile in cells.Values)
        {
            if (tile.IsEnemySpawn)
            {
                found++;
            }
        }

        return found;
    }

    // 이 통행 방식으로 지나야 하는 칸 수.
    public static int CountPass(Dictionary<Vector2Int, Tile> cells, PassType way)
    {
        int found = 0;
        foreach (Tile tile in cells.Values)
        {
            if (tile.State.Pass == way)
            {
                found++;
            }
        }

        return found;
    }

    // 이 기믹이 걸린 칸 수.
    public static int CountGimmick(Dictionary<Vector2Int, Tile> cells, GimmickType gimmick)
    {
        int found = 0;
        foreach (Tile tile in cells.Values)
        {
            if (tile.State.Gimmick == gimmick)
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>이 배치 허용이 켜져 있는 칸 수.</summary>
    public static int CountFlag(Dictionary<Vector2Int, Tile> cells, MapBrush brush)
    {
        int found = 0;
        foreach (Tile tile in cells.Values)
        {
            if (TileFlagQuery.IsOn(tile, brush))
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>
    /// 이 허용이 지금 규칙에서 효력을 갖는 칸 수 = 집계의 분모.
    /// 근접·생산은 지상 칸이, 원거리는 고지 칸이 모집단이다.
    /// </summary>
    public static int CountField(Dictionary<Vector2Int, Tile> cells, MapBrush brush)
    {
        int found = 0;
        foreach (Tile tile in cells.Values)
        {
            if (TileFlagQuery.TakesEffect(tile.Terrain, brush))
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>
    /// 켜져 있지만 지금 규칙에서는 아무 효과가 없는 칸 수(예: 지상 칸의 CanRanged).
    /// 규칙이 나중에 풀릴 것을 보고 남겨둔 값일 수 있어 문제로 잡지 않고 수만 알린다.
    /// </summary>
    public static int CountInert(Dictionary<Vector2Int, Tile> cells, MapBrush brush)
    {
        int found = 0;
        foreach (Tile tile in cells.Values)
        {
            if (TileFlagQuery.IsOn(tile, brush) && !TileFlagQuery.TakesEffect(tile.Terrain, brush))
            {
                found++;
            }
        }

        return found;
    }
}
