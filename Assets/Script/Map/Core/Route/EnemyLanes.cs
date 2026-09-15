using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MapBoard))]
[DefaultExecutionOrder(100)]
public class EnemyLanes : MonoBehaviour
{
    [SerializeField] private MapBoard board;
    [SerializeField] private RouteConfig routes;

    private readonly List<LaneData> lanes = new();

    // 스폰마다 그 스폰에서 갈라지는 레인들. 계산 단계가 만들면서 밀어 넣는다.
    private readonly List<List<LaneData>> spawnLanes = new();
    private readonly Dictionary<Tile, int> spawnSlot = new();
    private List<IReadOnlyList<Vector3>> _cachedWalkPaths0;

    private ILaneBuilder builder;

    public IReadOnlyList<LaneData> Lanes => lanes;
    public bool IsReady { get; private set; }

    // 스폰 수. GetPaths가 내는 목록과 번호가 그대로 짝이 된다.
    public int SpawnCount => spawnLanes.Count;

    public event Action Changed;

    // 같은 GameObject의 MapBoard와 RouteConfig를 인스펙터 참조에 자동 할당합니다.
    private void Reset()
    {
        board = GetComponent<MapBoard>();
        routes = GetComponent<RouteConfig>();
    }

    // 필수 참조를 준비하고 현재 맵의 모든 적 이동 레인을 계산합니다.
    private void Awake()
    {
        Prepare();
        RefreshLanes();
    }

    // 기존 레인을 비우고 현재 MapBoard 상태를 기준으로 전체 레인을 다시 계산합니다.
    public void RefreshLanes()
    {
        IsReady = false;
        _cachedWalkPaths0 = null;
        lanes.Clear();
        spawnLanes.Clear();
        spawnSlot.Clear();
        Prepare();

        if (!CanBuild())
        {
            Changed?.Invoke();
            return;
        }

        CollectEndpoints(out List<Tile> spawns, out List<Tile> cores);
        IReadOnlyList<LaneData> built = builder.BuildLanes(board.Cells, spawns, cores, board.CoreDistance);
        ApplyLanes(built);
    }

    // 이 일차로 다시 계산한다. 날짜가 바뀌면 MapAssemble이 부른다.
    public void RefreshForDay(int day)
    {
        if (routes != null)
        {
            routes.SetActiveDay(day);
        }

        RefreshLanes();
    }

    // 레인 계산에 사용할 ILaneBuilder 구현체를 교체합니다.
    public void SetBuilder(ILaneBuilder value)
    {
        if (value == null)
        {
            return;
        }

        builder = value;
    }

    // 스폰마다 대표 경로 하나. 번호가 SpawnCount와 짝이 맞아야 해서 막힌 스폰도 빈 자리로 남긴다.
    // pass를 생략하면 걷기 경로를 낸다.
    public IReadOnlyList<IReadOnlyList<Vector3>> GetPaths(float yOffset, PassType pass = PassType.Walk)
    {
        if (Mathf.Abs(yOffset) < 1e-5f && pass == PassType.Walk && _cachedWalkPaths0 != null)
        {
            return _cachedWalkPaths0;
        }

        var paths = new List<IReadOnlyList<Vector3>>();

        if (!IsReady)
        {
            return paths;
        }

        for (int i = 0; i < spawnLanes.Count; i++)
        {
            paths.Add(spawnLanes[i][0].GetPoints(pass, yOffset));
        }

        if (Mathf.Abs(yOffset) < 1e-5f && pass == PassType.Walk)
        {
            _cachedWalkPaths0 = paths;
        }

        return paths;
    }

    // 이 스폰에서 갈라지는 길 수.
    public int BranchCount(int spawnIndex)
    {
        return spawnLanes[spawnIndex].Count;
    }

    // 이 스폰의 이 갈래 경로. 적 한 마리가 그대로 받아 걷는다. pass를 생략하면 걷기 경로를 낸다.
    public IReadOnlyList<Vector3> GetBranchPath(int spawnIndex, int branchIndex, float yOffset, PassType pass = PassType.Walk)
    {
        return spawnLanes[spawnIndex][branchIndex].GetPoints(pass, yOffset);
    }

    // MapBoard 참조와 기본 LaneBuilder가 준비되었는지 확인합니다.
    private void Prepare()
    {
        board = GetComponent<MapBoard>();
        routes = GetComponent<RouteConfig>();

        if (builder == null)
        {
            builder = new LaneBuilder(routes);
        }
    }

    // 레인을 계산할 MapBoard와 셀이 준비되었는지 확인합니다.
    private bool CanBuild()
    {
        return board.CellCount > 0;
    }

    // MapBoard의 셀에서 스폰과 코어를 수집합니다.
    private void CollectEndpoints(out List<Tile> spawns, out List<Tile> cores)
    {
        spawns = new List<Tile>();
        cores = new List<Tile>();

        IReadOnlyList<Tile> cellList = board.CellList;
        for (int i = 0; i < cellList.Count; i++)
        {
            AddEndpoint(cellList[i], spawns, cores);
        }
    }

    // 타일의 역할에 따라 스폰 또는 코어 목록에 추가합니다.
    private static void AddEndpoint(Tile tile, List<Tile> spawns, List<Tile> cores)
    {
        if (tile.IsEnemySpawn)
        {
            spawns.Add(tile);
        }

        if (tile.IsCore)
        {
            cores.Add(tile);
        }
    }

    // 계산된 레인을 내부 목록에 보관하고 준비 완료 이벤트를 알립니다.
    private void ApplyLanes(IReadOnlyList<LaneData> built)
    {
        for (int i = 0; i < built.Count; i++)
        {
            lanes.Add(built[i]);
            AddToSpawn(built[i]);
        }

        IsReady = true;
        Changed?.Invoke();
    }

    // 이 레인을 제 스폰 묶음에 넣는다. 처음 보는 스폰이면 묶음을 새로 연다.
    private void AddToSpawn(LaneData lane)
    {
        if (spawnSlot.TryGetValue(lane.Start, out int found))
        {
            spawnLanes[found].Add(lane);
            return;
        }

        spawnSlot[lane.Start] = spawnLanes.Count;
        spawnLanes.Add(new List<LaneData> { lane });
    }
}
