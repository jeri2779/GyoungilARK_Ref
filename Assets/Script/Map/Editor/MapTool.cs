/// <summary>
/// 맵 메이커에서 클릭이 무슨 일을 하는지 정하는 도구.
///
/// 팔레트(무엇을)와 도구(어떻게)를 나눈다 — 같은 "지상"을 골라도 칠하기는 데이터만 바꾸고
/// 교체는 큐브 실물을 갈아끼운다. 예전처럼 토글 하나로 뜻이 바뀌면 누르기 전에 알 수 없다.
/// </summary>
public enum MapTool
{
    Select,
    Paint,
    Swap,
    Erase,
    Pick,
    Route
}

/// <summary>도구 이름과 설명. 창과 예고줄이 같은 말을 쓰도록 여기 모아 둔다.</summary>
public static class MapToolWord
{
    public static string Name(MapTool tool)
    {
        switch (tool)
        {
            case MapTool.Select: return "선택";
            case MapTool.Paint: return "칠하기";
            case MapTool.Swap: return "교체";
            case MapTool.Erase: return "지우기";
            case MapTool.Route: return "경로";
            default: return "스포이드";
        }
    }

    /// <summary>이 도구가 타일을 바꾸는가. 선택·스포이드는 읽기만 한다.</summary>
    public static bool Writes(MapTool tool)
    {
        return tool == MapTool.Paint || tool == MapTool.Swap || tool == MapTool.Erase;
    }
}
