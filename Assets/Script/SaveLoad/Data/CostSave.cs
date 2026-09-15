using System;

// 자원 종류 + 수량 한 쌍 (건설·강화 실제 지불액 목록에 재사용)
[Serializable]
public class CostSave
{
    public ProductionType costType;   // 어떤 자원인지 (목재/식량/골드/철/석재)
    public int costAmount;            // 그 자원을 얼마나 썼는지
}
