using UnityEngine;

// 사막 지대 전용 — 밤마다 보드를 훑어 노출 칸을 강한/약한 이펙트로 분류합니다.
public class DesertEffectCalc
{
    private MapBoard board;
    private WindShelterData terrain;
    private UnitShelter units;
    private WindwallData walls;
    private Vector2Int wind;

    // 이 밤의 바람과 배치 기준으로 노출 칸마다 강한/약한 결과를 채운 데이터를 돌려준다.
    public DesertEffectData BuildData(MapBoard board, WindShelterData terrain, UnitShelter units, WindwallData walls, Vector2Int wind)
    {
        this.board = board;
        this.terrain = terrain;
        this.units = units;
        this.walls = walls;
        this.wind = wind;

        DesertEffectData data = new();
        RectInt play = board.PlayRect;
        for (int row = play.yMin; row < play.yMax; row++)
        {
            ResolveRow(row, play, data);
        }

        return data;
    }

    // 한 행의 칸마다 노출 여부를 확인해 데이터에 분류해 담는다.
    private void ResolveRow(int row, RectInt play, DesertEffectData data)
    {
        for (int col = play.xMin; col < play.xMax; col++)
        {
            ResolveCell(new Vector2Int(col, row), data);
        }
    }

    // 유닛이 선 칸은 건너뛰고, 노출 칸만 강한/약한 중 하나로 데이터에 담는다.
    private void ResolveCell(Vector2Int cell, DesertEffectData data)
    {
        if (!board.TryGetCell(cell, out Tile tile))
        {
            return;
        }

        if (units.HasUnit(tile.Coord))
        {
            return;
        }

        if (!DesertShelterQuery.IsUnsheltered(terrain, units, walls, tile, wind))
        {
            return;
        }

        KeepResolved(cell, tile, data);
    }

    // 이 타일이 뒤 칸의 바람을 막아 세우는 블로커(고지 또는 유닛)인지 확인한다. 강한/약한 분류에만 쓴다.
    private bool IsBlockerTile(Tile tile)
    {
        if (tile.IsHigh)
        {
            return true;
        }

        return units.HasUnit(tile.Coord);
    }

    // 다음 칸이 블로커인지에 따라 강한/약한 중 하나로 담는다.
    private void KeepResolved(Vector2Int cell, Tile tile, DesertEffectData data)
    {
        if (IsNextBlocker(cell))
        {
            data.KeepStrong(tile);
            return;
        }

        data.KeepWeak(tile);
    }

    // 바람이 다음으로 향할 칸이 블로커(안쪽 맵 밖 포함)인지 확인한다.
    private bool IsNextBlocker(Vector2Int cell)
    {
        Vector2Int nextCell = cell + wind;
        if (!board.PlayRect.Contains(nextCell))
        {
            return true;
        }

        if (!board.TryGetCell(nextCell, out Tile nextTile))
        {
            return true;
        }

        return IsBlockerTile(nextTile);
    }
}
