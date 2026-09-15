using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저작한 경로(EnemyRouteSet.Entry)를 실제 웨이포인트로 편다.
///
/// 노드 사이는 그 이동 방식으로 갈 수 있는 최단 구간으로 이어 준다(맵 LaneBuilder.NodeSegment와 같은 방식) —
/// 끌어 그린 경로는 이웃 칸이 이어져 있어 그린 대로 나오고, 한 칸씩 찍은 경로는 사이가 자동으로 채워진다.
/// 마지막 노드에서 본진까지도 자동으로 잇는다.
///
/// MapBoard는 공개 API(Cells/TryGetCell)만 읽고 절대 수정하지 않는다 —
/// FlyingPathfinder·SwimPathfinder와 같은 전제(맵은 다른 담당 영역).
/// </summary>
public static class EnemyRoutePath
{
    /// <summary>공중 경로의 비행 고도. EnemyMovement가 FlyingPathfinder를 부를 때 쓰는 값과 같아야
    /// 저작 경로와 자동 경로의 높이가 어긋나지 않는다.</summary>
    public const float FlightHeight = 1.5f;

    /// <summary>이 경로의 웨이포인트(런타임). 경로를 만들 수 없으면 빈 목록.
    /// skipped를 주면 그 방식으로 못 지나는 저작 칸을 담아 준다.</summary>
    public static List<Vector3> Build(MapBoard board, EnemyRouteSet.Entry entry, List<Vector2Int> skipped = null)
    {
        if (board == null) return new List<Vector3>();
        return Build(board.Cells, entry, skipped);
    }

    /// <summary>격자 사전을 직접 받는 판. 저작 창은 MapBoard.Build를 거치지 않아 board.Cells가 비어 있으므로
    /// ModuleScan.MapCells가 만든 사전을 그대로 넘긴다(LaneQuery가 LaneBuilder에 넘기는 것과 같은 방식).</summary>
    public static List<Vector3> Build(IReadOnlyDictionary<Vector2Int, Tile> cells, EnemyRouteSet.Entry entry,
        List<Vector2Int> skipped = null)
    {
        var points = new List<Vector3>();

        List<Tile> tiles = BuildTiles(cells, entry, skipped);
        if (tiles == null) return points;

        // 공중은 지형 높이를 무시하고 "시작 높이 + 고도"로 고정한다(FlyingPathfinder와 같은 규칙) —
        // 타일 윗면을 그대로 쓰면 고지를 지날 때 위아래로 튄다.
        bool level = entry.Kind == EnemyRouteKind.Air;
        float flightY = tiles[0].WorldTop.y + FlightHeight;

        for (int i = 0; i < tiles.Count; i++)
        {
            Vector3 top = tiles[i].WorldTop;
            points.Add(level ? new Vector3(top.x, flightY, top.z) : top);
        }

        return points;
    }

    /// <summary>이 경로의 타일 목록(스폰→본진). 스폰 칸이 없거나 본진까지 못 이으면 null.
    /// 저작 창이 격자에 그리는 데도 쓴다 — 런타임과 같은 해석기를 봐야 그린 대로 나오는지 알 수 있다.
    ///
    /// 이웃 연결(Tile.NeighborTiles)이 이미 되어 있어야 한다. 런타임은 MapBoard.Build가 해 주고,
    /// 에디터는 호출부가 TileLink.LinkNeighbors를 먼저 불러야 한다(LaneQuery와 같은 전제).</summary>
    public static List<Tile> BuildTiles(IReadOnlyDictionary<Vector2Int, Tile> cells, EnemyRouteSet.Entry entry,
        List<Vector2Int> skipped = null)
    {
        if (cells == null || entry == null || entry.Nodes == null) return null;
        if (!cells.TryGetValue(entry.Spawn, out Tile start)) return null;

        Func<Tile, bool> canPass = Pass(entry.Kind);
        if (!canPass(start)) return null; // 스폰 칸 자체를 못 지나면 그 방식으로는 나올 수 없다

        var path = new List<Tile> { start };
        Tile current = start;

        for (int i = 0; i < entry.Nodes.Count; i++)
        {
            Vector2Int coord = entry.Nodes[i];

            // 못 지나는 노드는 건너뛴다 — 한 칸 때문에 경로 전체가 무효화되지 않게(맵 GetNodes와 같은 취지).
            // 다만 조용히 버리지 않고 skipped로 알린다. 맵 쪽은 말없이 버려서 원인을 알 수 없다.
            if (!cells.TryGetValue(coord, out Tile node) || !canPass(node))
            {
                skipped?.Add(coord);
                continue;
            }

            if (node == current) continue; // 같은 칸을 연달아 찍은 경우

            List<Tile> segment = Segment(current, node, canPass);
            if (segment == null) return null;

            Append(path, segment);
            current = node;
        }

        List<Tile> tail = ToCore(cells, current, canPass);
        if (tail == null) return null;

        Append(path, tail);
        return path;
    }

