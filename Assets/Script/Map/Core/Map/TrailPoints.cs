using System.Collections.Generic;
using UnityEngine;

// 트레일이 표현하는 통행 종류.
public enum TrailKind
{
    Ground,
    Air,
    Swim
}

// 트레일 하나가 그릴 점 목록과 그 경로의 통행 종류.
public readonly struct TrailPoints
{
    public readonly IReadOnlyList<Vector3> Points;
    public readonly TrailKind Kind;

    public TrailPoints(IReadOnlyList<Vector3> points, TrailKind kind)
    {
        Points = points;
        Kind = kind;
    }
}
