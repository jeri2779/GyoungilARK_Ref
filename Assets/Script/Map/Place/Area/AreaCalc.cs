using System.Collections.Generic;
using UnityEngine;

// 여러 칸을 차지하는 배치물이 덮는 칸 좌표를 만드는 계산 전용 정적 클래스.
public static class AreaCalc
{
    // 시작 칸에서 크기만큼 펼쳐 덮는 칸 좌표를 낸다.
    // origin은 시작 칸(왼쪽 아래)이다. 중앙 스냅은 부르는 쪽에서 끝내고 넘긴다.
    public static List<Vector2Int> GetCells(
        Vector2Int origin,
        Vector2Int size)
    {
        // 0 이하의 크기는 의미가 없으므로 예외를 던진다.
        if (size.x <= 0 || size.y <= 0)
        {
            throw new System.ArgumentException($"칸 크기는 1 이상이어야 한다: {size}");
        }

        List<Vector2Int> cells = new(size.x * size.y);

        for (int row = 0; row < size.y; row++)// 행
        {
            for (int col = 0; col < size.x; col++)// 열
            {
                Vector2Int cell = new Vector2Int(
                    origin.x + col,// col은 x축
                    origin.y + row);// row은 y축

                cells.Add(cell);// 행 우선 순서로 추가
            }
        }

        return cells; // 행과 열만큼의 칸을 반환.
    }
}
