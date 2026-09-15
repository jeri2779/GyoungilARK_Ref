using System;
using UnityEngine;

// 배치된 유닛을 집어 다른 자리에 다시 놓는 담당.
public class UnitReplace
{
    private readonly PlacedUnitData _unitList;

    // 지금 집어 든 것 하나.
    private HeldData held;

    public UnitReplace(PlacedUnitData unitList)
    {
        _unitList = unitList;
    }

    // 집었다/내려놨다가 바뀔 때마다 알린다(UI가 매 프레임 폴링하지 않게).
    public event Action OnHoldChanged;

    // 재배치가 "성공적으로" 끝났을 때만 알린다 - ReturnHeld(취소)/CancelHeldAndDestroy에서는 안 울린다.
    // OnHoldChanged는 취소든 성공이든 똑같이 울려서 구분이 안 되므로, "재배치를 한 번 완료했는지"만
    // 필요한 구독자(튜토리얼 등)를 위해 따로 둔다.
    public event Action<GameObject, OccupantKind> Replaced;

    public bool IsHolding => held.Unit != null;
    public Tile HeldFromTile => held.FromTile;
    public GameObject HeldUnit => held.Unit;
    public OccupantKind HeldKind => held.Kind;
    public Vector2Int HeldSize => held.Size;

    // 칸의 유닛을 집어 든다. 빈 칸이면 false.
    public bool PickUp(Tile tile)
    {
        GameObject unit = tile.OccupantObject;
        if (unit == null)
        {
            return false;
        }

        if (!_unitList.TryGetArea(unit, out PlacementArea area))
        {
            return false;
        }

        OccupantKind kind = tile.State.Occupant;   // 칸을 비우면 None이 되므로 먼저 읽는다.
        Vector3 fromPosition = unit.transform.position;   // 비우기 전에 미리 읽는다(취소 시 되돌릴 자리).

        AreaPlace.Remove(area);
        _unitList.Remove(unit);

        held = new HeldData(unit, tile, area, fromPosition, kind, area.Size);
        OnHoldChanged?.Invoke();
        return true;
    }

    // 재배치를 취소하고 집었던 자리에 그대로 되돌린다.
    public void ReturnHeld()
    {
        PlaceData data = new PlaceData(held.FromArea, held.FromPosition, true);
        AreaPlace.Place(data, held.Unit, held.Kind);
        _unitList.Add(held.Unit, held.FromArea);

        ClearHeld();
    }

    // 집은 유닛을 목표 자리 한가운데로 옮긴다.
    public void MoveHeldTo(PlaceData data)
    {
        held.Unit.transform.position = data.Position;
    }

    // 집은 유닛을 자리에 내려놓는다. 못 놓으면 false.
    public bool TryDrop(PlaceData data)
    {
        if (!data.CanPlace && !TrySwap(ref data))
        {
            return false;
        }

        AreaPlace.Place(data, held.Unit, held.Kind);
        _unitList.Add(held.Unit, data.Area);
        //
        Hero hero = held.Unit.GetComponent<Hero>();
        if (hero != null)
        {
            hero.SetBoard(data.Area.Board);
            hero.SetCurrentTile();
        }
        //
        GameObject droppedUnit = held.Unit;
        OccupantKind droppedKind = held.Kind;
        ClearHeld();
        Replaced?.Invoke(droppedUnit, droppedKind);
        return true;
    }

    // 점유 자체는 무시하고, held는 목표 칸에 · 기존 점유자는 held의 원래 칸에 각각 들어갈 수 있어
    // 서로 자리를 맞바꿀 수 있는 조합인지만 살핀다(상태는 바꾸지 않는다). 미리보기 색상 판정과
    // 실제 스왑(TrySwap) 양쪽에서 같은 기준을 쓰기 위해 따로 뺐다.
    private bool CanSwapWith(PlaceData data, out GameObject otherUnit, out OccupantKind otherKind)
    {
        otherUnit = null;
        otherKind = OccupantKind.None;

        if (!IsHolding || data.Area == null)
        {
            return false;
        }

        Tile targetTile = data.Area.Board.Cells[data.Area.Origin];
        otherUnit = targetTile.OccupantObject;
        if (otherUnit == null)
        {
            return false;
        }

        otherKind = targetTile.State.Occupant;

        return TilePlacementRule.CanPlaceIgnoringOccupant(targetTile.State, held.Kind)
            && TilePlacementRule.CanPlaceIgnoringOccupant(held.FromTile.State, otherKind);
    }

    // 미리보기(칠하기)용: 지금 든 유닛을 이 자리에 놓을 수 있는지 — 바로 놓거나, 자리를 바꿔서라도.
    public bool CanPlaceOrSwap(PlaceData data)
    {
        return data.CanPlace || CanSwapWith(data, out _, out _);
    }

    // 목표 칸이 다른 유닛에 막혀 있을 때, 서로 자리를 맞바꿀 수 있으면 바꾸고 data를 갱신한다.
    // 점유가 아닌 다른 사유(본진·지형·기믹 등)로 막힌 경우이거나, 서로의 지형 조건이 안 맞으면 false.
    private bool TrySwap(ref PlaceData data)
    {
        if (!CanSwapWith(data, out GameObject otherUnit, out OccupantKind otherKind))
        {
            return false;
        }

        AreaPlace.Remove(data.Area);
        _unitList.Remove(otherUnit);

        PlaceData otherData = new PlaceData(held.FromArea, held.FromPosition, true);
        AreaPlace.Place(otherData, otherUnit, otherKind);
        _unitList.Add(otherUnit, held.FromArea);
        if (otherUnit.TryGetComponent(out Hero otherHero))
        {
            otherHero.SetBoard(held.FromArea.Board);
            otherHero.SetCurrentTile();
        }

        // 목표 칸이 이제 비었으니 held가 놓일 자리를 실제 높이 기준으로 다시 계산한다.
        float yOffset = data.Position.y - data.Area.Center.y;
        Vector3 resolvedPosition = AreaPlace.Position(data.Area, held.Kind, yOffset, out bool canPlaceNow);
        data = new PlaceData(data.Area, resolvedPosition, canPlaceNow);
        return canPlaceNow;
    }

    // 집은 유닛을 파괴하고 집은 상태를 해제한다.
    public void CancelHeldAndDestroy()
    {
        if (held.Unit.TryGetComponent(out Hero hero))
        {
            hero.PrepareForDespawn();
            PoolManager.Instance.Despawn(held.Unit);
        }
        else
        {
            UnityEngine.Object.Destroy(held.Unit);
        }
        ClearHeld();
    }

    private void ClearHeld()
    {
        held = default;
        OnHoldChanged?.Invoke();
    }
}
