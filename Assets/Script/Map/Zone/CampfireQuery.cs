using UnityEngine;

// 계산을 다시 하지 않고 저장된 모닥불 보호 결과만 조회합니다.
public static class CampfireQuery
{
    // 지정 좌표가 추위 보호 영역인지 확인합니다.
    public static bool IsProtected(CampfireData data, Vector2Int cell)
    {
        if (data == null)
        {
            return false;
        }

        return data.Contains(cell);
    }
}
