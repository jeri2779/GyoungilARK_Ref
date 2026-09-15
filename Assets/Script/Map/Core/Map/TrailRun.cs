using System.Collections.Generic;
using UnityEngine;

// 트레일 하나가 그릴 점 목록과 재생 중 진행 상태.
public class TrailRun
{
    public readonly List<Vector3> Points = new();
    public TrailRenderer Trail;
    public Transform Runner;
    public float Length;
    public int Index;
    public Vector3 LastAdded;
}
