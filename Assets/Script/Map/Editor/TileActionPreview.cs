using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지금 가리키는 칸을 클릭하면 무슨 일이 일어나는지 미리 한 줄로 말한다.
///
/// 같은 클릭이 상황마다 다른 일을 한다 — 스왑이 켜져 있으면 판을 얹거나 걷어내거나 밑판을 갈고,
/// 꺼져 있으면 데이터만 칠한다. 무엇이 될지 누르기 전에 보이지 않으면 눌러 보고서야 알게 된다.
///
/// 여기는 말만 만든다. 실제로 바꾸는 것은 TileSwap과 TileStamp다 —
/// 두 곳의 판단 규칙이 갈리면 예고가 거짓말이 되므로 분기 순서를 같게 맞춰 둔다.
/// </summary>
public static class TileActionPreview
{
    /// <summary>이 붓이 실물을 갈아끼우는 붓인가. 장식은 지형이 아니지만 얹을 실물이 있다.</summary>
    public static bool Swaps(MapBrush brush)
    {
        return brush == MapBrush.Ground || brush == MapBrush.High || brush == MapBrush.Core
            || brush == MapBrush.Special || brush == MapBrush.Empty || brush == MapBrush.Decor;
    }

    /// <summary>지형 붓이 가리키는 지형. 지형 붓이 아니면 Empty로 떨어진다.</summary>
    public static TerrainType TerrainOf(MapBrush brush)
    {
        switch (brush)
        {
            case MapBrush.Ground: return TerrainType.Ground;
            case MapBrush.High: return TerrainType.High;
            case MapBrush.Core: return TerrainType.Core;
            case MapBrush.Special: return TerrainType.Special;
            case MapBrush.Empty: return TerrainType.Empty;
            default: return TerrainType.Empty;
        }
    }

    /// <summary>이 칸에 쌓인 겹을 사람 말로. 예: "2겹 — 외곽 + 고지 판 · 장식 1"</summary>
    public static string DescribeStack(Grid module, Vector2Int coord, Tile tile)
    {
        List<Tile> layers = TileSwap.Stack(module, coord);
        string stack = "1겹";

        if (layers.Count > 1)
        {
            var words = new List<string>();
            foreach (Tile layer in layers)
            {
                words.Add(Word(layer.State.Terrain));
            }

            stack = $"{layers.Count}겹 — {string.Join(" + ", words)}";
        }

        int decor = DecorPlace.Count(module, tile);
        if (decor > 0)
        {
            stack += $" · 장식 {decor}";
        }

        return stack;
    }

    /// <summary>클릭하면 무엇이 되는지. turnOff는 Alt를 누르고 있는 상태(끄는 쪽으로 찍기).</summary>
    public static string Describe(Grid module, Vector2Int coord, Tile tile,
        MapTool tool, MapBrush brush, GameObject pick, bool turnOff)
    {
        if (tool == MapTool.Select)
        {
            return "계층에서 이 타일을 고릅니다 — 데이터는 바뀌지 않습니다";
        }

        if (tool == MapTool.Pick)
        {
            return $"이 칸의 {Word(tile.Terrain)}을(를) 팔레트로 가져옵니다";
        }

        if (tool == MapTool.Erase)
        {
            return EraseWord(module, coord, tile);
        }

        if (brush == MapBrush.None)
        {
            return "팔레트에서 무엇을 칠할지 먼저 고르세요";
        }

        if (brush == MapBrush.Spawn)
        {
            return turnOff ? "적 스폰 끄기" : "적 스폰 켜기";
        }

        if (brush == MapBrush.Swim)
        {
            return SwimWord(tile, turnOff);
        }

        if (brush == MapBrush.Fire)
        {
            return FireWord(turnOff);
        }

        if (brush == MapBrush.Campfire)
        {
            return CampfireWord(turnOff);
        }

        if (IsPlaceBrush(brush))
        {
            return PlaceWord(tile, brush, turnOff);
        }

        // 장식은 지형 값이 없다 — 칠하기로는 기록할 자리가 없고 교체로만 얹힌다.
        if (brush == MapBrush.Decor)
        {
            if (tool != MapTool.Swap)
            {
                return "장식은 규칙 데이터가 아닙니다 — 교체 도구로 얹으세요";
            }

            if (pick == null)
            {
                return "얹을 장식 프리팹을 먼저 고르세요 (테마에 장식이 없으면 에셋에 추가)";
            }

            int on = DecorPlace.Count(module, tile);
            return $"이 칸 위에 {pick.name}을(를) 얹습니다 (장식 {on}개 → {on + 1}개)";
        }

        TerrainType want = TerrainOf(brush);
        if (tool == MapTool.Swap)
        {
            if (pick == null)
            {
                return $"{Word(want)}에 고른 프리팹이 없습니다 — 아래 선반에서 하나 고르세요";
            }

            return SwapWord(module, coord, want, pick);
        }

        if (tile.Terrain == want)
        {
            return $"이미 {Word(want)} — 바뀌는 것 없음";
        }

        return $"{Word(tile.Terrain)} → {Word(want)} · 데이터만 (큐브는 그대로라 겉모습이 어긋납니다)";
    }

