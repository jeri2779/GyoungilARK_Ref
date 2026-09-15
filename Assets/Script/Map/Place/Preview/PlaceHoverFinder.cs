using UnityEngine;

// 이번 프레임에 배치/재배치 호버가 가리키는 자리를 찾는다. 칠하지 않는다.
public class PlaceHoverFinder
{
    private readonly MapView game;
    private readonly PlaceFinder finder;
    private readonly HoverPlaceData hoverPlace;

    public PlaceHoverFinder(MapView game, PlaceFinder finder, HoverPlaceData hoverPlace)
    {
        this.game = game;
        this.finder = finder;
        this.hoverPlace = hoverPlace;
    }

    public HoverMode FindHover(out PlaceData placeData, out GameObject unit, out OccupantKind kind, out bool canPlaceOrSwap)
    {
        HoverMode mode = ResolveHoverMode();
        ResolveHover(mode, out placeData, out unit, out kind);
        UpdateHoverPlace(mode, placeData);
        canPlaceOrSwap = ResolveCanPlaceOrSwap(mode, placeData);
        return mode;
    }

    // 재배치 중 목표 칸이 점유돼 있어도 자리 맞바꾸기가 가능하면 미리보기를 "배치 가능"으로 본다.
    // 배치 모드(로스터에서 새로 놓기)는 스왑 대상이 아니므로 원래 판정을 그대로 쓴다.
    private bool ResolveCanPlaceOrSwap(HoverMode mode, PlaceData placeData)
    {
        if (placeData.CanPlace)
        {
            return true;
        }

        if (mode != HoverMode.Held)
        {
            return false;
        }

        return game.CanPlaceOrSwap(placeData);
    }

    private HoverMode ResolveHoverMode()
    {
        if (IsInputBlocked())
        {
            return HoverMode.Blocked;
        }

        if (IsPlacingMode())
        {
            return HoverMode.Placing;
        }

        if (IsHeldReplaceMode())
        {
            return HoverMode.Held;
        }

        return HoverMode.Range;
    }

    private bool IsInputBlocked()
    {
        return game.InputBlocked;
    }

    private bool IsPlacingMode()
    {
        return game.IsPlacing;
    }

    private bool IsHeldReplaceMode()
    {
        if (IsReplacingMode())
        {
            return IsHoldingUnit();
        }

        return false;
    }

    private bool IsReplacingMode()
    {
        return game.IsReplacing;
    }

    private bool IsHoldingUnit()
    {
        return game.IsHolding;
    }

    private bool IsPlacingHoverMode(HoverMode mode)
    {
        return mode == HoverMode.Placing;
    }

    private bool IsHeldHoverMode(HoverMode mode)
    {
        return mode == HoverMode.Held;
    }

    private void ResolveHover(HoverMode mode, out PlaceData placeData, out GameObject unit, out OccupantKind kind)
    {
        placeData = default;
        unit = null;
        kind = OccupantKind.None;

        if (IsPlacingHoverMode(mode))
        {
            ResolvePlacingHover(out placeData, out unit, out kind);
            return;
        }

        if (IsHeldHoverMode(mode))
        {
            ResolveHeldHover(out placeData, out unit);
            kind = game.HeldKind;
        }
    }

    private void ResolvePlacingHover(out PlaceData placeData, out GameObject unit, out OccupantKind kind)
    {
        placeData = default;
        unit = null;
        kind = OccupantKind.None;

        bool resolved = finder.TryResolveSlot(out Placeable slot, out placeData);
        if (slot != null)
        {
            unit = slot.prefab;
            kind = slot.kind;
        }

        if (!resolved)
        {
            placeData = default;
        }
    }

    private void ResolveHeldHover(out PlaceData placeData, out GameObject unit)
    {
        unit = game.HeldUnit;
        finder.TryResolve(game.HeldKind, game.HeldSize, out placeData);
    }

    private void UpdateHoverPlace(HoverMode mode, PlaceData placeData)
    {
        if (IsPlacingHoverMode(mode))
        {
            KeepOrClearHover(placeData);
            return;
        }

        if (IsHeldHoverMode(mode))
        {
            KeepOrClearHover(placeData);
            return;
        }

        hoverPlace.Clear();
    }

    private void KeepOrClearHover(PlaceData placeData)
    {
        if (HasArea(placeData.Area))
        {
            hoverPlace.Keep(placeData);
            return;
        }

        hoverPlace.Clear();
    }

    private static bool HasArea(PlacementArea area)
    {
        return area != null;
    }
}
