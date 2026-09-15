using System.Collections.Generic;
using UnityEngine;

// 경로 점 목록을 받아 트레일 오브젝트를 만들고 없애는 책임만 맡는다.
public class TrailRunBuilder
{
    private readonly TrailRenderer trailPrefab;
    private readonly TrailPalette palette;
    private readonly Transform parent;
    private readonly float trailLift;

    // 트레일 원본·색상표·부모 트랜스폼·띄우는 높이를 받아 둔다.
    public TrailRunBuilder(TrailRenderer trailPrefab, TrailPalette palette, Transform parent, float trailLift)
    {
        this.trailPrefab = trailPrefab;
        this.palette = palette;
        this.parent = parent;
        this.trailLift = trailLift;
    }

    // 점 목록마다 TrailRun을 하나씩 만들어 runs에 담는다.
    public void Build(IReadOnlyList<TrailPoints> points, List<TrailRun> runs)
    {
        for (int i = 0; i < points.Count; i++)
        {
            AddRun(points[i], runs);
        }
    }

    // 활성 경로 하나의 Trail과 누적 길이를 생성해 runs에 담는다.
    private void AddRun(TrailPoints data, List<TrailRun> runs)
    {
        IReadOnlyList<Vector3> path = data.Points;
        if (path == null || path.Count < 2) return;

        TrailRenderer trail = Object.Instantiate(trailPrefab, parent);
        trail.transform.localScale = Vector3.one;
        trail.emitting = false;
        trail.Clear();
        ApplyKindColor(trail, data.Kind);

        var run = new TrailRun();
        run.Trail = trail;
        run.Runner = trail.transform;
        Vector3 lift = Vector3.up * trailLift;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point = path[i] + lift;
            if (run.Points.Count > 0)
            {
                int last = run.Points.Count - 1;
                run.Length += Vector3.Distance(run.Points[last], point);
            }

            run.Points.Add(point);
        }
        runs.Add(run);
    }

    // 경로 종류에 맞는 색을 팔레트 표에서 찾아 입힌다.
    private void ApplyKindColor(TrailRenderer trail, TrailKind kind)
    {
        trail.colorGradient = palette.GradientOf(kind);
    }

    // 생성된 Trail을 제거하고 실행 목록을 비운다.
    public void Clear(List<TrailRun> runs)
    {
        for (int i = 0; i < runs.Count; i++)
        {
            TrailRun run = runs[i];
            if (run.Trail != null)
            {
                Object.Destroy(run.Trail.gameObject);
            }
        }

        runs.Clear();
    }
}
