using System;
using UnityEngine;

// 지역별 활성 포탈 — 스폰 칸 좌표 목록  NightReady 전용
[Serializable]
public class PortalSave
{
    public int regionId;             // 어느 지역의 포탈인지
    public Vector2Int[] spawnCells = Array.Empty<Vector2Int>();  // 이번 라운드에 켜진 포탈들의 칸 좌표
}
