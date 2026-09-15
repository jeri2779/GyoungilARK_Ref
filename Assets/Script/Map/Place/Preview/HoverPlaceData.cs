using UnityEngine;

// 이번 프레임에 커서가 가리키는 배치 자리를 들고 있다가 내준다. 계산하지는 않는다.
public class HoverPlaceData
{
    private PlaceData data;
    private bool hasData;

    public PlaceData Data => data;
    public bool HasData => hasData;

    public void Keep(PlaceData placeData)
    {
        data = placeData;
        hasData = true;
    }

    public void Clear()
    {
        data = default;
        hasData = false;
    }
}
