
public static class TilePlacementRule
{
    /// <summary>placing 종류를 tile에 놓을 수 있는지(설계 §4).</summary>
    public static bool CanPlace(TileState tile, OccupantKind placing)
    {
        if (tile == null)
        {
            return false;
        }

        // 한 타일에는 하나의 주요 점유만(설계 §4). 적 경로(EnemyLane)는 점유가 아니라 통과 허용.
        if (!tile.IsEmpty)
        {
            return false;
        }

        return CanPlaceIgnoringOccupant(tile, placing);
    }

    /// <summary>점유 여부만 빼고 나머지 조건(지형·기믹·통행)을 검사한다.
    /// 재배치 스왑처럼 "지금 다른 유닛이 있어도 그 유닛을 치우면 놓을 수 있는가"를 물을 때 쓴다.</summary>
    public static bool CanPlaceIgnoringOccupant(TileState tile, OccupantKind placing)
    {
        if (tile == null)
        {
            return false;
        }

        if (tile.Terrain == TerrainType.Core)
        {
            return false;   // 본진 영역
        }

        if (tile.Terrain == TerrainType.Special)
        {
            return false;
        }

        // 모닥불 칸은 고정 장식이라 어떤 점유(영웅·건물·자원)도 놓을 수 없다.
        if (tile.Gimmick == GimmickType.Campfire)
        {
            return false;
        }

        // 걷기가 아닌 통행 칸(헤엄 등)은 아군이 설 자리가 아니다 — 지형이 지상이어도 놓지 못한다.
        if (tile.Pass != PassType.Walk)
        {
            return false;
        }

        return placing switch
        {
            OccupantKind.MeleeHero  => CheckMelee(tile),
            OccupantKind.RangedHero => CheckRanged(tile),
            OccupantKind.Building   => CheckBuild(tile),
            OccupantKind.Resource   => CheckBuild(tile),
            _ => false
        };
    }

    // 근접 영웅은 지상 타일에만.
    private static bool CheckMelee(TileState tile)
        => tile.Terrain == TerrainType.Ground && tile.CanMelee;

    // 원거리 영웅은 고지 타일에만.
    private static bool CheckRanged(TileState tile)
        => tile.Terrain == TerrainType.High && tile.CanRanged;

    // 생산 건물은 지상 타일에만.
    private static bool CheckBuild(TileState tile)
        => tile.Terrain == TerrainType.Ground && tile.CanBuild;
}
