using System;

// 강화 자원 비용만 담당하는 계산기. HeroSimCalc의 200줄 제한을 지키려고 분리했다.
public static class HeroSimCostCalc
{
    // 0레벨부터 목표 레벨까지 실제로 지불하는 자원 총합. 자원 종류별 값을 모두 더한다.
    public static int CalculateCumulativeCost(Func<int, (ProductionType Type, int Amount)[]> costOfLevel, int upgradeCount)
    {
        int total = 0;
        for (int level = 0; level < upgradeCount; level++)
        {
            total += SumLevelCost(costOfLevel(level));
        }
        return total;
    }

    // 한 레벨의 자원 비용을 모두 더한다. 원본이 음수로 돌려주므로 부호를 뒤집는다.
    private static int SumLevelCost((ProductionType Type, int Amount)[] costs)
    {
        int total = 0;
        for (int index = 0; index < costs.Length; index++)
        {
            total -= costs[index].Amount;
        }
        return total;
    }
}
