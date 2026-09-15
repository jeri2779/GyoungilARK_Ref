using System;
using System.Collections.Generic;
using UnityEngine;

public class LaneBuilder : ILaneBuilder
{
    private readonly RouteConfig routes;

    // 저작 경로 보관처를 받습니다. 없으면 항상 최단 경로로 계산합니다.
    public LaneBuilder(RouteConfig config = null)
    {
        routes = config;
    }

    // 모든 스폰을 좌표순으로 정렬하고 스폰마다 독립된 레인을 만듭니다. coreDistance를 생략하면 직접 계산합니다.
    public IReadOnlyList<LaneData> BuildLanes(IReadOnlyDictionary<Vector2Int, Tile> cells, IReadOnlyList<Tile> spawnTiles, IReadOnlyList<Tile> coreTiles, IReadOnlyDictionary<Tile, int> coreDistance = null)
    {
        var lanes = new List<LaneData>();
        var spawns = new List<Tile>(spawnTiles);
        var cores = new HashSet<Tile>(coreTiles);

        if (coreDistance == null)
        {
            coreDistance = BuildCoreDistance(cells, coreTiles);
        }

        spawns.Sort(CompareSpawn);

        for (int i = 0; i < spawns.Count; i++)
        {
            AddSpawnLanes(cells, spawns[i], cores, coreDistance, lanes);
        }

        return lanes;
    }

    // 타일마다 가장 가까운 본진까지 칸 거리를 미리 계산합니다.
    private static Dictionary<Tile, int> BuildCoreDistance(IReadOnlyDictionary<Vector2Int, Tile> cells, IReadOnlyList<Tile> cores)
    {
        var result = new Dictionary<Tile, int>();
        foreach (Tile tile in cells.Values)
        {
            result[tile] = ComputeNearestCore(tile, cores);
        }
        return result;
    }

