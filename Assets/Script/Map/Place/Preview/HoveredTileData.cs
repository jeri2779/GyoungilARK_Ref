// 역할: 이번 프레임에 마우스 포인터가 가리키는 타일을 딱 한번만 계산해서 보관하는 장부
// 쓰는 곳: PointerPick이 마우스 위치를 적어두고, MapView와 미리보기 일꾼들이 읽어감
// 안 하는 일: 마우스가 맵의 어느 타일을 가리키는지 직접 레이를 쏘거나 계산하지 않음
public class HoveredTileData
{
    private Tile hoveredTile;

    public Tile HoveredTile => hoveredTile;
    public bool HasTile => hoveredTile != null;

    public void Keep(Tile tile)
    {
        hoveredTile = tile;
    }

    public void Clear()
    {
        hoveredTile = null;
    }
}
