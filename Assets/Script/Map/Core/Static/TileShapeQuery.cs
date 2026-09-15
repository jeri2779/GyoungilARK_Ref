using System.Collections.Generic;
using UnityEngine;

// MapBoard.GetTiles(origin, range, bool square)만 사용해 Cross/Line 조회를 조합하는 헬퍼.
// MapBoard 자체에는 새 필드/메서드를 추가하지 않는다.
public static class TileShapeQuery
{
    public static List<Tile> GetTiles(MapBoard board, Vector2Int origin, int range, RangeShape shape)
    {
        if (shape == RangeShape.Diamond) return board.GetTiles(origin, range, false);
        if (shape == RangeShape.Square) return board.GetTiles(origin, range, true);

        // Cross: 원점을 지나는 가로·세로 축 위의 칸만 직접 훑는다(정사각 전체를 만들고 버리지 않는다).
        var result = new List<Tile>();
        for (int dx = -range; dx <= range; dx++)
        {
            if (board.TryGetCell(new Vector2Int(origin.x + dx, origin.y), out Tile h)) result.Add(h);
        }
        for (int dy = -range; dy < 0; dy++)
        {
            if (board.TryGetCell(new Vector2Int(origin.x, origin.y + dy), out Tile v)) result.Add(v);
        }
        for (int dy = 1; dy <= range; dy++)
        {
            if (board.TryGetCell(new Vector2Int(origin.x, origin.y + dy), out Tile v)) result.Add(v);
        }
        return result;
    }

    // origin 다음 칸부터 direction(단위 벡터, 4방향)으로 length칸 조회. origin 자신은 포함하지 않는다.
    // width > 0이면 진행 방향의 수직 방향으로 좌우 width칸씩 넓혀(폭 2*width+1칸) 조회한다.
    public static List<Tile> GetLineTiles(MapBoard board, Vector2Int origin, Vector2Int direction, int length, int width = 0)
    {
        Vector2Int perp = new Vector2Int(-direction.y, direction.x);
        var result = new List<Tile>(length * (2 * width + 1));
        for (int step = 1; step <= length; step++)
        {
            Vector2Int center = origin + direction * step;
            for (int offset = -width; offset <= width; offset++)
            {
                Vector2Int target = center + perp * offset;
                if (board.TryGetCell(target, out Tile tile))
                {
                    result.Add(tile);
                }
            }
        }
        return result;
    }
}
public enum RangeShape { Diamond, Square, Cross }