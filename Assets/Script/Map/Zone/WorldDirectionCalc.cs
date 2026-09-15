using UnityEngine;

// 격자 방향을 그 보드의 실제 월드 방향으로 바꾸는 계산 전담.
public static class WorldDirectionCalc
{
    // 격자 한 칸 이동을 월드 이동으로 바꿔 정규화한 방향을 돌려준다.
    public static Vector3 ReadDirection(MapBoard board, Vector2Int cellDirection)
    {
        Vector3 origin = board.CellPointToWorld(Vector2.zero);
        Vector3 target = board.CellPointToWorld(new Vector2(cellDirection.x, cellDirection.y));
        return (target - origin).normalized;
    }

    // 그 방향을 바라보는 바닥 기준 회전을 돌려준다.
    public static Quaternion ReadRotation(MapBoard board, Vector2Int cellDirection)
    {
        return Quaternion.LookRotation(ReadDirection(board, cellDirection), Vector3.up);
    }
}
