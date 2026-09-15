using System;
using System.Collections.Generic;

/// <summary>
/// 칸마다 이동 비용이 다른 A*. Map/Core/Pathfinder와 알고리즘은 같고, 한 칸 비용이
/// 고정(StepCost=1)이 아니라 "어느 칸에서 어느 칸으로 넘어가는가"로 정해지는 것만 다르다.
///
/// Pathfinder에 stepCost 인자를 하나 더 다는 편이 깔끔하지만 Map은 다른 담당 영역이라
/// 수정하지 않는다(SwimPathfinder의 "절대 수정하지 않는다"와 같은 취지). TileHeap은 공개 타입이라
/// 읽기만 하며 그대로 재사용한다.
///
/// 비용이 간선(from→to) 기준이어도 칸 단위 bestCost/closed는 그대로 유효하다 —
/// 비용이 (from, to)만으로 정해지고 "여기까지 어떻게 왔는지"가 이후 간선 값을 바꾸지 않기 때문.
/// </summary>
public static class WeightedPathfinder
{
    private const int StartCost = 0; // 출발 칸까지 온 비용(아직 한 칸도 안 움직였다)

    /// <summary>시작 칸들에서 목표 칸까지 총비용이 가장 싼 길을 찾는다. 없으면 null.</summary>
    /// <param name="stepCost">(현재 칸, 이웃 칸) → 그 이웃으로 넘어가는 비용. 0 이상이어야 한다.</param>
    /// <param name="heuristic">남은 비용의 과소평가치. 최소 한 칸 비용을 곱해 두지 않으면
    /// 실제 남은 비용을 넘겨 버려(과대평가) 최단이 아닌 길이 조용히 나온다.</param>
    public static List<Tile> FindPath(
        IReadOnlyList<Tile> sources,
        Func<Tile, bool> isGoal,
        Func<Tile, bool> canPass,
        Func<Tile, Tile, int> stepCost,
        Func<Tile, int> heuristic)
    {
        var cameFrom = new Dictionary<Tile, Tile>();
        var bestCost = new Dictionary<Tile, int>();
        var closed = new HashSet<Tile>();
        var open = new TileHeap();

        for (int i = 0; i < sources.Count; i++)
        {
            Tile source = sources[i];

            if (bestCost.ContainsKey(source))
            {
                continue;
            }

            if (isGoal(source))
            {
                return new List<Tile> { source };
            }

            bestCost[source] = StartCost;
            open.Push(source, heuristic(source));
        }

        while (open.Count > 0)
        {
            Tile current = open.Pop();

            if (closed.Contains(current))
            {
                continue; // 더 싼 점수로 이미 처리된 칸 — 이 항목은 낡았다
            }

            closed.Add(current);

            if (isGoal(current))
            {
                return Reconstruct(cameFrom, current);
            }

            int costHere = bestCost[current];
            ReadOnlySpan<Tile> neighbors = current.NeighborTiles;

            for (int i = 0; i < neighbors.Length; i++)
            {
                Relax(neighbors[i], current, costHere, canPass, stepCost, heuristic, cameFrom, bestCost, open);
            }
        }

        return null;
    }

    // 이 이웃으로 넘어가는 것이 지금까지 알려진 것보다 싸면 기록을 갱신하고 새 점수로 힙에 넣는다.
    private static void Relax(
        Tile neighbor, Tile current, int costHere,
        Func<Tile, bool> canPass, Func<Tile, Tile, int> stepCost, Func<Tile, int> heuristic,
        Dictionary<Tile, Tile> cameFrom, Dictionary<Tile, int> bestCost, TileHeap open)
    {
        if (!canPass(neighbor))
        {
            return;
        }

        int reached = costHere + stepCost(current, neighbor);

        if (bestCost.TryGetValue(neighbor, out int known) && reached >= known)
        {
            return;
        }

        cameFrom[neighbor] = current;
        bestCost[neighbor] = reached;
        open.Push(neighbor, reached + heuristic(neighbor));
    }

    // 도착 칸에서 온 길을 거꾸로 되짚어 스폰부터의 순서로 편다.
    private static List<Tile> Reconstruct(Dictionary<Tile, Tile> cameFrom, Tile goal)
    {
        var path = new List<Tile> { goal };
        Tile current = goal;

        while (cameFrom.TryGetValue(current, out Tile previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
