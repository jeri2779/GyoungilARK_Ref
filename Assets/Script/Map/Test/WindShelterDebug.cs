using System.Collections.Generic;
using UnityEngine;

// 사막 지대의 지금 바람 차폐 상태(고지+유닛)를 타일 색으로 확인하는 임시 디버그 도구.
public class WindShelterDebug : MonoBehaviour
{
    [SerializeField] private MapBoard desertBoard;
    [SerializeField] private DesertZone desertZone;
    [SerializeField] private TilePainter painter;

    // 지금 배치 기준으로 고지·유닛·가림막 차폐를 다시 계산해 타일마다 보호/노출 색을 칠한다.
    [ContextMenu("Tint Wind Shelter")]
    public void TintShelter()
    {
        WindShelterData shelterData = new WindShelterCalc().BuildData(desertBoard.Cells);
        WindwallData windwallData = new WindwallCalc().BuildData(desertBoard.Cells, desertZone.WindwallReach);
        UnitShelter unitShelter = new(CollectHeroTiles());
        Vector2Int wind = desertZone.WindDirection;

        foreach (Tile tile in desertBoard.Cells.Values)
        {
            bool unsheltered = DesertShelterQuery.IsUnsheltered(shelterData, unitShelter, windwallData, tile, wind);
            painter.SetColor(tile, unsheltered ? painter.denyColor : painter.okColor);
        }
    }

    // 칠한 색을 지운다.
    [ContextMenu("Clear Wind Shelter")]
    public void ClearShelter()
    {
        foreach (Tile tile in desertBoard.Cells.Values)
        {
            painter.ClearColor(tile);
        }
    }

    // 지금 사막 보드에 서 있는 영웅 타일만 모은다.
    private List<Tile> CollectHeroTiles()
    {
        List<Tile> tiles = new();
        foreach (Tile tile in desertBoard.Cells.Values)
        {
            if (tile.OccupantObject != null && tile.OccupantObject.TryGetComponent(out Hero _))
            {
                tiles.Add(tile);
            }
        }

        return tiles;
    }
}
