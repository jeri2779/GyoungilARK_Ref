using System.Collections.Generic;
using UnityEngine;

// 배치된 유닛과 그 유닛이 선 자리를 보관하는 장부.
public class PlacedUnitData
{
    private readonly List<GameObject> _units = new();
    private readonly Dictionary<GameObject, PlacementArea> _areaByUnit = new();

    public int Count => _units.Count;

    // 인덱스로 유닛을 꺼낸다.
    public GameObject UnitAt(int index)
    {
        return _units[index];
    }

    // 인덱스로 그 유닛의 자리를 꺼낸다.
    public PlacementArea AreaAt(int index)
    {
        return _areaByUnit[_units[index]];
    }

    // 유닛과 자리를 함께 올린다.
    public void Add(GameObject unit, PlacementArea area)
    {
        _units.Add(unit);
        _areaByUnit[unit] = area;
    }

    // 유닛과 자리를 함께 내린다.
    public void Remove(GameObject unit)
    {
        _units.Remove(unit);
        _areaByUnit.Remove(unit);
    }

    // 장부를 통째로 비운다.
    public void Clear()
    {
        _units.Clear();
        _areaByUnit.Clear();
    }

    // 유닛이 선 자리를 찾는다. 장부에 없으면 false.
    public bool TryGetArea(GameObject unit, out PlacementArea area)
    {
        return _areaByUnit.TryGetValue(unit, out area);
    }
}
