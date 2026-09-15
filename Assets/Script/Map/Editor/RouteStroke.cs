using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마우스를 끌 때 프레임 사이에 건너뛴 칸을 메우는 에디터 전용 도구.
///
/// 빠르게 끌면 칸이 듬성듬성 찍힌다. 그대로 두면 사람이 뱅글 돌려 그린 구간이
/// 최단 거리로 잘려 나가 그린 모양과 실제 경로가 달라진다.
/// 가로로 먼저 가고 세로로 꺾어 빈틈없이 잇는다.
/// </summary>
public static class RouteStroke
{
    /// <summary>앞 칸 다음부터 이 칸까지 한 칸씩. 앞 칸은 빼고 낸다.</summary>
    public static List<Vector2Int> Between(Vector2Int from, Vector2Int to)
    {
        var steps = new List<Vector2Int>();
        Vector2Int step = from;

        while (step.x != to.x)
        {
            step.x += Toward(step.x, to.x);
            steps.Add(step);
        }

        while (step.y != to.y)
        {
            step.y += Toward(step.y, to.y);
            steps.Add(step);
        }

        return steps;
    }

    private static int Toward(int from, int to)
    {
        if (to > from)
        {
            return 1;
        }

        return -1;
    }
}
