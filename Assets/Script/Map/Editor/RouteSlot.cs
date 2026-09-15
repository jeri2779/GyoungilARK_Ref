using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 새로 찍은 경유 칸이 기존 순서의 어디에 들어갈지 고르는 에디터 전용 도구.
///
/// 사람이 번호를 세지 않는다 — 지금 경로의 구간마다 그 칸을 끼웠을 때 길이가 얼마나 늘어나는지 재고,
/// 가장 조금 늘어나는 구간 사이에 넣는다. 길 끝쪽을 찍으면 자연히 맨 뒤에 붙는다.
///
/// 계산만 한다. 실제로 넣는 것은 RouteEdit이고, 예고줄도 같은 답을 써야 하므로 여기 한 곳에 둔다.
/// </summary>
public static class RouteSlot
{
    /// <summary>이 칸이 들어갈 자리. 0이면 맨 앞, 경유 칸 수와 같으면 맨 뒤다.</summary>
    public static int Best(
        IReadOnlyList<RouteNode> nodes,
        Vector2Int spawn,
        Vector2Int goal,
        Vector2Int coord)
    {
        if (nodes == null || nodes.Count == 0)
        {
            return 0;
        }

        int slot = 0;
        int cheapest = int.MaxValue;

        for (int i = 0; i <= nodes.Count; i++)
        {
            int added = Added(From(nodes, spawn, i), To(nodes, goal, i), coord);
            if (added < cheapest)
            {
                cheapest = added;
                slot = i;
            }
        }

        return slot;
    }

    // 이 구간에 칸을 끼우면 늘어나는 길이. 구간 위에 있으면 0이다.
    private static int Added(Vector2Int from, Vector2Int to, Vector2Int coord)
    {
        return GridCalculator.GetDistance(from, coord)
            + GridCalculator.GetDistance(coord, to)
            - GridCalculator.GetDistance(from, to);
    }

    // i번째 구간의 시작. 첫 구간은 스폰에서 나온다.
    private static Vector2Int From(IReadOnlyList<RouteNode> nodes, Vector2Int spawn, int i)
    {
        if (i == 0)
        {
            return spawn;
        }

        return nodes[i - 1].Coord;
    }

    // i번째 구간의 끝. 마지막 구간은 본진으로 들어간다.
    private static Vector2Int To(IReadOnlyList<RouteNode> nodes, Vector2Int goal, int i)
    {
        if (i == nodes.Count)
        {
            return goal;
        }

        return nodes[i].Coord;
    }
}
