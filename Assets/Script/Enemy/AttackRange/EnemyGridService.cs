
using UnityEngine;

// 좌표 변환 담당 (타일맵 의존 지점 = seam).
// ★ 실제 타일맵 오면 이 클래스 내부만 교체하면 됨. 나머지 게임 코드는 안 건드림.
public static class EnemyGridService
{
    public static MapBoard mapBoard; //타일맵와서 타일맵기반으로 변경
    // 보드가 연결돼 있으면 실제 격자로 변환(이동과 동일 좌표계). 없으면 폴백 — 단, 탑다운이므로 x/z 사용(y는 높이축).
    public static Vector2Int WorldToCell(Vector3 world)
        => mapBoard != null
            ? mapBoard.WorldToCell(world)
            : new(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));

    public static Vector3 CellToWorld(Vector2Int cell)
        => mapBoard.TryGetCell(cell,out var t)? t.WorldTop : new(cell.x, 0f ,cell.y);
}
