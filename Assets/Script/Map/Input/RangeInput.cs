using System.Collections.Generic;
using UnityEngine;

// 마우스를 누르고 있는 동안에만 사거리 보관소가 차 있게 한다.
// 사거리를 직접 구하거나 그리지는 않고 RangeCalc·RangeStore에 시킨다.
public class RangeInput : MonoBehaviour
{
    [SerializeField] private MapInput input;

    // MapAssemble이 조립할 때 넣어준다
    public PointerPick pointerPick;
    public RangeCalc rangeCalc;
    public RangeTileData rangeStore;

    private void OnEnable()
    {
        input.Pressed += KeepRange;
    }

    private void OnDisable()
    {
        input.Pressed -= KeepRange;
    }

    private void Update()
    {
        DropRange();
    }

    // 누른 자리의 유닛이 닿는 칸을 보관소에 채운다.
    private void KeepRange()
    {
        Tile tile = pointerPick.UnderPointer();
        if (rangeCalc.TryGetRange(tile, out List<Tile> range))
        {
            rangeStore.KeepRange(range, RangeCalc.ResolveEdgeKind(tile));
            return;
        }

        rangeStore.ClearRange();
    }

    // 손을 떼면 보관소를 비운다.
    // UI 위에서 떼면 뗌 신호가 오지 않으므로 눌림 상태를 직접 본다.
    private void DropRange()
    {
        if (CanDropRange())
        {
            rangeStore.ClearRange();
        }
    }

    private bool IsInputReleased()
    {
        return input.LeftHolding == false;
    }

    // 손을 뗐고 아직 지울 범위가 남아있을 때만 비우기를 허락한다.
    private bool CanDropRange()
    {
        return IsInputReleased() && rangeStore.HasRange;
    }
}
