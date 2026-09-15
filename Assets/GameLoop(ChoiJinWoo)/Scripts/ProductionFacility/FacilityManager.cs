using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class FacilityManager
{
    private List<ProductionFacility> facilities = new();
    private List<(ProductionType Type, int Amount)> products = new();

    private ResourcesManager resourcesManager;
    private EnviromentManager enviromentManager;
    [Inject]
    private void Construct(ResourcesManager resourcesManager, EnviromentManager enviromentManager)
    {
        this.resourcesManager = resourcesManager;
        this.enviromentManager = enviromentManager;

        enviromentManager.OnDay += SumProduct;
    }   

    public void AddFacility(ProductionFacility facility)
    {
        facilities.Add(facility);
    }

    public void RemoveFacility(ProductionFacility facility)
    {
        facilities.Remove(facility);
    }

    public void SumProduct()
    {
        var totals = GetTotalProduction();
        resourcesManager.ProductChanged(totals);
        products.Clear();
    }

    // 하루가 끝나길 기다리지 않고 지금 배치된 인력 기준 일일 총 생산량을 읽고 싶을 때(가운데 성 UI 등).
    // 자원에 반영하지 않는 조회 전용 — SumProduct와 같은 합산 로직을 재사용한다.
    public (ProductionType Type, int Amount)[] GetTotalProduction()
    {
        products.Clear();
        foreach (var facility in facilities)
        {
            var product = facility.ProduceProduction();
            if (product == default)
                continue;

            int index = products.FindIndex(p => p.Type == product.Item1);
            if (index < 0)
            {
                products.Add((product.Item1, product.Item2));
            }
            else
            {
                products[index] = (product.Item1, products[index].Amount + product.Item2);
            }
        }

        return products.ToArray();
    }
}
