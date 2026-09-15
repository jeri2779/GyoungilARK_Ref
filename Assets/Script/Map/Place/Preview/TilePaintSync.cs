using System.Collections.Generic;
using UnityEngine;

// 이번 프레임에 칠할 목록을 정한다. 지난 프레임과 같으면 아무것도 내주지 않는다.
public class TilePaintSync
{
    private readonly PlaceHoverFinder hoverFinder;
    private readonly SkillTargetFinder skillFinder;
    private readonly PlayerSkillTargetFinder playerSkillFinder;
    private readonly RangeCalc rangeCalc;
    private readonly RangeTileData rangeStore;
    private readonly TilePainter painter;
    private readonly PointerPick pointerPick;

    private static readonly List<Tile> NoEdgeTiles = new();

    private readonly List<PaintEntry> plan = new();
    private TileDisplayData lastDisplay;
    private bool hasDisplay;

    public PlaceEdgeData EdgeData { get; private set; }

    // 아무 동작도 없을 때 커서가 가리키는 타일. 배치·재배치·사거리 표시 중이면 비어있다.
    public Tile HoverTile { get; private set; }

    // 지금 눌러서 켜진 모닥불의 범위. 모닥불이 아니면 빈 목록.
    public IReadOnlyList<Tile> CampfireEdgeTiles { get; private set; } = NoEdgeTiles;

    // CampfireEdgeTiles가 바뀔 때마다 올라간다 — CampfireEdgeView가 다시 그릴지 판단하는 값.
    public int CampfireEdgeVersion { get; private set; }

    // 지금 눌러서 켜진 가림막의 범위. 가림막이 아니면 빈 목록.
    public IReadOnlyList<Tile> WindwallEdgeTiles { get; private set; } = NoEdgeTiles;

    // WindwallEdgeTiles가 바뀔 때마다 올라간다 — 가림막 외곽선 출력기가 다시 그릴지 판단하는 값.
    public int WindwallEdgeVersion { get; private set; }

    public TilePaintSync(
        PlaceHoverFinder hoverFinder,
        SkillTargetFinder skillFinder,
        PlayerSkillTargetFinder playerSkillFinder,
        RangeCalc rangeCalc,
        RangeTileData rangeStore,
        TilePainter painter,
        PointerPick pointerPick)
    {
        this.hoverFinder = hoverFinder;
        this.skillFinder = skillFinder;
        this.playerSkillFinder = playerSkillFinder;
        this.rangeCalc = rangeCalc;
        this.rangeStore = rangeStore;
        this.painter = painter;
        this.pointerPick = pointerPick;
    }

    public bool TryBuildPlan(out List<PaintEntry> result)
    {
        result = plan;

        HoverMode mode = hoverFinder.FindHover(out PlaceData placeData, out GameObject unit, out OccupantKind kind, out bool canPlaceOrSwap);
        EdgeData = new PlaceEdgeData(mode, kind);
        HoverTile = ResolveHoverTile(mode);
        skillFinder.TryFindTarget(out Hero caster, out HeroActiveSkill skill, out Tile skillOrigin);
        playerSkillFinder.TryFindTarget(out PlayerSkillSlot armedSkill, out Tile playerSkillOrigin);
        int rangeVersion = ResolveRangeVersion(mode);

        TileDisplayData display = BuildDisplayKey(
            mode, placeData.Area, unit, kind, canPlaceOrSwap, rangeVersion,
            caster, skill, skillOrigin, armedSkill, playerSkillOrigin);

        if (IsSameDisplay(display))
        {
            return false;
        }

        RebuildPlan(mode, placeData, unit, canPlaceOrSwap, skill, skillOrigin, armedSkill, playerSkillOrigin);

        lastDisplay = display;
        hasDisplay = true;
        return true;
    }

    // ---- 이번 프레임 표시 값 조립·비교 ----

    private int ResolveRangeVersion(HoverMode mode)
    {
        if (IsRangeHoverMode(mode))
        {
            return rangeStore.Version;
        }

        return 0;
    }

    private static TileDisplayData BuildDisplayKey(
        HoverMode mode,
        PlacementArea area,
        GameObject unit,
        OccupantKind kind,
        bool canPlace,
        int rangeVersion,
        Hero skillCaster,
        HeroActiveSkill skill,
        Tile skillOrigin,
        PlayerSkillSlot armedSkill,
        Tile playerSkillOrigin)
    {
        ResolveAreaOrigin(area, out MapBoard board, out Vector2Int origin);

        return new TileDisplayData(
            mode, board, origin, unit, kind, canPlace, rangeVersion,
            skillCaster, skill, skillOrigin, armedSkill, playerSkillOrigin);
    }

    private static void ResolveAreaOrigin(PlacementArea area, out MapBoard board, out Vector2Int origin)
    {
        board = null;
        origin = default;

        if (HasArea(area))
        {
            board = area.Board;
            origin = area.Origin;
        }
    }

    private bool IsSameDisplay(TileDisplayData display)
    {
        if (HasDisplay())
        {
            return display.SameAs(lastDisplay);
        }

        return false;
    }

    private bool HasDisplay()
    {
        return hasDisplay;
    }

    // ---- 칠할 목록 조립 ----

