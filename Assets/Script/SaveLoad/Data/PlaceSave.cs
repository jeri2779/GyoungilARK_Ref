using System;
using UnityEngine;

// 영웅 맵 배치 — 로스터Guid·모듈번호·칸 원점·크기
[Serializable]
public class PlaceSave
{
    public string rosterId;         // 배치된 영웅의 고유 번호
    public int moduleId;            // 어느 지역(모듈) 위에 서 있는지
    public Vector2Int cellOrigin;   // 서 있는 칸들의 왼쪽 아래 시작 칸
    public Vector2Int cellSize;     // 몇 칸을 차지하는지
}
