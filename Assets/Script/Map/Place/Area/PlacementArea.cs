using System.Collections.Generic;
using UnityEngine;

// 배치물을 어디에 몇 칸으로 놓을지를 담은 계산 결과. 유닛도 배치 여부도 갖지 않는다.
public class PlacementArea
{
    // 이 자리가 속한 모듈 보드. 칸 좌표가 모듈 로컬이라 보드까지 있어야 주소가 완성된다.
    public MapBoard Board { get; }

    // 차지하는 칸들의 시작 칸(왼쪽 아래).
    public Vector2Int Origin { get; }
    public Vector2Int Size { get; }

    // 덮는 칸 좌표들. 그 자리에 타일이 실제로 있는지는 보지 않는다.
    public IReadOnlyList<Vector2Int> Cells { get; }

    // 유닛 몸통이 설 월드 지점(덮는 칸들의 한가운데).
    public Vector3 Center { get; }

    public PlacementArea(
        MapBoard board,
        Vector2Int origin,
        Vector2Int size,
        IReadOnlyList<Vector2Int> cells,
        Vector3 center)
    {
        Board = board;
        Origin = origin;
        Size = size;
        Cells = cells;
        Center = center;
    }
}
