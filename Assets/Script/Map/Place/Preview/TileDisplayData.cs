using UnityEngine;

// 이번 프레임에 뭘 보여줘야 하는지 담아 지난 프레임과 비교하는 값 묶음. 계산도 표시도 하지 않는다.
public readonly struct TileDisplayData
{
    public readonly HoverMode Mode;
    public readonly MapBoard Board;
    public readonly Vector2Int Origin;
    public readonly GameObject Unit;
    public readonly OccupantKind Kind;
    public readonly bool CanPlace;
    public readonly int RangeVersion;
    public readonly Hero SkillCaster;
    public readonly HeroActiveSkill Skill;
    public readonly Tile SkillOrigin;
    public readonly PlayerSkillSlot ArmedSkill;
    public readonly Tile PlayerSkillOrigin;

    public TileDisplayData(
        HoverMode mode,
        MapBoard board,
        Vector2Int origin,
        GameObject unit,
        OccupantKind kind,
        bool canPlace,
        int rangeVersion,
        Hero skillCaster,
        HeroActiveSkill skill,
        Tile skillOrigin,
        PlayerSkillSlot armedSkill,
        Tile playerSkillOrigin)
    {
        Mode = mode;
        Board = board;
        Origin = origin;
        Unit = unit;
        Kind = kind;
        CanPlace = canPlace;
        RangeVersion = rangeVersion;
        SkillCaster = skillCaster;
        Skill = skill;
        SkillOrigin = skillOrigin;
        ArmedSkill = armedSkill;
        PlayerSkillOrigin = playerSkillOrigin;
    }

    public bool SameAs(TileDisplayData other)
    {
        return Mode == other.Mode
            && Board == other.Board
            && Origin == other.Origin
            && Unit == other.Unit
            && Kind == other.Kind
            && CanPlace == other.CanPlace
            && RangeVersion == other.RangeVersion
            && SkillCaster == other.SkillCaster
            && Skill == other.Skill
            && SkillOrigin == other.SkillOrigin
            && ArmedSkill == other.ArmedSkill
            && PlayerSkillOrigin == other.PlayerSkillOrigin;
    }
}

public enum HoverMode { Blocked, Placing, Held, Range }
