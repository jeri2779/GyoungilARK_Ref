using System;
using System.Collections.Generic;
using UnityEngine;

// 제작자가 지정한 경로 한 벌을 보관합니다.
[Serializable]
public sealed class RouteData
{
    [SerializeField] private Vector2Int spawn;
    // 이 경로가 적용되는 일차. 0 = 공통(모든 날 동일). 특정 날만 다르면 그 날 번호로 겹쳐 쓴다.
    [SerializeField] private int day;
    [SerializeField] private List<RouteNode> nodes = new();

    public Vector2Int Spawn => spawn;
    public int Day => day;
    public IReadOnlyList<RouteNode> Nodes => nodes;
}
