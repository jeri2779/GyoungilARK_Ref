using UnityEngine;

// 배치된 유닛을 판에서 치우는 담당.
public class UnitRemover
{
    private readonly PlacedUnitData _unitList;
    private readonly HeroRoster _heroRoster;

    public UnitRemover(PlacedUnitData unitList, HeroRoster heroRoster)
    {
        _unitList = unitList;
        _heroRoster = heroRoster;
    }

    // 한 칸의 유닛을 치운다. 칸이 없으면 false.
    public bool TryRemoveUnit(Tile tile)
    {
        if(tile == null) return false;
        UnitRemove(tile);
        return true;
    }

    public GameObject UnitRemove(Tile tile)
    {
        GameObject unit = tile.OccupantObject;

        if(unit == null) return null;

        if (!_unitList.TryGetArea(unit, out PlacementArea area))
        {
            return null;
        }

        AreaPlace.Remove(area);
        _unitList.Remove(unit);

        DestroyOrReturnToPool(unit);
        return unit;
    }

    // 영웅(로스터 출신)이면 로스터로 되돌리고 풀에 반납(PrepareForDespawn 후 PoolManager.Despawn).
    // 생산 시설·집은 기반시설 UI(BaseConstructor.Demolish)로 옮겨가 더 이상 맵 유닛으로 존재하지 않는다.
    private void DestroyOrReturnToPool(GameObject unit)
    {
        HeroRosterLink link = unit.GetComponent<HeroRosterLink>();
        if (link != null && link.Entry != null)
        {
            link.Entry.MarkAvailable();
            _heroRoster.NotifyStateChanged();
        }

        if (unit.TryGetComponent(out Hero hero))
        {
            hero.PrepareForDespawn();
            PoolManager.Instance.Despawn(unit);
        }
        else
        {
            Object.Destroy(unit);
        }
    }
}
