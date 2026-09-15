using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HeroUpgradeData", menuName = "UpgradeData/HeroUpgradeData")]
public class HeroUpgradeData : ScriptableObject
{
    public List<AttackDataSO> attackDatas;

    [SerializeField] private List<ResourceCost> cost;

    public (ProductionType Type, int Amount)[] Cost
    {
        get
        {
            var temp = new (ProductionType, int)[cost.Count];
            for (int i = 0; i < cost.Count; i++)
                temp[i] = (cost[i].Type, -cost[i].Amount);
            return temp;
        }
    }

    public void Upgrade(Hero hero) => hero.ExchangeAttackDatas(attackDatas);
}
