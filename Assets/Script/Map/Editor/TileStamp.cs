using UnityEditor;

/// <summary>
/// 타일 한 칸에 저작 값을 되돌리기 가능하게 기록하는 에디터 전용 도구.
///
/// Terrain·CanMelee 같은 값은 Tile.State에, 스폰은 Tile.isEnemySpawn에 있어 저장 위치가 다르지만
/// 둘 다 Tile 컴포넌트에 직렬화되므로 Undo.RecordObject(tile) 하나로 같이 덮인다.
/// 붓질 한 번(누르고 끌어 떼기)은 BeginStroke~EndStroke로 묶여 Ctrl+Z 한 번에 통째로 돌아간다.
/// </summary>
public static class TileStamp
{
    /// <summary>
    /// 붓질 시작. 반환한 그룹 번호를 EndStroke에 그대로 넘긴다.
    ///
    /// 먼저 묶음을 하나 넘긴다. 안 그러면 GetCurrentGroup이 직전 작업이 들어 있는 묶음을 가리키고,
    /// EndStroke의 CollapseUndoOperations가 그 작업까지 이 붓질에 접어 넣는다 —
    /// Ctrl+Z 한 번에 붓질과 무관한 편집까지 같이 사라진다.
    /// </summary>
    public static int BeginStroke()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Stamp Tiles");
        return Undo.GetCurrentGroup();
    }

    /// <summary>붓질 종료. 그 사이 찍은 칸 전부를 되돌리기 한 단계로 접는다.</summary>
    public static void EndStroke(int group)
    {
        Undo.CollapseUndoOperations(group);
    }

    /// <summary>
    /// 이 칸에 붓을 찍는다. on=false면 토글 붓을 끄는 쪽으로 찍는다(지형 붓은 on을 보지 않는다).
    /// 붓이 None이면 찍을 것이 없으므로 호출 자체가 잘못이다 — 조용히 넘기지 않고 바로 터뜨린다.
    /// </summary>
    public static void Stamp(Tile tile, MapBrush brush, bool on)
    {
        Undo.RecordObject(tile, "Stamp Tile");

        switch (brush)
        {
            case MapBrush.Ground:
                tile.State.Terrain = TerrainType.Ground;
                break;
            case MapBrush.High:
                tile.State.Terrain = TerrainType.High;
                break;
            case MapBrush.Core:
                tile.State.Terrain = TerrainType.Core;
                break;
            case MapBrush.Special:
                tile.State.Terrain = TerrainType.Special;
                break;
            case MapBrush.Empty:
                tile.State.Terrain = TerrainType.Empty;
                break;
            case MapBrush.Spawn:
                tile.isEnemySpawn = on;
                break;
            case MapBrush.Melee:
                tile.State.CanMelee = on;
                DropLegacyBit(tile, TileFlags.MeleePlaceable);
                break;
            case MapBrush.Ranged:
                tile.State.CanRanged = on;
                DropLegacyBit(tile, TileFlags.RangedPlaceable);
                break;
            case MapBrush.Build:
                tile.State.CanBuild = on;
                DropLegacyBit(tile, TileFlags.BuildingPlaceable);
                break;
            case MapBrush.Swim:
                // 끄면 기본인 걷기로 돌아온다 — 통행 방식은 칸마다 하나만 정해진다.
                tile.State.Pass = PassType.Walk;
                if (on)
                {
                    tile.State.Pass = PassType.Swim;
                }
                break;
            case MapBrush.Fire:
                StampGimmick(tile, GimmickType.Fire, on);
                break;
            case MapBrush.Campfire:
                StampGimmick(tile, GimmickType.Campfire, on);
                break;
            case MapBrush.Windwall:
                StampGimmick(tile, GimmickType.Windwall, on);
                break;
            default:
                throw new System.ArgumentOutOfRangeException(
                    nameof(brush), brush, "찍을 수 없는 붓입니다. 창이 None 상태로 Stamp를 부른 것입니다.");
        }

        EditorUtility.SetDirty(tile);
    }

    // 지정한 기믹을 켜거나 같은 기믹일 때만 끈다.
    private static void StampGimmick(Tile tile, GimmickType gimmick, bool on)
    {
        if (on)
        {
            tile.State.Gimmick = gimmick;
            return;
        }

        if (tile.State.Gimmick == gimmick)
        {
            tile.State.Gimmick = GimmickType.None;
        }
    }

    /// <summary>
    /// 이 허용을 담고 있던 옛 Flags 비트를 지운다.
    /// TileState.ImportFlags는 비트가 켜져 있으면 명시 필드를 true로 올리기만 하고 내리지는 않는다.
    /// 그래서 비트를 남겨두면 붓으로 끈 값이 런타임에 되살아난다 — 찍은 값이 곧 결과가 되도록 비운다.
    /// </summary>
    private static void DropLegacyBit(Tile tile, TileFlags bit)
    {
        int kept = (int)tile.State.Flags & ~(int)bit;
        tile.State.Flags = (TileFlags)kept;
    }
}

/// <summary>
/// 메이커 창의 붓 종류. None은 읽기 전용(기존 Path Preview와 같은 동작).
/// Decor는 지형이 아니라 타일 위에 얹는 겉모습이라 칠하기로는 찍히지 않는다(교체 도구로 얹는다).
/// 값이 테마 에셋에 직렬화되므로 새 붓은 끝에만 붙인다 — 중간에 끼우면 저장된 슬롯이 밀린다.
/// </summary>
public enum MapBrush
{
    None,
    Ground,
    High,
    Core,
    Special,
    Empty,
    Spawn,
    Melee,
    Ranged,
    Build,
    Decor,
    Swim,
    Fire,
    Campfire,
    Windwall
}
