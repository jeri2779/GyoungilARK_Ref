using UnityEngine;

// 시드와 일차만으로 그날의 바람 방향을 계산한다 (같은 세이브면 항상 같은 결과).
public static class WindPicker
{
    private const int StartIndex = 0;   // 1일차 이전의 기준 방향 = Directions[0](오른쪽)

    // 시드와 일차로 그날의 바람 방향을 계산한다.
    public static Vector2Int Pick(string seed, int dayCount)
    {
        int index = ReadIndex(seed, dayCount);
        return GridCalculator.Directions[index];
    }

    // 1일차부터 오늘까지 방향 전환을 되짚어 오늘의 방향 번호를 계산한다.
    private static int ReadIndex(string seed, int dayCount)
    {
        int index = StartIndex;
        for (int day = 1; day <= dayCount; day++)
        {
            index = (index + ReadOffset(seed, day)) % GridCalculator.Directions.Length;
        }

        return index;
    }

    // 그날의 방향 전환 칸수를 1~3 중 하나로 계산한다 (0을 빼서 어제와 같은 방향을 막는다).
    private static int ReadOffset(string seed, int day)
    {
        int derived = GameSeeding.Derive($"{seed}:desertwind", day);
        return 1 + (int)((uint)derived % (uint)(GridCalculator.Directions.Length - 1));
    }
}
