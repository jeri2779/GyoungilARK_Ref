// 낮/밤에 따라 건설(영웅 배치)·스킬 시전 가능 여부를 판정한다.
public class DayNightBuildRule
{
    public GameManager rule;

    public bool CanBuild()
    {
        return rule == null || rule.CanBuild;
    }
}
