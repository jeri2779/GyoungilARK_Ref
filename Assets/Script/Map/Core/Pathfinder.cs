using System.Collections.Generic;
using System;

 //탐색 알고리즘(임시용)
public static class Pathfinder
{
    // 출발 칸까지 온 비용(아직 한 칸도 안 움직였다)
    private const int StartCost = 0;

    // 이웃 칸으로 한 칸 옮기는 비용
    private const int StepCost = 1;

    // 시작 칸에서 목표 칸까지 가장 짧은 길을 찾는다(A*). 이웃은 타일이 미리 이어 둔 목록에서 읽는다.
    //
    // "다음에 살필 칸"은 힙에서 꺼낸다 — 목록을 훑어 최솟값을 매번 다시 찾지 않는다.
    // 점수(비용+예상거리)는 힙에 넣는 순간 한 번만 계산해 같이 담아 둔다 — 꺼낼 때마다 다시 계산하지 않는다.
    // 더 싼 경로가 나중에 발견되면 새 점수로 다시 넣고, 먼저 나온 낡은 항목은 closed로 걸러낸다.
    public static List<Tile> FindPath(
        IReadOnlyList<Tile> sources,
        Func<Tile, bool> isGoal,
        Func<Tile, bool> canPass,
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

            int reached = bestCost[current] + StepCost;
            ReadOnlySpan<Tile> neighbors = current.NeighborTiles;

            for (int i = 0; i < neighbors.Length; i++)
            {
                Relax(neighbors[i], current, reached, canPass, heuristic, cameFrom, bestCost, open);
            }
        }

        return null;
    }

    // 이웃 칸으로 넘어가는 것이 이득이면 기록을 갱신하고 새 점수로 힙에 넣는다.
    private static void Relax(
        Tile neighbor, Tile current, int reached,
        Func<Tile, bool> canPass, Func<Tile, int> heuristic,
        Dictionary<Tile, Tile> cameFrom, Dictionary<Tile, int> bestCost, TileHeap open)
    {
        if (!canPass(neighbor))
        {
            return;
        }

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