    // 지우기는 제일 위 한 겹만 없앤다. 마지막 겹이면 그 칸에 타일이 아예 없어진다.
    // 장식이 얹혀 있으면 그것이 제일 위라 먼저 걷힌다 — 실제 지우기와 같은 순서로 말한다.
    private static string EraseWord(Grid module, Vector2Int coord, Tile tile)
    {
        int decor = DecorPlace.Count(module, tile);
        if (decor > 0)
        {
            string name = DecorPlace.TopName(module, tile);
            return $"장식 {name} 하나를 걷어냅니다 (장식 {decor}개 → {decor - 1}개, 타일은 그대로)";
        }

        List<Tile> layers = TileSwap.Stack(module, coord);
        if (layers.Count == 0)
        {
            return "타일이 없는 칸 — 지울 것이 없습니다";
        }

        Tile top = layers[layers.Count - 1];
        if (layers.Count == 1)
        {
            return $"마지막 {Word(top.State.Terrain)} 한 겹을 지웁니다 — 이 칸에 타일이 없어집니다";
        }

        return $"{Word(top.State.Terrain)} 한 겹을 걷어냅니다 ({layers.Count}겹 → {layers.Count - 1}겹)";
    }

    // 스왑이 실제로 할 일. TileSwap.Apply의 분기와 같은 순서로 판단하고, 벽 판단은 그쪽 함수에 물어본다.
    private static string SwapWord(Grid module, Vector2Int coord, TerrainType want, GameObject prefab)
    {
        List<Tile> layers = TileSwap.Stack(module, coord);
        if (layers.Count == 0)
        {
            return "타일이 없는 칸 — 아무것도 하지 않습니다";
        }

        Tile bottom = layers[0];
        Tile top = layers[layers.Count - 1];

        if (want == TerrainType.High)
        {
            if (top.State.Terrain == TerrainType.High)
            {
                return "이미 고지 — 판을 겹쳐 쌓지 않습니다";
            }

            return $"{Word(top.State.Terrain)} → 고지 · 위에 판을 얹습니다 " +
                   $"({layers.Count}겹 → {layers.Count + 1}겹, {prefab.name})" +
                   $"{DefaultWord(TerrainType.High)}{SpawnWord(top)}";
        }

        if (top.State.Terrain == want)
        {
            return SkinWord(layers.Count, want, prefab);
        }

        if (TileSwap.Walled(layers))
        {
            return $"{Word(top.State.Terrain)} 겹이 얹혀 있습니다 — 지우기로 먼저 걷으세요 (교체하지 않습니다)";
        }

        if (layers.Count > 1)
        {
            // 벽이 없다는 것은 위에 얹힌 것이 고지뿐이라는 뜻이다 — 전부 걷으면 밑판 한 겹만 남는다.
            // 밑판이 이미 그 지형이면 밑판 저작은 손대지 않는다(바뀌는 것은 겹 구조와 실물뿐).
            string kept = bottom.State.Terrain == want
                ? " · 밑판의 배치 저작은 그대로"
                : DefaultWord(want);

            return $"고지 → {Word(want)} · 판을 걷어내고 밑판을 교체합니다 " +
                   $"({layers.Count}겹 → 1겹, {prefab.name}){kept}";
        }

        return $"{Word(bottom.State.Terrain)} → {Word(want)} · 밑판을 교체합니다 " +
               $"({prefab.name}){DefaultWord(want)}";
    }

