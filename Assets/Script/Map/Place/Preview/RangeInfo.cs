using UnityEngine;

// 영웅에게 설정된 실제 사거리 정보를 읽는다.
public class RangeInfo
{
    public bool TryGet(
        GameObject unit,
        out int range,
        out RangeShape shape)
    {
        range = 0;
        shape = RangeShape.Diamond;

        bool hasUnit = unit != null;
        if (!hasUnit)
        {
            return false;
        }

        bool hasHero = unit.TryGetComponent(out Hero hero);
        if (!hasHero)
        {
            return false;
        }

        range = Mathf.Max(0, hero.Range);
        shape = hero.RangeShape;
        return true;
    }
}
