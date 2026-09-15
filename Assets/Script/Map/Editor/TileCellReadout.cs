using System.Collections.Generic;

/// <summary>
/// 타일 한 칸을 기획 용어 한 줄로 옮긴다.
///
/// 창은 enum 이름(Ground/High/Special)이 아니라 기획서에서 쓰는 말(지상/고지/외곽 장식)로 말해야 한다.
/// 구역은 타일이 매달린 Tilemap 그룹 이름(Production·Combat·Boundary)에서 읽는다 —
/// 이미 씬에 있는 분류인데 인스펙터로는 눈에 안 들어오는 정보다.
/// </summary>
public static class TileCellReadout
{
    /// <summary>예: "(13, 4)  지상 · 전투구역 · 근접"</summary>
    public static string Describe(Tile tile)
    {
        var parts = new List<string>
        {
            TerrainWord(tile.Terrain),
            GroupWord(tile)
        };

        if (tile.State.Pass == PassType.Swim)
        {
            parts.Add("헤엄");
        }

        if (tile.IsFire)
        {
            parts.Add("불");
        }

        if (tile.IsCampfire)
        {
            parts.Add("모닥불");
        }

        parts.Add(AllowWord(tile));
        return $"({tile.Coord.x}, {tile.Coord.y})  {string.Join(" · ", parts)}";
    }

    private static string TerrainWord(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Ground: return "지상";
            case TerrainType.High: return "고지";
            case TerrainType.Special: return "외곽 장식";
            case TerrainType.Core: return "본진";
            default: return "빈 칸(벽)";
        }
    }

    // 타일이 매달린 Tilemap 그룹. 이름이 예상 밖이면 지어내지 않고 그대로 보여준다.
    private static string GroupWord(Tile tile)
    {
        string group = tile.transform.parent.name;

        switch (group)
        {
            case "Production": return "생산구역";
            case "Combat": return "전투구역";
            case "Boundary": return "외곽";
            default: return group;
        }
    }

    // 켜진 허용들. 지금 규칙에서 효력이 없는 것은 그렇다고 붙여 알린다(잘못이 아니라 대비일 수 있으므로).
    private static string AllowWord(Tile tile)
    {
        var allows = new List<string>();
        Append(allows, tile, MapBrush.Melee, "근접");
        Append(allows, tile, MapBrush.Ranged, "원거리");
        Append(allows, tile, MapBrush.Build, "생산");

        if (tile.IsEnemySpawn)
        {
            allows.Add("적 스폰");
        }

        if (allows.Count == 0)
        {
            return "배치 없음";
        }

        return string.Join(", ", allows);
    }

    private static void Append(List<string> allows, Tile tile, MapBrush brush, string word)
    {
        if (!TileFlagQuery.IsOn(tile, brush))
        {
            return;
        }

        if (TileFlagQuery.TakesEffect(tile.Terrain, brush))
        {
            allows.Add(word);
            return;
        }

        allows.Add($"{word}(지금 규칙에선 효과 없음)");
    }
}
