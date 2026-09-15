using System.Collections.Generic;
using UnityEngine;

// 고정 모닥불 타일을 읽어 마름모 보호 영역을 한 번 계산합니다.
public class CampfireCalc
{
    // 실제 보드에 존재하는 모닥불 보호 좌표만 만들어 반환합니다.
    public CampfireData BuildData(IReadOnlyDictionary<Vector2Int, Tile> cells, int range)
    {
        CampfireData data = new();
        int safeRange = Mathf.Max(0, range);

        foreach (Tile tile in cells.Values)
        {
            if (tile.IsCampfire)
            {
                KeepRange(cells, data, tile, safeRange);
            }
        }

        return data;
    }

    // 이 모닥불 원점에서 맨해튼 거리 안에 있는 실제 타일만 보관합니다. 통합 좌표와 원점 전용 범위를 함께 채웁니다.
    private static void KeepRange(
        IReadOnlyDictionary<Vector2Int, Tile> cells,
        CampfireData data,
        Tile origin,
        int range)
    {
        var protectedTiles = new List<Tile>();

        for (int x = -range; x <= range; x++)
        {
            int height = range - Mathf.Abs(x);
            for (int y = -height; y <= height; y++)
            {
                Vector2Int cell = origin.Coord + new Vector2Int(x, y);
                if (cells.TryGetValue(cell, out Tile tile))
                {
                    data.Keep(cell);
                    protectedTiles.Add(tile);
                }
            }
        }

        data.KeepRange(origin, protectedTiles);
    }
}
