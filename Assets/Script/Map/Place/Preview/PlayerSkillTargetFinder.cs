// 이번 프레임에 플레이어 스킬이 노리는 무장 스킬·원점 타일을 찾는다. 칠하지 않는다.
// SkillTargetFinder의 자매 클래스 — 플레이어 스킬은 캐스터/보드 제약이 없으므로 항상 호버 타일이 원점이다.
public class PlayerSkillTargetFinder
{
    private readonly PlayerSkillCastController cast;
    private readonly MapView game;

    public PlayerSkillTargetFinder(PlayerSkillCastController cast, MapView game)
    {
        this.cast = cast;
        this.game = game;
    }

    public bool TryFindTarget(out PlayerSkillSlot skill, out Tile origin)
    {
        skill = cast?.Armed;
        origin = skill != null ? game.HoverTile : null;
        return skill != null && origin != null;
    }
}
