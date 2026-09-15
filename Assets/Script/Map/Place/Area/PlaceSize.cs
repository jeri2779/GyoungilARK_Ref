using UnityEngine;

// 배치물이 몇 칸을 차지하는지 낸다. 생산 시설·집은 기반시설 UI로 옮겨가 팔레트엔 이제 Hero만 남아있고, Hero는 전부 한 칸이다.
public static class PlaceSize
{
    public static Vector2Int GetSize(Placeable slot)
    {
        return Vector2Int.one;
    }
}