    // 한 타일에서 가장 가까운 본진까지 칸 거리를 잰다. BuildCoreDistance가 한 번만 부른다.
    private static int ComputeNearestCore(Tile tile, IReadOnlyList<Tile> cores)
    {
        if (cores.Count == 0)
        {
            return 0;
        }

        int best = int.MaxValue;
        for (int i = 0; i < cores.Count; i++)
        {
            int distance = GridCalculator.GetDistance(tile.Coord, cores[i].Coord);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    // 이 스폰에 지정된 경로마다 레인 하나를 만듭니다. 지정이 없으면 자동 최단 경로 하나만 냅니다.
    private void AddSpawnLanes(IReadOnlyDictionary<Vector2Int, Tile> cells, Tile spawn, HashSet<Tile> cores, IReadOnlyDictionary<Tile, int> coreDistance, List<LaneData> lanes)
    {
        List<RouteData> spawnRoutes = GetRoutes(spawn);

        if (spawnRoutes.Count == 0)
        {
            lanes.Add(BuildLane(cells, spawn, null, cores, coreDistance));
            return;
        }

        for (int index = 0; index < spawnRoutes.Count; index++)
        {
            lanes.Add(BuildLane(cells, spawn, spawnRoutes[index], cores, coreDistance));
        }
    }

    // 레인 하나를 걷기·헤엄 두 통행 방식으로 각각 계산해 함께 담습니다. 걷기가 실패하면 빈 레인을 냅니다.
    // 헤엄은 걷기보다 지날 수 있는 칸이 더 넓어(물+걷는 칸) 걷기가 되면 헤엄도 항상 됩니다.
    private LaneData BuildLane(IReadOnlyDictionary<Vector2Int, Tile> cells, Tile spawn, RouteData route, HashSet<Tile> cores, IReadOnlyDictionary<Tile, int> coreDistance)
    {
        if (cores.Count == 0)
        {
            return new LaneData(spawn, null, route, Array.Empty<Tile>(), Array.Empty<Tile>());
        }

        List<Tile> walk = FindRoute(cells, spawn, route, cores, coreDistance, PassType.Walk);

        if (walk == null || walk.Count == 0)
        {
            return new LaneData(spawn, null, route, Array.Empty<Tile>(), Array.Empty<Tile>());
        }

        List<Tile> swim = FindRoute(cells, spawn, route, cores, coreDistance, PassType.Swim);
        Tile goal = walk[walk.Count - 1];
        return new LaneData(spawn, goal, route, walk, swim ?? EmptyTiles);
    }

    // 헤엄 계산이 실패했을 때 대신 담는 빈 목록.
    private static readonly List<Tile> EmptyTiles = new();

    // 지정 경로가 있으면 그 노드를 따르고, 없으면 최단 경로를 씁니다.
    private List<Tile> FindRoute(IReadOnlyDictionary<Vector2Int, Tile> cells, Tile spawn, RouteData route, HashSet<Tile> cores, IReadOnlyDictionary<Tile, int> coreDistance, PassType pass)
    {
        if (route == null)
        {
            return AutoPath(spawn, cores, coreDistance, pass);
        }

        List<Tile> nodes = GetNodes(cells, route.Nodes, pass);
        return NodePath(spawn, nodes, cores, coreDistance, pass);
    }

    // 이 스폰에 지정된 경로들. 보관처가 없거나 지정이 없으면 빈 목록입니다.
    private List<RouteData> GetRoutes(Tile spawn)
    {
        if (routes == null)
        {
            return EmptyRoutes;
        }

        if (routes.TryGetRoutes(spawn.Coord, out List<RouteData> found))
        {
            return found;
        }

        return EmptyRoutes;
    }

    private static readonly List<RouteData> EmptyRoutes = new();

    // 저작 좌표 중 이 통행 방식으로 지날 수 있는 칸만 추립니다. 못 지나는 칸은 건너뛰어,
    // 그 한 칸 때문에 경로 전체가 무효화되지 않게 합니다 — 남은 칸 사이는 자동 경로가 이어줍니다.
    private static List<Tile> GetNodes(
        IReadOnlyDictionary<Vector2Int, Tile> cells,
        IReadOnlyList<RouteNode> coords,
        PassType pass)
    {
        var nodes = new List<Tile>(coords.Count);

        for (int i = 0; i < coords.Count; i++)
        {
            bool found = cells.TryGetValue(coords[i].Coord, out Tile tile);

            if (!found || !tile.CanPass(pass))
            {
                continue;
            }

            nodes.Add(tile);
        }

        return nodes;
    }

    // 스폰에서 노드를 차례로 거쳐 코어까지 이어진 경로를 만듭니다.
    private static List<Tile> NodePath(
        Tile spawn,
        IReadOnlyList<Tile> nodes,
        HashSet<Tile> cores,
        IReadOnlyDictionary<Tile, int> coreDistance,
        PassType pass)
    {
        var path = new List<Tile> { spawn };
        Tile current = spawn;

        for (int i = 0; i < nodes.Count; i++)
        {
            List<Tile> segment = NodeSegment(current, nodes[i], pass);

            if (segment == null)
            {
                return null;
            }

            Append(path, segment);
            current = nodes[i];
        }

        List<Tile> last = AutoPath(current, cores, coreDistance, pass);

        if (last == null)
        {
            return null;
        }

        Append(path, last);
        return path;
    }

    // 한 지점에서 지정된 노드까지, 이 통행 방식으로 갈 수 있는 최단 구간을 계산합니다.
    private static List<Tile> NodeSegment(Tile from, Tile node, PassType pass)
    {
        return Pathfinder.FindPath(
            new[] { from },
            tile => tile == node,
            tile => tile.CanPass(pass),
            tile => GridCalculator.GetDistance(tile.Coord, node.Coord));
    }

    // 코어까지, 이 통행 방식으로 갈 수 있는 최단 경로를 계산합니다.
    private static List<Tile> AutoPath(Tile from, HashSet<Tile> cores, IReadOnlyDictionary<Tile, int> coreDistance, PassType pass)
    {
        return Pathfinder.FindPath(
            new[] { from },
            tile => cores.Contains(tile),
            tile => tile.CanPass(pass),
            tile => NearestCore(tile, coreDistance));
    }

    // 가장 가까운 본진까지의 칸 거리를 꺼냅니다.
    private static int NearestCore(Tile tile, IReadOnlyDictionary<Tile, int> coreDistance)
    {
        coreDistance.TryGetValue(tile, out int distance);
        return distance;
    }

    // 구간을 이어붙입니다. 첫 타일은 앞 구간의 끝과 겹치므로 건너뜁니다.
    private static void Append(List<Tile> path, List<Tile> segment)
    {
        for (int i = 1; i < segment.Count; i++)
        {
            path.Add(segment[i]);
        }
    }

    // 스폰을 X 좌표 우선, Y 좌표 차순으로 비교합니다.
    private static int CompareSpawn(Tile left, Tile right)
    {
        int column = left.Coord.x.CompareTo(right.Coord.x);
        if (column != 0)
        {
            return column;
        }

        return left.Coord.y.CompareTo(right.Coord.y);
    }
}
