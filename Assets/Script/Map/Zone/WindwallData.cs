using System.Collections.Generic;
using UnityEngine;

// 가림막이 막아주는 칸을 바람 방향별로 나눠 보관합니다.
public class WindwallData
{
    private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> armsByWind = new();

    // 가림막 타일마다 바람 방향별로 나눈 범위를 따로 들고 있습니다(면역 판정용 통합 표와는 다른 용도).
    private readonly Dictionary<Vector2Int, Dictionary<Vector2Int, List<Tile>>> rangesByWind = new();

    // 네 방향 빈 세트를 미리 만들어 둡니다. 채울 때 방향 존재 검사가 필요 없어집니다.
    public WindwallData()
    {
        for (int index = 0; index < GridCalculator.Directions.Length; index++)
        {
            armsByWind[GridCalculator.Directions[index]] = new HashSet<Vector2Int>();
            rangesByWind[GridCalculator.Directions[index]] = new Dictionary<Vector2Int, List<Tile>>();
        }
    }

    // 이 바람 방향에서 보호되는 칸 하나를 보관합니다.
    public void KeepArm(Vector2Int wind, Vector2Int cell)
    {
        armsByWind[wind].Add(cell);
    }

    // 이 칸이 오늘 바람 방향의 가림막 팔에 들어 있는지 확인합니다.
    public bool HasArm(Vector2Int wind, Vector2Int cell)
    {
        return armsByWind[wind].Contains(cell);
    }

    // 이 가림막 타일이 그 바람에서 막아주는 범위를 보관합니다.
    public void KeepRange(Vector2Int wind, Vector2Int origin, List<Tile> range)
    {
        rangesByWind[wind][origin] = range;
    }

    // 그 바람에서 이 가림막이 막아주는 범위를 가져옵니다. 가림막 원점이 아니면 실패합니다.
    public bool TryGetRange(Vector2Int wind, Vector2Int origin, out List<Tile> range)
    {
        return rangesByWind[wind].TryGetValue(origin, out range);
    }
}