    // 지형이 그대로인 교체 = 겉모습만 바꾸기. 데이터가 그대로라는 것을 같이 말한다.
    private static string SkinWord(int layers, TerrainType want, GameObject prefab)
    {
        if (layers == 1)
        {
            return $"{Word(want)} 실물을 {prefab.name}으로 갈아끼웁니다 — 지형·배치 저작은 그대로";
        }

        return $"보이는 {Word(want)} 겹을 {prefab.name}으로 갈아끼웁니다 " +
               $"({layers}겹 그대로, 지형·배치 저작은 그대로)";
    }

    // 지형이 바뀌는 칸은 배치 허용이 그 지형 기본으로 다시 깔린다 — 손으로 켜 둔 값이 덮이므로 미리 말한다.
    private static string DefaultWord(TerrainType terrain)
    {
        MapBrush basic = TileTerrainDefault.Of(terrain);
        if (basic == MapBrush.None)
        {
            return " · 배치 허용은 모두 꺼집니다";
        }

        return $" · {AllowWord(basic)} 배치를 켭니다";
    }

    // 스폰이 찍힌 칸을 고지로 올리면 표식이 새 판으로 따라 올라간다 — 말없이 옮기면 스폰이 사라진 줄 안다.
    private static string SpawnWord(Tile top)
    {
        return top.isEnemySpawn ? " · 적 스폰 표식도 새 판으로 옮깁니다" : string.Empty;
    }

    // 배치 허용 붓. 켜도 지금 지형에선 효과가 없으면 그 자리에서 알린다.
    private static string PlaceWord(Tile tile, MapBrush brush, bool turnOff)
    {
        string word = AllowWord(brush);
        if (turnOff)
        {
            return $"{word} 끄기";
        }

        if (tile.State.Pass != PassType.Walk)
        {
            return $"{word} 켜기 — 다만 헤엄 칸에는 아군을 놓을 수 없습니다";
        }

        if (TileFlagQuery.TakesEffect(tile.Terrain, brush))
        {
            return $"{word} 켜기";
        }

        return $"{word} 켜기 — 다만 {Word(tile.Terrain)}에서는 지금 규칙상 효과가 없습니다";
    }

    // 통행 방식 붓. 지형이 원래 못 지나는 칸이면 켜도 달라지는 것이 없으므로 그 자리에서 알린다.
    private static string SwimWord(Tile tile, bool turnOff)
    {
        if (turnOff)
        {
            return "헤엄 통행 끄기 — 걷는 적이 다시 지나갑니다";
        }

        if (!tile.Walkable)
        {
            return $"헤엄 통행 켜기 — 다만 {Word(tile.Terrain)}은 원래 못 지나는 칸입니다";
        }

        return "헤엄 통행 켜기 — 걷는 적은 이 칸을 못 지납니다(길이 돌아갑니다)";
    }

    // 불 붓. 길을 막지도, 배치를 막지도 않으므로 지형과 상관없이 말이 하나다.
    private static string FireWord(bool turnOff)
    {
        if (turnOff)
        {
            return "불 끄기";
        }

        return "불 켜기 — 올라선 아군·적이 모두 지속피해를 받습니다";
    }

    // 모닥불 붓. 지금 단계에서는 고정 보호 타일로 저장하는 의미만 안내한다.
    private static string CampfireWord(bool turnOff)
    {
        if (turnOff)
        {
            return "모닥불 끄기";
        }

        return "모닥불 켜기 — 추위 보호용 고정 타일로 저장합니다";
    }

    private static bool IsPlaceBrush(MapBrush brush)
    {
        return brush == MapBrush.Melee || brush == MapBrush.Ranged || brush == MapBrush.Build;
    }

    private static string AllowWord(MapBrush brush)
    {
        switch (brush)
        {
            case MapBrush.Melee: return "근접";
            case MapBrush.Ranged: return "원거리";
            default: return "생산";
        }
    }

    private static string Word(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Ground: return "지상";
            case TerrainType.High: return "고지";
            case TerrainType.Special: return "외곽";
            case TerrainType.Core: return "본진";
            default: return "빈 칸(벽)";
        }
    }
}