    private void RebuildPlan(HoverMode mode, PlaceData data, GameObject unit, bool canPlaceOrSwap, HeroActiveSkill skill, Tile skillOrigin, PlayerSkillSlot armedSkill, Tile playerSkillOrigin)
    {
        plan.Clear();
        CampfireEdgeTiles = NoEdgeTiles;
        CampfireEdgeVersion = 0;
        WindwallEdgeTiles = NoEdgeTiles;
        WindwallEdgeVersion = 0;
        AddHoverEntries(mode, data, unit, canPlaceOrSwap);
        AddSkillEntries(skill, skillOrigin);
        AddPlayerSkillEntries(armedSkill, playerSkillOrigin);
    }

    private void AddHoverEntries(HoverMode mode, PlaceData data, GameObject unit, bool canPlaceOrSwap)
    {
        if (IsPlacingHoverMode(mode))
        {
            AddAreaPreviewEntries(data, unit, canPlaceOrSwap);
            return;
        }

        if (IsHeldHoverMode(mode))
        {
            AddAreaPreviewEntries(data, unit, canPlaceOrSwap);
            return;
        }

        if (IsRangeHoverMode(mode))
        {
            AddUnitRangeEntries();
        }
    }

    private bool IsPlacingHoverMode(HoverMode mode)
    {
        return mode == HoverMode.Placing;
    }

    private bool IsHeldHoverMode(HoverMode mode)
    {
        return mode == HoverMode.Held;
    }

    private static bool IsRangeHoverMode(HoverMode mode)
    {
        return mode == HoverMode.Range;
    }

    // 배치·재배치 중이 아니고 사거리도 안 뜬 평소 상태에서만 커서 아래 타일을 내어줍니다.
    private Tile ResolveHoverTile(HoverMode mode)
    {
        if (!IsRangeHoverMode(mode) || rangeStore.HasRange)
        {
            return null;
        }

        Tile tile = pointerPick.UnderPointer();
        if (tile == null || tile.IsSpecial)
        {
            return null;
        }

        return tile;
    }

    private void AddAreaPreviewEntries(PlaceData data, GameObject unit, bool canPlaceOrSwap)
    {
        if (HasArea(data.Area))
        {
            AddPreviewEntries(data, unit, canPlaceOrSwap);
        }
    }

    private static bool HasArea(PlacementArea area)
    {
        return area != null;
    }

    private void AddPreviewEntries(PlaceData data, GameObject unit, bool canPlaceOrSwap)
    {
        Color placeColor = ResolvePlaceColor(canPlaceOrSwap);
        Color rangeColor = ResolveRangeColor(canPlaceOrSwap);

        if (data.Area.Board.TryGetCell(data.Area.Origin, out Tile center))
        {
            if (rangeCalc.TryGetRangeAtCenter(unit, center, out List<Tile> range))
            {
                AddTileEntries(range, rangeColor);
            }
        }

        AddAreaCellEntries(data.Area, placeColor);
    }

    private Color ResolvePlaceColor(bool canPlaceOrSwap)
    {
        if (canPlaceOrSwap)
        {
            return painter.okColor;
        }

        return painter.denyColor;
    }

    private Color ResolveRangeColor(bool canPlaceOrSwap)
    {
        if (canPlaceOrSwap)
        {
            return painter.okColor;
        }

        return painter.denyColor;
    }

    // 배치 영역 타일을 상태 색상으로 추가합니다.
    private void AddAreaCellEntries(PlacementArea area, Color color)
    {
        if (area.Board.TryGetCell(area.Origin, out Tile tile))
        {
            plan.Add(new PaintEntry(tile, color));
        }
    }

    private void AddUnitRangeEntries()
    {
        if (rangeStore.EdgeKind == RangeEdgeKind.Campfire)
        {
            KeepCampfireEdge();
            return;
        }

        if (rangeStore.EdgeKind == RangeEdgeKind.Windwall)
        {
            KeepWindwallEdge();
            return;
        }

        AddRangeFillEntries();
    }

    // 클릭한 모닥불 범위를 모닥불 외곽선 출력기가 읽을 값으로 넘긴다.
    private void KeepCampfireEdge()
    {
        CampfireEdgeTiles = rangeStore.Tiles;
        CampfireEdgeVersion = rangeStore.Version;
    }

    // 클릭한 가림막 범위를 가림막 외곽선 출력기가 읽을 값으로 넘긴다.
    private void KeepWindwallEdge()
    {
        WindwallEdgeTiles = rangeStore.Tiles;
        WindwallEdgeVersion = rangeStore.Version;
    }

    private void AddRangeFillEntries()
    {
        for (int i = 0; i < rangeStore.Tiles.Count; i++)
        {
            plan.Add(new PaintEntry(rangeStore.Tiles[i], painter.rangeColor));
        }
    }

    private void AddSkillEntries(HeroActiveSkill skill, Tile origin)
    {
        if (HasTile(origin))
        {
            List<Tile> hitRange = SkillRangeCalc.BuildHitRange(skill, origin);
            AddTileEntries(hitRange, painter.skillColor);
        }
    }

    private void AddPlayerSkillEntries(PlayerSkillSlot skill, Tile origin)
    {
        if (skill != null && HasTile(origin))
        {
            List<Tile> hitRange = SkillRangeCalc.BuildHitRange(skill, origin);
            AddTileEntries(hitRange, painter.skillColor);
        }
    }

    private static bool HasTile(Tile tile)
    {
        return tile != null;
    }

    private void AddTileEntries(List<Tile> tiles, Color color)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            plan.Add(new PaintEntry(tiles[i], color));
        }
    }
}
