using UnityEngine;

/// <summary>
/// [GridCalculator] 그리드 좌표 관련 "계산 전담" 정적 클래스.
///   월드 위치 → 칸 번호, 칸↔배열 인덱스, 두 칸 사이 거리 같은
///                  순수한 산수(나눗셈·뺄셈·곱셈)만 담당한다.
 
/// </summary>
public static class GridCalculator
{

    public static readonly Vector2Int Right = new(1, 0);   // col + 1
    public static readonly Vector2Int Left = new(-1,  0);   // col - 1
    public static readonly Vector2Int Up = new(0, 1);   // row + 1
    public static readonly Vector2Int Down = new( 0, -1);   // row - 1


    public static readonly Vector2Int[] Directions = { Right, Left, Up, Down };

    // 월드↔칸 변환은 여기 없다 — 씬 Grid(MapBoard.WorldToCell)가 담당한다.
    // 셀 크기·원점·Swizzle을 Grid가 쥐고 있어, 코드가 따로 알면 어긋날 뿐이다.

    //칸 좌표를 1차원 배열 인덱스로 변환
    public static int GetIndexFromCell(int col, int row, int width)
    {
        return row * width + col;
    }
    //좌표가 격자 범위 안에 있는지 여부 정적 bool 함수
    public static bool IsInGrid(Vector2Int cell, int width, int height)
    {
        return cell.x >= 0 
        && cell.x < width 
        && cell.y >= 0 
        && cell.y < height;
    }

    public static int GetDistance(Vector2Int fromCell, Vector2Int toCell)
    {
        int gapX = Mathf.Abs(fromCell.x - toCell.x);
        int gapY = Mathf.Abs(fromCell.y - toCell.y);
        return gapX + gapY;
    }

    public static Vector2Int CardinalToward(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            return dx >= 0 ? Right : Left;
        return dy >= 0 ? Up : Down;
    }

}
