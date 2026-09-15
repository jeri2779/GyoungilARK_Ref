using UnityEngine;

// 슬롯의 프리팹으로 유닛을 만들어 판에 놓는 담당.
// 생산 시설·집은 기반시설 UI(BaseConstructor)로 옮겨가 더 이상 맵에 배치되지 않는다 - 여기 남은 건 Hero뿐이다.
public class UnitPlacer
{
    // MapGame이 주입 완료 후 넣어준다.
    public PlacedUnitData unitList;

    // 슬롯을 자리에 놓는다. 못 놓으면 false.
    public bool TryPlace(PlaceData data, Placeable slot, out GameObject placedUnit)
    {
        placedUnit = null;

        GameObject unit = Create(slot, data.Area.Board);
        if (unit == null)
        {
            return false;
        }

        AreaPlace.Place(data, unit, slot.kind);
        if (unit.TryGetComponent(out Hero hero)) hero.SetCurrentTile();
        unitList.Add(unit, data.Area);
        placedUnit = unit;
        return true;
    }

    // 슬롯의 프리팹으로 영웅 오브젝트를 만든다.
    private GameObject Create(Placeable slot, MapBoard board)
    {
        CheckPrefab(slot);

        // 영웅은 로스터 "생성" 단계(HeroSetPanel)에서 이미 비용을 치렀으므로 여기선 다시 검사하지 않는다.
        if (!slot.prefab.TryGetComponent(out Hero _))
        {
            return null;
        }

        // 위치는 의미 없음 — 곧바로 AreaPlace.Place가 unit.transform.position을 다시 세팅한다.
        GameObject unit = PoolManager.Instance.Spawn(slot.prefab, Vector3.zero, Quaternion.identity);
        // PrepareForSpawn()이 SpawnAuraZones()를 통해 Board를 바로 참조하므로(오라 보유 영웅), 그 전에
        // 반드시 SetBoard부터 해줘야 한다 — 순서가 바뀌면 Board가 null인 채로 참조돼 예외가 터진다.
        BindBoard(unit, board);
        if (unit.TryGetComponent(out Hero hero)) hero.PrepareForSpawn();
        return unit;
    }

    // 유닛에게 자기가 놓인 모듈 보드를 알려준다.
    private static void BindBoard(GameObject unit, MapBoard board)
    {
        if (unit.TryGetComponent(out Hero hero))
        {
            hero.SetBoard(board);
        }
    }

    private static void CheckPrefab(Placeable slot)
    {
        if (slot.prefab == null)
        {
            throw new MissingReferenceException($"{slot.label} 프리팹이 없습니다.");
        }
    }
}
