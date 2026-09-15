using UnityEditor;

/// <summary>
/// 지형이 바뀐 타일에 그 지형의 기본 배치 허용을 새기는 에디터 전용 도구.
///
/// 교체할 때마다 배치 붓으로 되돌아가 근접·원거리를 다시 칠하는 일을 없애려고 둔다.
/// 지형만 바꾸고 허용을 그대로 두면 그 자리에서 못 쓰는 칸이 된다 —
/// 새로 얹은 고지 판은 원거리가 꺼져 있어 아무도 못 서고,
/// 고지를 걷어 지상으로 돌린 칸은 원거리만 켜져 있어 근접을 못 놓는다.
/// </summary>
public static class TileTerrainDefault
{
    /// <summary>
    /// 이 지형에서 기본으로 켜지는 배치 허용. 없으면 None.
    ///
    /// 생산은 기본에 넣지 않는다. 모듈 A~F 여섯 개가 모두 지상을 근접 80칸 대 생산 36칸으로
    /// 갈라 저작해 두었고(둘이 겹치는 칸은 없다), 생산은 지형이 아니라 구역이 정하는 값이다.
    /// 지상 전체에 켜면 그 구역 저작이 통째로 무의미해진다.
    ///
    /// 지형별로 "효력이 있는" 허용을 묻는 TileFlagQuery.TakesEffect와는 다른 질문이다 —
    /// 지상은 생산도 효력이 있지만 기본은 아니다.
    /// </summary>
    public static MapBrush Of(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Ground: return MapBrush.Melee;
            case TerrainType.High: return MapBrush.Ranged;
            default: return MapBrush.None;
        }
    }

    /// <summary>이 타일의 배치 허용을 지금 지형의 기본값으로 새긴다.</summary>
    public static void Apply(Tile tile)
    {
        Undo.RecordObject(tile, "Swap Tile");

        MapBrush want = Of(tile.State.Terrain);
        tile.State.CanMelee = want == MapBrush.Melee;
        tile.State.CanRanged = want == MapBrush.Ranged;
        tile.State.CanBuild = want == MapBrush.Build;

        DropLegacyBits(tile);
        EditorUtility.SetDirty(tile);
    }

    /// <summary>
    /// 배치 허용을 담고 있던 옛 Flags 비트를 지운다.
    /// TileState.ImportFlags는 비트가 켜져 있으면 명시 필드를 올리기만 하고 내리지는 않으므로,
    /// 남겨 두면 여기서 끈 허용이 런타임에 되살아난다.
    /// </summary>
    private static void DropLegacyBits(Tile tile)
    {
        const int places = (int)TileFlags.MeleePlaceable
            | (int)TileFlags.RangedPlaceable
            | (int)TileFlags.BuildingPlaceable;

        tile.State.Flags = (TileFlags)((int)tile.State.Flags & ~places);
    }
}
