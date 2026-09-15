using UnityEngine;

// 얼음 지대 전용 — 밤마다 이 보드를 훑어 모닥불 보호 여부를 판정합니다.
public class IceZoneEffect : ZoneDebuffEffect
{
    private readonly IceZone iceZone;

    // 이 얼음 보드와 소속 디버프를 보관합니다.
    public IceZoneEffect(MapBoard board, PlacedUnitData unitList, IceZone iceZone)
        : base(board, unitList, iceZone.Debuffs)
    {
        this.iceZone = iceZone;
    }

    // 낮이 시작되면 이 얼음 보드에 걸린 디버프를 전원 제거합니다.
    protected override void RunDay()
    {
        ClearBoard();
    }

    // 밤이 시작되면 이 얼음 보드의 영웅을 훑어 모닥불 보호 여부로 디버프를 확정합니다.
    protected override void RunNight()
    {
        BoardHeroes heroes = ReadHeroes();
        ApplyEach(heroes);
    }

    // 영웅마다 모닥불 보호 상태를 다시 판정합니다.
    private void ApplyEach(BoardHeroes heroes)
    {
        for (int index = 0; index < heroes.Heroes.Count; index++)
        {
            RefreshIce(heroes.Heroes[index], heroes.Areas[index]);
        }
    }

    // 기존 얼음 효과를 정리하고 현재 모닥불 보호 상태에 맞춰 다시 결정합니다.
    private void RefreshIce(Hero hero, PlacementArea area)
    {
        Remove(hero);
        bool protectedCell = CampfireQuery.IsProtected(iceZone.CampfireData, area.Origin);
        ApplyIfExposed(hero, protectedCell);
    }

    // 보호받지 못한 영웅에게만 얼음 효과를 적용합니다.
    private void ApplyIfExposed(Hero hero, bool protectedCell)
    {
        if (protectedCell)
        {
            // Debug.Log($"[Zone] {hero.name} 모닥불 보호 - 얼음 효과 면역");
            return;
        }

        Apply(hero);
        // Debug.Log($"[Zone] {hero.name} 얼음 지대 진입 - 효과 적용");
    }
}
