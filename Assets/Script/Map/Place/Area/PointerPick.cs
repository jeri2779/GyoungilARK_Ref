using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 포인터 아래의 타일을 모든 모듈 보드에서 찾는다(그리드 밖이면 가장 가까운 가장자리 타일).
// 여러 보드가 동시에 맞으면 카메라에 가장 가까운 타일을 고른다 → 그 타일의 Board가 곧 클릭된 모듈.
// 배치물이 여러 칸을 차지할 때는 타일 하나로 부족하므로 놓일 자리(PlacementArea)까지 내준다.
public class PointerPick
{
    private readonly List<MapBoard> boards;
    private readonly HoveredTileData hoverData;
    private int hoverFrame = -1;
    private Vector2 mousePos;

    public PointerPick(List<MapBoard> boards, HoveredTileData hoverData)
    {
        this.boards = boards;
        this.hoverData = hoverData;
    }

    private Ray PointerRay()
    {
        return Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
    }

    // 포인터 아래에 배치물이 몇 칸으로 어디에 놓일지. 그리드 밖이면 가장자리 타일을 기준으로 잡는다.
    public PlacementArea GetArea(Vector2Int size)
    {
        // 기준 칸과 자리 계산이 같은 광선에서 나와야 한다 — 따로 뽑으면 포인터가 움직인 만큼 어긋난다.
        Ray ray = PointerRay();
        Tile tile = Nearest(ray);
        return AreaAnchor.Resolve(tile, ray, size);
    }

    public Tile UnderPointer()   // 없으면 null
    {
        RefreshHoverData();
        return hoverData.HoveredTile;
    }

    public Tile NearestCell()
    {
        return Nearest(PointerRay());
    }

    // 포인터 아래 타일을 갱신한다. 이미 갱신한 프레임이면 아무것도 안 한다.
    private void RefreshHoverData()
    {
        if (IsSameFrame())
        {
            return;
        }
        MarkFrame();

        Vector2 current = MousePos();
        bool moved = IsMoved(current);
        MarkPos(current);

        if (!moved && hoverData.HasTile)
        {
            return;
        }

        Tile tile = HitTile(PointerRay());
        hoverData.Keep(tile);
    }

    private bool IsSameFrame()
    {
        return hoverFrame == Time.frameCount;
    }

    private void MarkFrame()
    {
        hoverFrame = Time.frameCount;
    }

    // 지금 마우스 위치. 아직 안 잡혔으면 (0,0).
    private Vector2 MousePos()
    {
        if (Mouse.current == null)
        {
            return Vector2.zero;
        }
        return Mouse.current.position.ReadValue();
    }

    // 지난 화면에 적어둔 자리랑 다른가만 본다. 아무것도 안 바꾼다.
    private bool IsMoved(Vector2 current)
    {
        return current != mousePos;
    }

    private void MarkPos(Vector2 current)
    {
        mousePos = current;
    }

    private Tile HitTile(Ray ray)
    {
        // 클릭은 언제나 타일이 받는다. 유닛 몸통을 먼저 잡으면, 몸통이 화면에서 덮는
        // 위쪽 칸을 눌렀을 때도 그 유닛의 발밑 칸이 선택된다.
        Tile frontTile = null;
        float frontSqr = float.MaxValue;
        for (int i = 0; i < boards.Count; i++)
        {
            MapBoard board = boards[i];
            if (!IsUsable(board)) continue;

            Tile tile = board.CellFromRay(ray);
            if (tile == null) continue; // 이 보드는 레이가 안 맞음 — 다음 모듈

            float sqr = DistanceSqr(tile, ray);
            if (sqr < frontSqr)
            {
                frontSqr = sqr;
                frontTile = tile;
            }
        }
        return frontTile;
    }

    // 화면에 켜져 있고 잠금이 풀린 모듈인지.
    private bool IsUsable(MapBoard board)
    {
        return board.gameObject.activeInHierarchy && board.IsUnlocked;
    }

    // 카메라(레이 원점)에서 이 타일까지 얼마나 가까운지 — 두 모듈에 겹쳐 맞으면 더 가까운 쪽이 앞에 보이는 타일이다.
    private float DistanceSqr(Tile tile, Ray ray)
    {
        return (tile.WorldTop - ray.origin).sqrMagnitude;
    }

    private Tile Nearest(Ray ray)
    {
        Tile nearestTile = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < boards.Count; i++)
        {
            MapBoard board = boards[i];
            if (!IsUsable(board)) continue;

            Tile tile = board.NearestCellFromRay(ray);
            if (tile == null) continue;

            float distance = LineDistance(tile, ray);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTile = tile;
            }
        }
        return nearestTile;
    }

    // 그리드 밖 클램프 후보끼리는 레이 직선에서 얼마나 벗어났나로 비교한다(포인터에 가장 붙은 타일).
    private float LineDistance(Tile tile, Ray ray)
    {
        return Vector3.Cross(ray.direction, tile.WorldTop - ray.origin).magnitude;
    }
}
