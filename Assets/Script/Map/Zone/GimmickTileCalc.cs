using System.Collections.Generic;

// 지역 보드를 한 번 훑어 기믹이 걸린 칸만 골라낸다.
public static class GimmickTileCalc
{
    // 그 보드 안에서 기믹 칸만 모아 새 목록으로 돌려준다.
    public static List<Tile> CollectTiles(MapBoard board)
    {
        List<Tile> found = new();
        IReadOnlyList<Tile> cells = board.CellList;

        for (int index = 0; index < cells.Count; index++)
        {
            AddWhenGimmick(found, cells[index]);
        }

        return found;
    }

    // 기믹이 걸렸거나 헤엄 칸일 때만 목록에 담는다.
    private static void AddWhenGimmick(List<Tile> found, Tile tile)
    {
        if (!IsGimmickTile(tile))
        {
            return;
        }
        found.Add(tile);
    }

    // 이 칸이 안내 대상 기믹 칸인지 알려준다.
    public static bool IsGimmickTile(Tile tile)
    {
        return tile.Gimmick != GimmickType.None || tile.State.Pass == PassType.Swim;
    }
}