    /// <summary>이 이동 방식의 통행 판정. 흩어져 있던 규칙을 여기 한 곳에 모은다.</summary>
    public static Func<Tile, bool> Pass(EnemyRouteKind kind)
    {
        // 공중은 고지·벽·물을 가리지 않지만 외곽 장식 줄은 판 밖이라 지나지 않는다
        // (FlyingPathfinder.PassInner와 같은 규칙 — 두 곳이 어긋나면 저작 경로와 폴백 경로가 달라진다).
        if (kind == EnemyRouteKind.Air) return tile => !tile.IsSpecial;

        // 헤엄은 물 칸이 열리고 고지·빈 칸은 여전히 막힌다(Tile.CanPass 그대로).
        return tile => tile.CanPass(PassType.Swim);
    }

    // 한 지점에서 지정 칸까지 이 방식으로 갈 수 있는 최단 구간.
    private static List<Tile> Segment(Tile from, Tile to, Func<Tile, bool> canPass)
    {
        return Pathfinder.FindPath(
            new[] { from },
            tile => tile == to,
            canPass,
            tile => GridCalculator.GetDistance(tile.Coord, to.Coord));
    }

    // 마지막 노드에서 본진까지. 도착 판정은 좌표가 아니라 IsCore로 한다 —
    // 타일 좌표는 모듈 로컬이라 보드가 여러 개면 다른 보드의 같은 좌표에 걸린다(Tile.Board 주석 참조).
    private static List<Tile> ToCore(IReadOnlyDictionary<Vector2Int, Tile> cells, Tile from, Func<Tile, bool> canPass)
    {
        List<Vector2Int> cores = CoreCoords(cells);

        return Pathfinder.FindPath(
            new[] { from },
            tile => tile.IsCore,
            canPass,
            tile => Nearest(tile.Coord, cores));
    }

    // 이 보드의 본진 좌표들. 휴리스틱(탐색 방향 힌트)에만 쓰므로 매번 모아도 경로 정확도에 영향이 없다.
    private static List<Vector2Int> CoreCoords(IReadOnlyDictionary<Vector2Int, Tile> cells)
    {
        var cores = new List<Vector2Int>();
        foreach (Tile tile in cells.Values)
        {
            if (tile.IsCore) cores.Add(tile.Coord);
        }

        return cores;
    }

    // 가장 가까운 본진까지의 칸 거리. 본진이 없으면 0(=휴리스틱 없음)으로 두어 탐색만 계속하게 한다.
    private static int Nearest(Vector2Int from, List<Vector2Int> cores)
    {
        int best = int.MaxValue;
        for (int i = 0; i < cores.Count; i++)
        {
            int gap = GridCalculator.GetDistance(from, cores[i]);
            if (gap < best) best = gap;
        }

        return best == int.MaxValue ? 0 : best;
    }

    // 구간을 이어붙인다. 첫 타일은 앞 구간의 끝과 겹치므로 건너뛴다.
    private static void Append(List<Tile> path, List<Tile> segment)
    {
        for (int i = 1; i < segment.Count; i++)
        {
            path.Add(segment[i]);
        }
    }
}
