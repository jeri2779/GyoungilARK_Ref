using System.Collections.Generic;
using UnityEngine;

// 이번 라운드에 트레일로 보여줄 경로 데이터만 계산한다. 생성·애니메이션은 PathTrail이 맡는다.
public class PathTrailCalc
{
    // 폴백 경로라 갈래 정보가 없다는 표시. WaveSpawner.NoSpawn과 같은 뜻(값의 원본은 그쪽).
    private const int NoSpawn = -1;

    private readonly WaveSpawner spawner;
    private readonly EnemyLanes enemyLanes;
    private readonly List<EnemyRouteSet.Entry> routeEntries = new();

    public PathTrailCalc(WaveSpawner spawner, EnemyLanes enemyLanes)
    {
        this.spawner = spawner;
        this.enemyLanes = enemyLanes;
    }

    // 활성 포탈 전부의 트레일 점 목록(지상+공중+수영)을 계산한다(판단만, 실행 없음).
    public List<TrailPoints> CollectRuns()
    {
        IReadOnlyList<IReadOnlyList<Vector3>> paths = spawner.ActivePaths;
        IReadOnlyList<int> spawns = spawner.ActiveSpawns;
        var runs = new List<TrailPoints>();

        // 라운드당 한 번만 확인 — 웨이브 구성은 지역·라운드 단위라 종류마다 표를 다시 훑을 필요가 없다.
        CollectKindsThisRound(out bool hasGround, out bool hasAir, out bool hasSwim);

        // 활성 포탈 수는 라운드마다 달라져 미리 정할 수 없다.
        if (hasGround)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                runs.AddRange(GroundRuns(paths[i], SpawnIndexAt(spawns, i)));
            }
        }

        if (hasAir) runs.AddRange(AirRuns(paths, spawns));
        if (hasSwim) runs.AddRange(SwimRuns(paths, spawns));

        return runs;
    }

    // 지상 레인 경로. 갈래가 있으면 갈래 전부, 없으면 대표 경로 하나.
    private List<TrailPoints> GroundRuns(IReadOnlyList<Vector3> representative, int spawnIndex)
    {
        var runs = new List<TrailPoints>();
        AddGroundBranches(runs, spawnIndex);
        if (runs.Count == 0) runs.Add(new TrailPoints(representative, TrailKind.Ground));
        return runs;
    }

    // 이 스폰의 지상 갈래 점 목록 전부를 runs에 담는다. 갈래 정보가 없으면 아무것도 담지 않는다.
    private void AddGroundBranches(List<TrailPoints> runs, int spawnIndex)
    {
        if (spawnIndex == NoSpawn || enemyLanes == null) return;

        // 갈래 수는 스폰·날짜마다 달라 미리 정할 수 없다 — 그 수만큼 순회한다.
        int count = enemyLanes.BranchCount(spawnIndex);
        for (int i = 0; i < count; i++)
        {
            runs.Add(new TrailPoints(enemyLanes.GetBranchPath(spawnIndex, i, 0f), TrailKind.Ground));
        }
    }

    // 활성 포탈을 한 번만 훑는다: 저작 경로가 있으면 그 자리에 담고, 없는 자리는 대략선 후보로 따로 담아둔다.
    private List<TrailPoints> AirRuns(IReadOnlyList<IReadOnlyList<Vector3>> paths, IReadOnlyList<int> spawns)
    {
        var authoredRuns = new List<TrailPoints>();
        var fallbackRuns = new List<TrailPoints>();

        for (int i = 0; i < paths.Count; i++)
        {
            int before = authoredRuns.Count;
            AddRouteTrails(authoredRuns, SpawnIndexAt(spawns, i), EnemyRouteKind.Air, TrailKind.Air);
            if (authoredRuns.Count == before) AddAirFallback(fallbackRuns, paths[i]);
        }

        return authoredRuns.Count > 0 ? authoredRuns : fallbackRuns;
    }

    // 활성 포탈 중 저작된 물 경로가 있는 곳만 그린다. 없으면 지상 경로와 완전히 같은 길을 걷기 때문에 추가로 그리지 않는다.
    private List<TrailPoints> SwimRuns(IReadOnlyList<IReadOnlyList<Vector3>> paths, IReadOnlyList<int> spawns)
    {
        var runs = new List<TrailPoints>();
        for (int i = 0; i < paths.Count; i++)
        {
            AddRouteTrails(runs, SpawnIndexAt(spawns, i), EnemyRouteKind.Swim, TrailKind.Swim);
        }
        return runs;
    }

    // 이 스폰에 저작된 이 종류 경로 전부를 트레일로 바꿔 runs에 담는다. 좌표는 실제 스폰과 같은 표(WaveSpawner.SpawnCoord)에서 가져온다.
    private void AddRouteTrails(List<TrailPoints> runs, int spawnIndex, EnemyRouteKind kind, TrailKind trailKind)
    {
        if (spawnIndex == NoSpawn) return;

        EnemyRouteSet routes = spawner.EnemyRoutes;
        if (routes == null) return;

        Vector2Int coord = spawner.SpawnCoord(spawnIndex);
        routes.Collect(kind, coord, routeEntries);
        for (int i = 0; i < routeEntries.Count; i++)
        {
            runs.Add(new TrailPoints(EnemyRoutePath.Build(spawner.Board, routeEntries[i]), trailKind));
        }
    }

    // 공중 저작 경로가 없을 때, 실제 게임과 같은 방식(FlyingPathfinder)으로 직선 경로를 계산해 담는다.
    private void AddAirFallback(List<TrailPoints> runs, IReadOnlyList<Vector3> representative)
    {
        if (representative == null || representative.Count < 2) return;

        List<Vector3> fallback = FlyingPathfinder.BuildWaypoints(
            spawner.Board, representative[0], representative[representative.Count - 1], EnemyRoutePath.FlightHeight);
        if (fallback.Count > 0) runs.Add(new TrailPoints(fallback, TrailKind.Air));
    }

    // 이번 라운드 웨이브 구성(자기 웨이브 + 다른 해금 지역의 증원 웨이브)을 조회해 지상/공중/수영이 각각 있는지 구한다.
    private void CollectKindsThisRound(out bool hasGround, out bool hasAir, out bool hasSwim)
    {
        hasGround = false;
        hasAir = false;
        hasSwim = false;

        int region = spawner.Region;
         if (!SpawnerManager.Instance.IsUnlocked(region)) return;
        int stage = SpawnerManager.Instance.LocalStage(region);
        int lookupId = WaveSpawner.GetStageLookupId(stage);
        CollectKindsFromWave(DataTableManager.WaveTable.GetWave(region, lookupId), ref hasGround, ref hasAir, ref hasSwim);

        // 다른 해금 지역이 보내는 증원 적도 이번 라운드 실제 스폰에 포함되므로 같은 기준으로 확인한다.
        List<int> unlockedRegions = SpawnerManager.Instance.UnlockedRegions();
        for (int i = 0; i < unlockedRegions.Count; i++)
        {
            if (unlockedRegions[i] == region) continue;
            CollectKindsFromWave(DataTableManager.WaveTable.GetWave(unlockedRegions[i], WaveSpawner.ReinforceBaseId), ref hasGround, ref hasAir, ref hasSwim);
        }
    }

    // 웨이브 행 목록 하나를 훑어 지상/공중/수영 존재 여부에 반영한다(자기 웨이브·증원 웨이브 공용).
    private static void CollectKindsFromWave(List<WaveTable.Data> wave, ref bool hasGround, ref bool hasAir, ref bool hasSwim)
    {
        for (int i = 0; i < wave.Count; i++)
        {
            EnemyTable.Data data = DataTableManager.EnemyTable.Get(wave[i].MonsterName);
            if (data == null) continue;

            EnemyAttribute attribute = EnemyBase.ParseAttribute(data.Attribute);
            if ((attribute & EnemyAttribute.Fly) != 0) hasAir = true;
            else if ((attribute & EnemyAttribute.Swim) != 0) hasSwim = true;
            else hasGround = true;
        }
    }

    // i번째 활성 포탈의 스폰 번호.
    private static int SpawnIndexAt(IReadOnlyList<int> spawns, int index) => spawns[index];
}
