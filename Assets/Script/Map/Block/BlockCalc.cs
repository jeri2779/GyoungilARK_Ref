using UnityEngine;

// 적이 저지당하는지 계산한다.
public static class BlockCalc
{
    // 적이 그 칸에서 저지당하는가.
    public static bool IsBlocked(Tile tile, GameObject enemy)
    {
        int enterIndex = tile.EnemyEnterIndex(enemy);
        return enterIndex < BlockLimit(tile);
    }

    // 그 칸에 선 근접 영웅이 막는 적 수.
    private static int BlockLimit(Tile tile)
    {
        if (tile.State.Occupant != OccupantKind.MeleeHero)
        {
            return 0;
        }

        return tile.OccupantHero.BlockCount;
    }
}
