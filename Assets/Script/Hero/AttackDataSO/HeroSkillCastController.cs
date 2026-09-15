// 밤 전투 중 "시전자 선택 → 타겟 칸 클릭"으로 영웅의 장착 액티브 스킬 1개를 사용한다.
// PlaceAction.SelectTile을 통해 매 클릭마다 호출되며, MapAssemble이 조립한다.
public class HeroSkillCastController
{
    public DayNightBuildRule dayNightRule;
    private Hero selectedCaster;
    public Hero SelectedCaster => selectedCaster;

    public void HandleClick(Tile tile)
    {
        if (tile == null || dayNightRule.CanBuild()) return; // 낮에는 동작 안 함(CanBuild==true가 낮)

        if (selectedCaster == null)
        {
            Hero clickedHero = null;
            if (tile.OccupantObject != null)
                tile.OccupantObject.TryGetComponent(out clickedHero);
            if (clickedHero != null && clickedHero.ActiveSkill != null && !clickedHero.IsDead && clickedHero.IsSkillReady)
                selectedCaster = clickedHero;
            return;
        }

        selectedCaster.TryUseActiveSkill(tile); // 타일 위에 다른 영웅이 있어도 타겟 클릭으로 취급
        selectedCaster = null;
    }

    public void ClearSelection() => selectedCaster = null;
}
