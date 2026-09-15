using System.Collections.Generic;
using UnityEngine;

// 수영(헤엄 칸 통행) 격자 길찾기.
// 통행 판정을 CanPass(PassType.Swim)로 바꿔 걷는 적이 못 지나는 물 칸(State.Pass == Swim)까지 길로 인정한다.
// 고지·빈 칸은 여전히 막힌다.
//
// 칸 수 최단이 아니라 "가장 빨리 도착하는 길"을 찾는다(WeightedPathfinder) —
// 물에서는 EnemyBase.swimSpeedMultiplier만큼 빠르므로 물 칸의 이동 비용이 그만큼 싸다.
// 그래서 조금 돌더라도 물을 타고 가는 편이 빠르면 그쪽으로 경로가 잡힌다.
//
// MapBoard는 공개 API(TryGetCell/WorldToCell)만 읽고 절대 수정하지 않는다(맵은 다른 담당 영역).
public static class SwimPathfinder
{
    // 지상 한 칸의 기준 비용. 물 칸 비용을 정수로 나눌 때 소수점이 뭉개지지 않게 크게 잡는다
    // (배율 1.3 같은 값도 1000/1.3 = 769로 충분히 정확하다).
    private const int TileCost = 1000;

    /// <summary>켜면 경로를 찾을 때마다 비용 내역을 로그로 남긴다(EnemyBase의 logSwimPath 체크박스).
    /// "물길을 왜 안 타는지" 가를 때만 쓰고 평소엔 끈다 — 켜져 있으면 비교용 탐색을 한 번 더 돌린다.</summary>
    public static bool LogPathCost;

    /// <summary>startWorld→본진을 잇는 격자 경로(웨이포인트). 경로 없으면 빈 리스트.
    /// 물 위도 걸어서 건너는 연출이라 Y는 타일 윗면(WorldTop) 그대로 쓴다.</summary>
    /// <param name="swimSpeedMultiplier">물속 이동속도 배율(EnemyBase와 같은 값). 1이면 칸 수 최단과 같아진다.</param>
    /// <param name="transitionPenaltyTiles">물에 들어가고 나올 때 1회당 손해로 치는 거리(타일 수).
    /// 잠수(Pool)/상승(Up) 모션 동안 제자리에 멈추기 때문에, 이걸 안 넣으면 한 칸짜리 물웅덩이를
    /// 굳이 들렀다 나오면서 오히려 늦게 도착한다.</param>
    public static List<Vector3> BuildWaypoints(
        MapBoard board, Vector3 startWorld, Vector3 goalWorld,
        float swimSpeedMultiplier = 1f, float transitionPenaltyTiles = 0f)
    {
        var list = new List<Vector3>();
        if (board == null) return list;

        if (!board.TryGetCell(board.WorldToCell(startWorld), out Tile startTile)) return list;

        // 물 칸은 배율만큼 빨리 지나가므로 비용이 그만큼 싸다. Min(1f)로 배율은 1 이상이 보장되지만
        // 인스펙터 밖에서 들어올 수도 있어 여기서도 한 번 막는다(0 이하면 0으로 나눠 터진다).
        int waterCost = Mathf.RoundToInt(TileCost / Mathf.Max(1f, swimSpeedMultiplier));
        int transitionCost = Mathf.RoundToInt(Mathf.Max(0f, transitionPenaltyTiles) * TileCost);

        // 휴리스틱은 "남은 칸 수 × 가장 싼 한 칸"이어야 한다. 실제 남은 비용을 넘기면(과대평가)
        // A*가 최단이 아닌 길을 조용히 내놓는다. 가장 싼 칸은 물이므로 waterCost가 하한.
        int minStep = Mathf.Min(TileCost, waterCost);

        // 도착 판정은 좌표 비교가 아니라 IsCore로 한다 — 타일 좌표는 모듈 로컬 0-base라
        // 보드가 여러 개면 다른 보드의 같은 좌표에 잘못 걸린다(Tile.Board 주석 참조).
        // goalCell은 휴리스틱(탐색 방향 힌트)에만 쓰므로 근사값이어도 경로 정확도에는 영향이 없다.
        Vector2Int goalCell = board.WorldToCell(goalWorld);
        List<Tile> path = Search(startTile, goalCell, waterCost, transitionCost, minStep);

        if (LogPathCost) Report(startTile, goalCell, path, swimSpeedMultiplier, transitionPenaltyTiles, waterCost, transitionCost);

        if (path == null) return list;
        foreach (Tile t in path)
        {
            list.Add(t.WorldTop);
        }
        return list;
    }

