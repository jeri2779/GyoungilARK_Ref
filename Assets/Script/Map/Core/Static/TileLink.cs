using System.Collections.Generic;
using UnityEngine;

// 격자에 놓인 타일들끼리 상하좌우 이웃을 미리 이어 두는 정적 클래스.
public static class TileLink
{
    // 같은 좌표 공간의 타일 전부에 이웃을 새긴다. 타일이 생기거나 사라졌을 때만 부르면 된다.
    public static void LinkNeighbors(IReadOnlyDictionary<Vector2Int, Tile> cells)
    {
        var neighbors = new List<Tile>(GridCalculator.Directions.Length);

        foreach (Tile tile in cells.Values)
        {
            FillNeighbors(cells, tile, neighbors);
            tile.SetNeighbors(neighbors.ToArray());
        }
    }

    // 한 타일의 상하좌우를 찾아 담는다. 방향이 넷으로 고정이라 돌지 않고 직접 집는다.
    private static void FillNeighbors(
        IReadOnlyDictionary<Vector2Int, Tile> cells,
        Tile tile,
        List<Tile> neighbors)
    {
        neighbors.Clear();
        for(int i = 0; i < GridCalculator.Directions.Length; i++)
        {
            Vector2Int direction = GridCalculator.Directions[i];
            AddNeighbor(cells, neighbors, tile.Coord + direction);
        }
        
    }

    // 그 좌표에 타일이 있으면 담는다. 격자 가장자리는 없는 방향이 생긴다.
    private static void AddNeighbor(
        IReadOnlyDictionary<Vector2Int, Tile> cells,
        List<Tile> neighbors,
        Vector2Int coord)
    {
        if (!cells.TryGetValue(coord, out Tile neighbor))
        {
            return;
        }

        neighbors.Add(neighbor);
    }
}
