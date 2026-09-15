using System;
using UnityEngine;

public static class WindDirectionText
{
    // 바람 벡터를 사람이 읽을 수 있는 방향 문구로 바꾼다.
    public static string ReadDirection(Vector2Int wind)
    {
        if (wind == GridCalculator.Down) return "북쪽 → 남쪽";
        if (wind == GridCalculator.Up) return "남쪽 → 북쪽";
        if (wind == GridCalculator.Right) return "서쪽 → 동쪽";
        if (wind == GridCalculator.Left) return "동쪽 → 서쪽";

        throw new ArgumentOutOfRangeException(nameof(wind));
    }
}
