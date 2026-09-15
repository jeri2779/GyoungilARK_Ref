using System.Collections.Generic;
using UnityEngine;

// 밤에 읽은 유닛 타일로 방향별 유닛 가림막을 조회합니다.
public class UnitShelter
{
    private readonly Dictionary<Vector2Int, bool> highByCell = new();

    // 동적으로 배치된 유닛 타일을 한 번 읽어 점유 칸과 그 칸의 고지 여부를 저장합니다.
    public UnitShelter(IReadOnlyList<Tile> units)
    {
        Collect(units);
    }

    // 대상 바로 한 칸 뒤(바람이 불어온 쪽)에 대상을 가릴 수 있는 높이의 유닛이 있는지 조회합니다.
    public bool IsSheltered(Tile target, Vector2Int wind)
    {
        Vector2Int behindCell = target.Coord - wind;
        if (!highByCell.TryGetValue(behindCell, out bool behindIsHigh))
        {
            return false;
        }

        return CanBlockWind(behindIsHigh, target.IsHigh);
    }

    // 이 칸에 유닛이 직접 서 있는지 조회합니다.
    public bool HasUnit(Vector2Int cell)
    {
        return highByCell.ContainsKey(cell);
    }

    // 뒤 유닛의 높이가 대상 유닛의 높이 이상일 때만 바람을 막을 수 있습니다.
    private static bool CanBlockWind(bool behindIsHigh, bool targetIsHigh)
    {
        if (!targetIsHigh)
        {
            return true;
        }

        return behindIsHigh;
    }

    // 유닛 수가 매번 달라지는 타일 목록을 한 번 훑어 점유 칸과 고지 여부를 모읍니다.
    private void Collect(IReadOnlyList<Tile> units)
    {
        for (int index = 0; index < units.Count; index++)
        {
            Tile tile = units[index];
            highByCell[tile.Coord] = tile.IsHigh;
        }
    }
}
