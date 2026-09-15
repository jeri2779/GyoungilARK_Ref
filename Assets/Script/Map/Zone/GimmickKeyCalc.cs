// 기믹 칸을 팝업에 띄울 문자열 표 키로 바꾼다.
public static class GimmickKeyCalc
{
    private const string KeyPrefix = "Gimmick_";
    private const string NameSuffix = "_Name";
    private const string DescSuffix = "_Desc";
    private const string WaterKind = "Water";

    // 그 칸의 이름 문자열 키를 만든다.
    public static string MakeNameKey(Tile tile)
    {
        return KeyPrefix + KindName(tile) + NameSuffix;
    }

    // 그 칸의 설명 문자열 키를 만든다.
    public static string MakeDescKey(Tile tile)
    {
        return KeyPrefix + KindName(tile) + DescSuffix;
    }

    // 기믹이 걸린 칸이면 그 종류를, 아니면 물로 본다.
    private static string KindName(Tile tile)
    {
        if (tile.Gimmick == GimmickType.None)
        {
            return WaterKind;
        }
        return tile.Gimmick.ToString();
    }
}
