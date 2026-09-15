using System.Collections.Generic;
using UnityEngine;

// 고정된 사막 맵을 읽어 모든 타일의 방향별 바람 면역 결과를 완성합니다.
public class WindShelterCalc
{
    private readonly List<Tile> highTiles = new();
    private readonly List<Tile> groundTiles = new();
    private readonly HashSet<Vector2Int> highCells = new();

    private int minCol = int.MaxValue;
    private int maxCol = int.MinValue;
    private int minRow = int.MaxValue;
    private int maxRow = int.MinValue;

    // 사막판 전체를 훑어서, 타일별로 바람이 막히는 방향 정보를 만들어 돌려준다.
    public WindShelterData BuildData(IReadOnlyDictionary<Vector2Int, Tile> cells)
    {
        WindShelterData data = new();
        CollectTiles(cells, data);
        CollectHighs();
        ResolveGrounds(data);
        return data;
    }

    // 모든 타일을 일단 "안 막힘"으로 채우고, 맵 크기와 타일 종류를 기록한다.
    private void CollectTiles(
        IReadOnlyDictionary<Vector2Int, Tile> cells,
        WindShelterData data)
    {
        foreach (Tile tile in cells.Values)
        {
            data.KeepShelter(tile, WindShelter.NoShelter);
            KeepBounds(tile.Coord);
            KeepTileType(tile);
        }
    }

    // 지금까지 본 타일 중 가장 바깥쪽 좌표(맵 경계)를 갱신한다.
    private void KeepBounds(Vector2Int cell)
    {
        minCol = Mathf.Min(minCol, cell.x);
        maxCol = Mathf.Max(maxCol, cell.x);
        minRow = Mathf.Min(minRow, cell.y);
        maxRow = Mathf.Max(maxRow, cell.y);
    }

    // 타일을 고지/평지 목록에 나눠 담는다.
    private void KeepTileType(Tile tile)
    {
        if (tile.Terrain == TerrainType.High)
        {
            highTiles.Add(tile);
        }

        if (tile.Terrain == TerrainType.Ground)
        {
            groundTiles.Add(tile);
        }
    }

    // 맵 안쪽에 있는 고지 타일만 골라서 위치를 기록한다.
    private void CollectHighs()
    {
        for (int highIndex = 0; highIndex < highTiles.Count; highIndex++)
        {
            Tile highTile = highTiles[highIndex];
            if (IsInnerCell(highTile.Coord))
            {
                KeepHigh(highTile.Coord);
            }
        }
    }

    // 이 좌표가 맵 가장자리가 아니라 안쪽인지 확인한다.
    private bool IsInnerCell(Vector2Int cell)
    {
        return cell.x > minCol
            && cell.x < maxCol
            && cell.y > minRow
            && cell.y < maxRow;
    }

    // 이 고지 타일의 좌표를 기록해둔다.
    private void KeepHigh(Vector2Int cell)
    {
        highCells.Add(cell);
    }

    // 모든 평지 타일에 대해 바람 막힘 여부를 계산해서 채운다.
    private void ResolveGrounds(WindShelterData data)
    {
        for (int groundIndex = 0; groundIndex < groundTiles.Count; groundIndex++)
        {
            Tile groundTile = groundTiles[groundIndex];
            WindShelter shelter = ResolveShelter(groundTile.Coord);
            data.KeepShelter(groundTile, shelter);
        }
    }

    // 이 타일 바로 옆 한 칸에 고지가 있어 어느 방향 바람을 막는지 계산한다.
    private WindShelter ResolveShelter(Vector2Int cell)
    {
        WindShelter shelter = WindShelter.NoShelter;

        if (HasHigh(cell + GridCalculator.Left))
        {
            shelter |= WindShelter.FromWest;
        }

        if (HasHigh(cell + GridCalculator.Right))
        {
            shelter |= WindShelter.FromEast;
        }

        if (HasHigh(cell + GridCalculator.Down))
        {
            shelter |= WindShelter.FromSouth;
        }

        if (HasHigh(cell + GridCalculator.Up))
        {
            shelter |= WindShelter.FromNorth;
        }

        return shelter;
    }

    // 이 좌표에 고지가 있는지 확인한다.
    private bool HasHigh(Vector2Int cell)
    {
        return highCells.Contains(cell);
    }
}
