using System;
using UnityEngine;

// 경로가 거쳐 가는 칸 하나. 어디를 지나는지와 거기서 몇 초 멈추는지를 함께 들고 있습니다.
[Serializable]
public struct RouteNode
{
    [SerializeField] private int col;
    [SerializeField] private int row;
    [SerializeField] private float waitTime;

    public RouteNode(int column, int rowIndex, float seconds)
    {
        col = column;
        row = rowIndex;
        waitTime = seconds;
    }

    public int Col => col;
    public int Row => row;
    public float WaitTime => waitTime;

    // 격자 조회와 거리 계산이 Vector2Int 키를 쓰므로 이 경계에서만 만들어 넘긴다.
    public Vector2Int Coord => new Vector2Int(col, row);
}
