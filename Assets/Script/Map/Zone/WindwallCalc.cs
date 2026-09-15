using System.Collections.Generic;
using UnityEngine;

// 고정된 언덕 가림막을 읽어 바람 방향별 보호 칸을 맵 로드 때 한 번 계산합니다.
// 칸 딕셔너리만 있으면 계산 가능하다 — 런타임(MapBoard.Cells)과 저작 창(에디터 스캔) 둘 다 같은 함수를 쓴다.
public class WindwallCalc
{
    // 안쪽 칸을 한 번 훑어 가림막마다 네 방향 팔을 채운 결과를 돌려줍니다.
    public WindwallData BuildData(IReadOnlyDictionary<Vector2Int, Tile> cells, int reach)
    {
        WindwallData data = new();

        foreach (Tile tile in cells.Values)
        {
            if (IsHighWindwall(tile))
            {
                KeepArms(cells, data, tile.Coord, reach);
            }
        }

        return data;
    }

    // 이 타일이 언덕 위 가림막인지 확인한다. 지상은 고지가 대신 막아주므로 가림막으로 인정하지 않는다.
    private static bool IsHighWindwall(Tile tile)
    {
        if (!tile.IsHigh)
        {
            return false;
        }

        return tile.IsWindwall;
    }

    // 가림막 한 칸에서 네 방향 팔을 방향마다 따로 보관한다.
    private static void KeepArms(IReadOnlyDictionary<Vector2Int, Tile> cells, WindwallData data, Vector2Int origin, int reach)
    {
        for (int index = 0; index < GridCalculator.Directions.Length; index++)
        {
            Vector2Int wind = GridCalculator.Directions[index];
            KeepOneWind(cells, data, origin, wind, reach);
        }
    }

    // 한 바람 방향의 팔만 담은 범위를 만들어 그 방향 칸에 보관한다.
    private static void KeepOneWind(
        IReadOnlyDictionary<Vector2Int, Tile> cells,
        WindwallData data,
        Vector2Int origin,
        Vector2Int wind,
        int reach)
    {
        List<Tile> range = new();
        KeepOrigin(cells, origin, range);
        KeepArm(cells, data, origin, wind, reach, range);
        data.KeepRange(wind, origin, range);
    }

    // 표시용 범위 목록에만 원점(가림막 자신)을 담는다.
    // 판정용 팔(KeepArm)은 원점을 비보호로 그대로 두고, 윤곽선만 원점까지 이어진 한 덩어리로 그려지게 한다.
    private static void KeepOrigin(IReadOnlyDictionary<Vector2Int, Tile> cells, Vector2Int origin, List<Tile> range)
    {
        if (cells.TryGetValue(origin, out Tile tile))
        {
            range.Add(tile);
        }
    }

    // 한 방향으로 reach 칸까지 보호 칸을 보관하고, 실제 타일이 있는 칸은 범위 목록에도 담는다.
    private static void KeepArm(
        IReadOnlyDictionary<Vector2Int, Tile> cells,
        WindwallData data,
        Vector2Int origin,
        Vector2Int wind,
        int reach,
        List<Tile> range)
    {
        for (int step = 1; step <= reach; step++)
        {
            Vector2Int cell = origin + wind * step;
            data.KeepArm(wind, cell);
            if (cells.TryGetValue(cell, out Tile tile))
            {
                range.Add(tile);
            }
        }
    }
}
