using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 좌표 격자에서 스폰→본진 레인을 구성하는 에디터 전용 도구.
/// 넘겨받은 사전만 읽고 어떤 공유 상태도 건드리지 않는다 — MapBoard.GetPath와 달리
/// EnemyLane을 켜지 않는다(저작 중 미리보기가 게임 상태를 바꾸면 안 되므로).
///
/// 런타임과 같은 LaneBuilder를 사용해 스폰마다 독립된 결과를 만든다.
/// 막힌 스폰도 LaneData에 남겨 저작 단계에서 바로 확인할 수 있게 한다.
/// </summary>
public static class LaneQuery
{
    // 셀에서 스폰과 코어를 수집하고 스폰별 레인 결과를 계산합니다.
    // 저작된 경로가 있으면 그대로 따라가 창의 선이 게임에서 걷는 길과 같아집니다.
    public static List<LaneData> BuildLanes(Dictionary<Vector2Int, Tile> cells, RouteConfig routes)
    {
        TileLink.LinkNeighbors(cells);   // 창은 MapBoard.Build를 거치지 않으므로 여기서 직접 잇는다
        List<Tile> spawns = CollectSpawns(cells);
        List<Tile> cores = CollectCores(cells);
        var builder = new LaneBuilder(routes);
        IReadOnlyList<LaneData> built = builder.BuildLanes(cells, spawns, cores);
        var lanes = new List<LaneData>(built.Count);

        for (int i = 0; i < built.Count; i++)
        {
            lanes.Add(built[i]);
        }

        return lanes;
    }

    // 셀에서 적 스폰으로 지정된 타일만 수집합니다.
    public static List<Tile> CollectSpawns(Dictionary<Vector2Int, Tile> cells)
    {
        var spawns = new List<Tile>();
        foreach (Tile tile in cells.Values)
        {
            if (tile.IsEnemySpawn)
            {
                spawns.Add(tile);
            }
        }

        return spawns;
    }

    // 셀에서 코어로 지정된 타일만 수집합니다.
    public static List<Tile> CollectCores(Dictionary<Vector2Int, Tile> cells)
    {
        var cores = new List<Tile>();
        foreach (Tile tile in cells.Values)
        {
            if (tile.IsCore)
            {
                cores.Add(tile);
            }
        }

        return cores;
    }
}