    private static List<Tile> Search(Tile startTile, Vector2Int goalCell, int waterCost, int transitionCost, int minStep)
    {
        return WeightedPathfinder.FindPath(
            new[] { startTile },
            tile => tile.IsCore,
            PassSwim,
            (from, to) => StepCost(from, to, waterCost, transitionCost),
            tile => GridCalculator.GetDistance(tile.Coord, goalCell) * minStep);
    }

    // 진단용. 지금 설정으로 찾은 경로와, "물이 거의 공짜"일 때 찾은 경로를 나란히 찍는다.
    // 둘을 비교하면 원인이 갈린다:
    //   비교 경로도 물 0칸  → 물길이 애초에 후보에 없다(물 칸이 Walkable이 아니거나 이웃으로 안 이어졌거나 스폰~본진 사이에 물이 없다)
    //   비교 경로만 물 있음 → 길찾기는 정상이고 지금 설정에선 땅이 더 싸다(배율/페널티 튜닝 문제)
    private static void Report(
        Tile startTile, Vector2Int goalCell, List<Tile> path,
        float multiplier, float penaltyTiles, int waterCost, int transitionCost)
    {
        // 물을 사실상 공짜로 두고 "물을 최대한 타는 길"을 뽑아낸다. 이 극단 설정은 길을 찾을 때만 쓰고,
        // 비용은 아래에서 실제 설정으로 다시 잰다 — 두 줄의 숫자가 같은 잣대여야 비교가 된다.
        const int NearFreeWaterCost = 1;
        List<Tile> waterFirst = Search(startTile, goalCell, NearFreeWaterCost, 0, NearFreeWaterCost);

        // Debug.Log(
        //     $"[SwimPath] 배율={multiplier} 페널티={penaltyTiles}칸 " +
        //     $"(한 칸당 땅 {TileCost} / 물 {waterCost}, 물 드나들 때마다 {transitionCost})\n" +
        //     $"  고른 길    → {Describe(path, waterCost, transitionCost)}\n" +
        //     $"  물 최대한  → {Describe(waterFirst, waterCost, transitionCost)}\n" +
        //     $"  ※ 비용은 둘 다 위 설정으로 잰 값이고, 낮을수록 빨리 도착한다 — 그래서 낮은 쪽을 고른다.");
    }

    // 경로를 "N칸(물 M칸) 비용 C" 한 줄로. 경로가 없으면 그렇게 적는다.
    private static string Describe(List<Tile> path, int waterCost, int transitionCost)
    {
        if (path == null) return "경로 없음";

        int water = 0;
        int cost = 0;
        for (int i = 0; i < path.Count; i++)
        {
            if (IsWater(path[i])) water++;
            if (i > 0) cost += StepCost(path[i - 1], path[i], waterCost, transitionCost);
        }
        return $"{path.Count}칸(물 {water}칸) 비용 {cost}";
    }

    // from에서 to로 한 칸 넘어가는 비용 = to를 지나는 비용 + (물↔지상 경계를 넘었다면 전이 손해).
    private static int StepCost(Tile from, Tile to, int waterCost, int transitionCost)
    {
        bool toWater = IsWater(to);
        int cost = toWater ? waterCost : TileCost;

        if (IsWater(from) != toWater) cost += transitionCost; // 잠수/상승 모션 동안 멈춰 있는 시간

        return cost;
    }

    // 헤엄으로만 지나는 칸인가. EnemySwim.IsSwimCell과 같은 기준을 쓴다 —
    // 실제로 속도 배율이 붙는 구간과 길찾기가 싸다고 본 구간이 어긋나면 안 된다.
    private static bool IsWater(Tile tile)
    {
        return tile.State.Pass == PassType.Swim;
    }

    // 헤엄치는 적 기준 통행 판정. Tile.CanPass가 물 칸을 열어 주고, 고지·빈 칸은 Walkable에서 걸러진다.
    private static bool PassSwim(Tile tile)
    {
        return tile.CanPass(PassType.Swim);
    }
}
