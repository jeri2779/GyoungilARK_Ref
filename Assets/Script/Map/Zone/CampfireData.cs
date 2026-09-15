using System.Collections.Generic;
using UnityEngine;

// 모닥불이 보호하는 실제 보드 좌표를 보관합니다.
public class CampfireData
{
    private readonly HashSet<Vector2Int> cells = new();

    // 모닥불 타일 하나하나가 자기 혼자만의 범위를 따로 들고 있습니다(면역 판정용 통합 좌표와는 다른 용도).
    private readonly Dictionary<Tile, List<Tile>> ranges = new();

    public IReadOnlyCollection<Vector2Int> Cells => cells;
    public int Count => cells.Count;

    // 보호 좌표를 중복 없이 보관합니다.
    public void Keep(Vector2Int cell)
    {
        cells.Add(cell);
    }

    // 이 모닥불 타일 혼자만의 범위를 보관합니다.
    public void KeepRange(Tile origin, List<Tile> range)
    {
        ranges[origin] = range;
    }

    // 지정 좌표가 보호 영역인지 확인합니다.
    public bool Contains(Vector2Int cell)
    {
        return cells.Contains(cell);
    }

    // 이 모닥불 타일 혼자만의 범위를 가져옵니다. 모닥불 원점이 아니면 실패합니다.
    public bool TryGetRange(Tile origin, out List<Tile> range)
    {
        return ranges.TryGetValue(origin, out range);
    }
}
