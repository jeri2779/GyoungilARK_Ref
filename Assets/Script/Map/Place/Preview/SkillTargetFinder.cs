// 이번 프레임에 스킬이 노리는 시전자·스킬·원점 타일을 찾는다. 칠하지 않는다.
public class SkillTargetFinder
{
    private readonly HeroSkillCastController skillCast;
    private readonly MapView game;

    public SkillTargetFinder(HeroSkillCastController skillCast, MapView game)
    {
        this.skillCast = skillCast;
        this.game = game;
    }

    public bool TryFindTarget(out Hero caster, out HeroActiveSkill skill, out Tile origin)
    {
        caster = null;
        skill = null;
        origin = null;

        if (HasSkillCastController())
        {
            return TryFindReadyTarget(out caster, out skill, out origin);
        }

        return false;
    }

    private bool HasSkillCastController()
    {
        return skillCast != null;
    }

    private bool TryFindReadyTarget(out Hero caster, out HeroActiveSkill skill, out Tile origin)
    {
        caster = skillCast.SelectedCaster;
        skill = null;
        origin = null;

        if (HasReadySkill(caster))
        {
            skill = caster.ActiveSkill;
            origin = ResolveSkillOrigin(caster, skill);
            return true;
        }

        caster = null;
        return false;
    }

    private bool HasReadySkill(Hero caster)
    {
        if (HasCaster(caster))
        {
            return caster.ActiveSkill != null;
        }

        return false;
    }

    private static bool HasCaster(Hero caster)
    {
        return caster != null;
    }

    // targetScope는 "어디를 클릭할 수 있는가"일 뿐, 실제로 칠하는 건 그 지점 중심의 타격 범위다.
    private Tile ResolveSkillOrigin(Hero caster, HeroActiveSkill skill)
    {
        if (IsSelfTargeted(skill))
        {
            return caster.CurrentTile;
        }

        return ResolveHoverOrigin(caster);
    }

    private static bool IsSelfTargeted(HeroActiveSkill skill)
    {
        return skill.targetScope == SkillTargetScope.Self;
    }

    private Tile ResolveHoverOrigin(Hero caster)
    {
        Tile hoverTile = game.HoverTile;

        if (IsOnCasterBoard(hoverTile, caster))
        {
            return hoverTile;
        }

        return null;
    }

    private static bool IsOnCasterBoard(Tile tile, Hero caster)
    {
        if (HasTile(tile))
        {
            return tile.Board == caster.Board;
        }

        return false;
    }

    private static bool HasTile(Tile tile)
    {
        return tile != null;
    }
}
