/// <summary>
/// 타일에 배치 허용이 켜져 있는지 읽는다.
///
/// 명시 필드(CanMelee 등)만 보면 안 된다 — 옛 씬 데이터는 허용을 Flags 비트에 담고 있고,
/// 런타임에서는 MapBoard.Build가 ImportFlags로 그 비트를 명시 필드에 합친 뒤에야 판정이 돈다.
/// 창은 Build를 거치지 않으므로 둘을 직접 합쳐 봐야 런타임과 같은 그림이 나온다.
///
/// 여기는 "켜져 있나"만 답한다. "놓을 수 있나"는 지형까지 따지는 TilePlacementRule의 몫이다.
/// </summary>
public static class TileFlagQuery
{
    public static bool CanMelee(Tile tile)
    {
        TileState state = tile.State;
        return state.CanMelee || (state.Flags & TileFlags.MeleePlaceable) != 0;
    }

    public static bool CanRanged(Tile tile)
    {
        TileState state = tile.State;
        return state.CanRanged || (state.Flags & TileFlags.RangedPlaceable) != 0;
    }

    public static bool CanBuild(Tile tile)
    {
        TileState state = tile.State;
        return state.CanBuild || (state.Flags & TileFlags.BuildingPlaceable) != 0;
    }

    /// <summary>이 붓이 가리키는 허용이 켜져 있나. 배치 붓이 아니면 false.</summary>
    public static bool IsOn(Tile tile, MapBrush brush)
    {
        switch (brush)
        {
            case MapBrush.Melee: return CanMelee(tile);
            case MapBrush.Ranged: return CanRanged(tile);
            case MapBrush.Build: return CanBuild(tile);
            default: return false;
        }
    }

    /// <summary>
    /// 이 허용이 지금 규칙에서 실제로 효력을 갖는 지형인가.
    /// TilePlacementRule이 근접·건물에는 Ground를, 원거리에는 High를 요구한다.
    /// 규칙이 나중에 풀릴 수 있으므로 여기서 막지 않고 "효력 여부"만 알려준다.
    /// </summary>
    public static bool TakesEffect(TerrainType terrain, MapBrush brush)
    {
        switch (brush)
        {
            case MapBrush.Melee: return terrain == TerrainType.Ground;
            case MapBrush.Build: return terrain == TerrainType.Ground;
            case MapBrush.Ranged: return terrain == TerrainType.High;
            default: return false;
        }
    }

    /// <summary>
    /// 배치 허용이 하나라도 켜졌지만 지금 지형에선 아무 효과가 없는 칸인가(무효 조합).
    /// 예: 지상 칸의 CanRanged. 잘못이 아니라 나중을 위한 대비일 수 있어 막지 않고 표시만 한다.
    /// </summary>
    public static bool IsInert(Tile tile)
    {
        return Inert(tile, MapBrush.Melee) || Inert(tile, MapBrush.Ranged) || Inert(tile, MapBrush.Build);
    }

    private static bool Inert(Tile tile, MapBrush brush)
    {
        return IsOn(tile, brush) && !TakesEffect(tile.Terrain, brush);
    }
}
