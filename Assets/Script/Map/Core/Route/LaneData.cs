using System.Collections.Generic;
using UnityEngine;

public class LaneData
{
    private readonly List<Tile> tiles;
    // 같은 원본 경로를 헤엄 기준으로 계산한 결과. 물이 없는 구간은 tiles와 같다.
    private readonly List<Tile> swimTiles;
    // 통행 방식별 경로 타일. 새 통행 방식이 추가돼도 이 자리 한 곳만 늘어난다.
    private readonly Dictionary<PassType, List<Tile>> tilesByPass;

    public Tile Start { get; }
    public Tile Goal { get; }

    // 이 레인의 신원. 계산 단계가 찾아낸 지정 항목을 그대로 넘겨받는다 — 좌표로 다시 조회하지 않는다.
    public RouteData Route { get; }

    public IReadOnlyList<Tile> Tiles => tiles;
    public IReadOnlyList<Tile> SwimTiles => swimTiles;
    public bool IsValid => Goal != null && tiles.Count > 0;

    // 스폰과 코어, 그리고 걷기·헤엄 두 통행 방식의 경로 타일을 레인 결과로 보관합니다.
    public LaneData(Tile start, Tile goal, RouteData route, IReadOnlyList<Tile> source, IReadOnlyList<Tile> swimSource)
    {
        Start = start;
        Goal = goal;
        Route = route;
        tiles = CopyTiles(source);
        swimTiles = CopyTiles(swimSource);
        tilesByPass = new Dictionary<PassType, List<Tile>>
        {
            { PassType.Walk, tiles },
            { PassType.Swim, swimTiles }
        };
    }

    // 걷기 경로 타일을 지정 높이가 적용된 월드 좌표 목록으로 변환합니다.
    public List<Vector3> GetPoints(float yOffset)
    {
        return Points(tiles, yOffset);
    }

    // 헤엄 경로 타일을 지정 높이가 적용된 월드 좌표 목록으로 변환합니다.
    public List<Vector3> SwimPoints(float yOffset)
    {
        return Points(swimTiles, yOffset);
    }

    // 지정한 통행 방식의 경로 타일을 지정 높이가 적용된 월드 좌표 목록으로 변환합니다.
    public List<Vector3> GetPoints(PassType pass, float yOffset)
    {
        return Points(tilesByPass[pass], yOffset);
    }

    private static List<Tile> CopyTiles(IReadOnlyList<Tile> source)
    {
        var copy = new List<Tile>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            copy.Add(source[i]);
        }

        return copy;
    }

    private static List<Vector3> Points(List<Tile> path, float yOffset)
    {
        var points = new List<Vector3>(path.Count);
        Vector3 lift = Vector3.up * yOffset;

        for (int i = 0; i < path.Count; i++)
        {
            points.Add(path[i].WorldTop + lift);
        }

        return points;
    }
}
