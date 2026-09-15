using System;
using UnityEngine;

public static class WindShelterQuery
{
    // 이 바람 방향으로 막혀 있는지 확인한다.
    public static bool IsSheltered(WindShelter shelter, Vector2Int wind)
    {
        WindShelter currentShelter = ResolveDirection(wind);
        return (shelter & currentShelter) == currentShelter;
    }

    // 바람 벡터를 그 방향을 막는 데 필요한 값으로 바꾼다.
    private static WindShelter ResolveDirection(Vector2Int wind)
    {
        if (wind == GridCalculator.Down) return WindShelter.FromNorth;
        if (wind == GridCalculator.Up) return WindShelter.FromSouth;
        if (wind == GridCalculator.Right) return WindShelter.FromWest;
        if (wind == GridCalculator.Left) return WindShelter.FromEast;

        throw new ArgumentOutOfRangeException(nameof(wind));
    }
}
