using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 모듈의 타일 격자를 2D로 그린다.
///
/// 한 칸에 두 층의 정보를 겹쳐 보여준다 — 바탕색은 지형(큰 분류), 칸 안의 점은 배치 허용(작은 분류).
/// 지금 고른 붓과 상관없는 칸은 배경 쪽으로 죽여, 무슨 모드로 보고 있는지가 격자 자체에 드러나게 한다.
///
/// row 0을 아래에 놓는다. 씬을 위에서 내려다본 그림과 위아래가 같아야 좌표를 옮겨 짚을 수 있다.
/// </summary>
public class TileGridView
{
    /// <summary>좌·상단 여백 — 좌표 라벨 자리.</summary>
    public const int Pad = 22;

    private readonly Dictionary<Vector2Int, Tile> _cells;
    private readonly MapBrush _brush;

    public int Cols { get; }
    public int Rows { get; }

    public TileGridView(Dictionary<Vector2Int, Tile> cells, MapBrush brush)
    {
        _cells = cells;
        _brush = brush;

        int maxCol = 0;
        int maxRow = 0;
        foreach (Vector2Int coord in cells.Keys)
        {
            if (coord.x > maxCol)
            {
                maxCol = coord.x;
            }

            if (coord.y > maxRow)
            {
                maxRow = coord.y;
            }
        }

        Cols = maxCol + 1;
        Rows = maxRow + 1;
    }

    public float PixelWidth(int cellPixels)
    {
        return Pad + Cols * cellPixels + 6;
    }

    public float PixelHeight(int cellPixels)
    {
        return Pad + Rows * cellPixels + 6;
    }

    /// <summary>
    /// 격자와 경로를 그린다. 각 경로는 스폰→본진 순서로 정렬돼 있어야 한다.
    /// chosen은 지금 고른 경로의 번호(없으면 -1), nodes는 그 경로에 사람이 찍은 경유 칸이다.
    /// overrides에 든 칸은 청록 구석 표식(프리팹과 다름), showInert면 무효 조합 칸에 주황 구석 표식.
    /// </summary>
    public void Draw(Rect area, int cellPixels, IReadOnlyList<LaneData> lanes, int chosen,
        IReadOnlyList<RouteNode> nodes, Vector2Int hover,
        HashSet<Vector2Int> overrides, bool showInert, CampfireData campfireData = null,
        HashSet<Vector2Int> windwallShape = null)
    {
        Vector2Int focus = FocusSpawn(lanes, chosen);
        HashSet<Vector2Int> route = RouteCells(lanes, chosen);

        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                var coord = new Vector2Int(col, row);
                bool exists = _cells.TryGetValue(coord, out Tile tile);
                if (!exists)
                {
                    continue; // 타일이 아예 없는 칸은 그리지 않는다(Empty로 칠한 벽과 구분된다)
                }

                Rect rect = CellRect(area, cellPixels, col, row);
                DrawCell(rect, tile, cellPixels, SpawnLit(focus, coord), RouteLit(route, coord));
                DrawCampfireRange(rect, coord, campfireData);
                DrawWindwallRange(rect, coord, windwallShape);

                // 붓과 무관한 상시 표식이라 DrawCell(붓에 따라 죽는 층) 위에 얹는다.
                if (overrides != null && overrides.Contains(coord))
                {
                    DrawCorner(rect, cellPixels, MapMakerPalette.Override, true);
                }

                if (showInert && TileFlagQuery.IsInert(tile))
                {
                    DrawCorner(rect, cellPixels, MapMakerPalette.Inert, false);
                }
            }
        }

        DrawLanes(area, cellPixels, lanes, chosen, nodes);
        DrawHover(area, cellPixels, hover);
        DrawAxisLabels(area, cellPixels);
    }

    // 실제 공용 계산 결과에 포함된 타일에 모닥불 보호 테두리를 표시합니다.
    private static void DrawCampfireRange(Rect rect, Vector2Int coord, CampfireData data)
    {
        if (!CampfireQuery.IsProtected(data, coord))
        {
            return;
        }

        Color color = MapMakerPalette.Campfire;
        color.a = 0.7f;
        const float line = 2f;

        if (!CampfireQuery.IsProtected(data, coord + Vector2Int.up))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, line), color);
        }

        if (!CampfireQuery.IsProtected(data, coord + Vector2Int.down))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - line, rect.width, line), color);
        }

        if (!CampfireQuery.IsProtected(data, coord + Vector2Int.left))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, line, rect.height), color);
        }

        if (!CampfireQuery.IsProtected(data, coord + Vector2Int.right))
        {
            EditorGUI.DrawRect(new Rect(rect.xMax - line, rect.y, line, rect.height), color);
        }
    }

    // 가림막 원점과 보호 팔을 하나로 합친 덩어리(shape)의 바깥 경계만 십자 모양 외곽선으로 표시합니다.
    // 덩어리 안쪽에서 서로 붙은 변에는 선을 안 그어, 원점·팔이 상자 여러 개로 겹쳐 보이지 않고
    // 하나로 이어진 윤곽선이 된다.
    private static void DrawWindwallRange(Rect rect, Vector2Int coord, HashSet<Vector2Int> shape)
    {
        if (shape == null || !shape.Contains(coord))
        {
            return;
        }

        Color color = MapMakerPalette.Windwall;
        const float line = 3f;

        if (!shape.Contains(coord + Vector2Int.up))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, line), color);
        }

        if (!shape.Contains(coord + Vector2Int.down))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - line, rect.width, line), color);
        }

        if (!shape.Contains(coord + Vector2Int.left))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, line, rect.height), color);
        }

        if (!shape.Contains(coord + Vector2Int.right))
        {
            EditorGUI.DrawRect(new Rect(rect.xMax - line, rect.y, line, rect.height), color);
        }
    }

    // 고른 경로의 출발 칸. 고른 것이 없으면 (-1,-1)이라 어느 칸과도 같지 않다.
    private static Vector2Int FocusSpawn(IReadOnlyList<LaneData> lanes, int chosen)
    {
        if (chosen < 0 || chosen >= lanes.Count || lanes[chosen].Start == null)
        {
            return new Vector2Int(-1, -1);
        }

        return lanes[chosen].Start.Coord;
    }

    /// <summary>
    /// 고른 경로가 지나가는 칸들. 고른 것이 없거나 그 경로가 막혔으면 null이다.
    ///
    /// 하나를 고른 사람은 그 길만 보려는 것이다 — 나머지 칸이 제 색으로 남아 있으면
    /// 지형이 눈에 먼저 들어와 정작 봐야 할 길이 배경에 묻힌다. 붓을 고르면 무관한 칸이 죽는 것과 같다.
    /// </summary>
    private static HashSet<Vector2Int> RouteCells(IReadOnlyList<LaneData> lanes, int chosen)
    {
        if (chosen < 0 || chosen >= lanes.Count)
        {
            return null;
        }

        IReadOnlyList<Tile> path = lanes[chosen].Tiles;
        if (path.Count == 0)
        {
            return null; // 막힌 경로 — 살릴 칸이 없어서 죽이면 판이 통째로 어두워진다
        }

        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < path.Count; i++)
        {
            cells.Add(path[i].Coord);
        }

        return cells;
    }

    // 이 칸이 고른 경로 위에 있는가. 고른 경로가 없으면 전부 살린다.
    private static bool RouteLit(HashSet<Vector2Int> route, Vector2Int coord)
    {
        if (route == null)
        {
            return true;
        }

        return route.Contains(coord);
    }

    // 이 스폰 표식을 살릴 것인가. 고른 경로가 없으면 전부 살린다(예전 그대로).
    private static bool SpawnLit(Vector2Int focus, Vector2Int coord)
    {
        if (focus.x < 0)
        {
            return true;
        }

        return focus == coord;
    }

    // 오버라이드/무효 구석 표식. top이면 오른쪽 위, 아니면 오른쪽 아래에 작은 사각형을 둔다 —
    // 지형색·배치 점(아래 가운데)·경로선과 겹치지 않는 구석이라 층이 늘어도 서로 안 가린다.
    private static void DrawCorner(Rect rect, int cellPixels, Color color, bool top)
    {
        float size = Mathf.Max(3f, cellPixels * 0.28f);
        float x = rect.xMax - size;
        float y = top ? rect.y : rect.yMax - size;
        EditorGUI.DrawRect(new Rect(x, y, size, size), color);
    }

    /// <summary>마우스 위치가 가리키는 칸. 격자 밖이면 (-1,-1).</summary>
    public Vector2Int CoordAt(Vector2 mouse, Rect area, int cellPixels)
    {
        float localX = mouse.x - (area.x + Pad);
        float localY = mouse.y - (area.y + Pad);

        int col = Mathf.FloorToInt(localX / cellPixels);
        int rowFromTop = Mathf.FloorToInt(localY / cellPixels);
        int row = Rows - 1 - rowFromTop;

        var coord = new Vector2Int(col, row);
        if (!GridCalculator.IsInGrid(coord, Cols, Rows))
        {
            return new Vector2Int(-1, -1);
        }

        return coord;
    }

    // ---- 칸 ----

    private void DrawCell(Rect rect, Tile tile, int cellPixels, bool spawnLit, bool routeLit)
    {
        bool lit = Lit(tile) && routeLit;

        Color fill = MapMakerPalette.Terrain(tile.Terrain);
        Color top = MapMakerPalette.HighTop;
        Color mark = MapMakerPalette.CoreMark;
        if (!lit)
        {
            fill = MapMakerPalette.Dim(fill);
            top = MapMakerPalette.Dim(top);
            mark = MapMakerPalette.Dim(mark);
        }

        EditorGUI.DrawRect(rect, fill);

        // 고지는 한 단 올라와 있다 — 윗면에 밝은 띠를 얹어 지상과 실루엣부터 다르게 만든다.
        if (tile.Terrain == TerrainType.High)
        {
            float band = Mathf.Max(2f, cellPixels * 0.22f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, band), top);
        }

        if (tile.Terrain == TerrainType.Core)
        {
            EditorGUI.DrawRect(Inset(rect, cellPixels * 0.28f), mark);
        }

        // 헤엄 칸은 지형이 지상이라 바탕색이 같다 — 아래 띠로 붓과 상관없이 늘 보이게 한다.
        if (tile.State.Pass == PassType.Swim)
        {
            DrawSwim(rect, cellPixels, lit);
        }

        // 불 칸도 지형색이 지상과 같다 — 헤엄의 아래 띠와 겹치지 않게 오른쪽 세로 띠로 둔다.
        if (tile.IsFire)
        {
            DrawFire(rect, cellPixels, lit);
        }

        if (tile.IsCampfire)
        {
            DrawCampfire(rect, cellPixels, lit);
        }

        if (tile.IsEnemySpawn)
        {
            DrawSpawn(rect, spawnLit);
        }

        // 지금 고른 배치 허용이 켜진 칸에만 점을 찍는다 — 모드마다 다른 층을 보는 셈이다.
        if (lit && TileFlagQuery.IsOn(tile, _brush))
        {
            float size = Mathf.Max(4f, cellPixels * 0.26f);
            float x = rect.x + (rect.width - size) * 0.5f;
            float y = rect.y + rect.height - size - 2f;
            DrawFlagMark(new Rect(x, y, size, size), tile);
        }

        if (TileAuthorRule.IsDeadCell(tile))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), MapMakerPalette.Problem);
        }

        DrawLetter(rect, tile, cellPixels, lit);
    }

    // 헤엄 칸 아래 띠. 걷는 적이 못 지나는 칸이라는 표시다.
    private static void DrawSwim(Rect rect, int cellPixels, bool lit)
    {
        Color color = MapMakerPalette.Swim;
        if (!lit)
        {
            color = MapMakerPalette.Dim(color);
        }

        float band = Mathf.Max(2f, cellPixels * 0.22f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - band, rect.width, band), color);
    }

    // 불 칸 오른쪽 띠. 올라선 유닛이 지속피해를 받는 칸이라는 표시다.
    private static void DrawFire(Rect rect, int cellPixels, bool lit)
    {
        Color color = MapMakerPalette.Fire;
        if (!lit)
        {
            color = MapMakerPalette.Dim(color);
        }

        float band = Mathf.Max(2f, cellPixels * 0.22f);
        EditorGUI.DrawRect(new Rect(rect.xMax - band, rect.y, band, rect.height), color);
    }

    // 모닥불 칸 위쪽 띠. 불의 오른쪽 띠와 겹치지 않아 두 기믹을 바로 구분할 수 있다.
    private static void DrawCampfire(Rect rect, int cellPixels, bool lit)
    {
        Color color = MapMakerPalette.Campfire;
        if (!lit)
        {
            color = MapMakerPalette.Dim(color);
        }

        float band = Mathf.Max(2f, cellPixels * 0.22f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, band), color);
    }

    /// <summary>
    /// 적 스폰 테두리. 스폰이 여럿이면 전부 같은 금색이라 어느 길의 출발점인지 갈리지 않는다 —
    /// 경로를 하나 고르면 그 스폰만 남기고 나머지는 경로선과 같은 방식으로 죽인다.
    /// 고른 것은 두께로도 갈라, 색이 비슷해 보이는 화면에서도 구분된다.
    /// </summary>
    private static void DrawSpawn(Rect rect, bool lit)
    {
        if (lit)
        {
            DrawBorder(rect, MapMakerPalette.Spawn, 3f);
            return;
        }

        DrawBorder(rect, MapMakerPalette.Dim(MapMakerPalette.Spawn), 2f);
    }

    // 칸 글자(P·G·H·B·C). 색만으로는 생산 바닥과 전투 지상이 둘 다 초록이라 갈리지 않는다.
    // 칸이 너무 작으면 글자가 뭉개져 오히려 방해되므로 그때는 색만 남긴다.
    private static void DrawLetter(Rect rect, Tile tile, int cellPixels, bool lit)
    {
        if (cellPixels < 15)
        {
            return;
        }

        if (_letter == null)
        {
            _letter = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        }

        Color ink = lit ? MapMakerPalette.Letter : MapMakerPalette.Dim(MapMakerPalette.Letter);
        _letter.normal.textColor = ink;
        GUI.Label(rect, TileZone.Letter(tile), _letter);
    }

    private static GUIStyle _letter;

    /// <summary>
    /// 켜진 허용 표식. 지금 규칙에서 효력이 있으면 꽉 찬 네모, 없으면 속 빈 네모다.
    /// 고지에 근접을 찍는 것 같은 조작은 잘못이 아니라 나중을 위한 대비일 수 있으므로 막지 않는다 —
    /// 다만 지금은 아무 일도 일어나지 않는다는 것이 찍은 자리에서 바로 보여야 한다.
    /// </summary>
    private void DrawFlagMark(Rect dot, Tile tile)
    {
        if (TileFlagQuery.TakesEffect(tile.Terrain, _brush))
        {
            EditorGUI.DrawRect(dot, MapMakerPalette.Mark);
            return;
        }

        DrawBorder(dot, MapMakerPalette.Mark, 1f);
    }

    /// <summary>이 칸이 지금 고른 붓의 대상인가. None이면 전부 밝게 둔다(읽기 전용).</summary>
    private bool Lit(Tile tile)
    {
        switch (_brush)
        {
            case MapBrush.None: return true;
            case MapBrush.Ground: return tile.Terrain == TerrainType.Ground;
            case MapBrush.High: return tile.Terrain == TerrainType.High;
            case MapBrush.Core: return tile.Terrain == TerrainType.Core;
            case MapBrush.Special: return tile.Terrain == TerrainType.Special;
            case MapBrush.Empty: return tile.Terrain == TerrainType.Empty;
            case MapBrush.Spawn: return tile.IsEnemySpawn;
            case MapBrush.Swim: return tile.State.Pass == PassType.Swim;
            case MapBrush.Fire: return tile.IsFire;
            case MapBrush.Campfire: return tile.IsCampfire;
            case MapBrush.Windwall: return tile.IsWindwall;
            default: return TileFlagQuery.IsOn(tile, _brush);
        }
    }

    // ---- 경로 ----

    /// <summary>
    /// 경로를 칸 채우기가 아니라 선으로 긋는다. 채우면 그 칸의 지형과 배치 표식이 가려져,
    /// "길이 어디로 나는가"와 "이 칸이 무엇인가"를 동시에 볼 수 없다.
    /// 4방향 경로라 모든 구간이 가로 또는 세로다 — 사각형 두 장으로 검은 테두리와 흰 선을 만든다.
    /// </summary>
    /// <summary>
    /// 경로가 여럿이면 전부 같은 흰 선이라 어느 스폰의 길인지 갈리지 않는다.
    /// 하나를 고르면 나머지를 배경 쪽으로 죽인다 — 붓을 고르면 무관한 칸이 죽는 것과 같은 방식이다.
    /// 고른 것을 나중에 그려, 죽인 선과 겹치는 구간에서도 위에 오게 한다.
    /// </summary>
    private void DrawLanes(Rect area, int cellPixels, IReadOnlyList<LaneData> lanes, int chosen,
        IReadOnlyList<RouteNode> nodes)
    {
        bool focused = chosen >= 0 && chosen < lanes.Count;

        for (int i = 0; i < lanes.Count; i++)
        {
            if (focused && i == chosen)
            {
                continue;
            }

            DrawLane(area, cellPixels, lanes[i], LaneColor(i, !focused), false);
        }

        if (!focused)
        {
            return;
        }

        DrawLane(area, cellPixels, lanes[chosen], MapMakerPalette.Lane(chosen), true);
        DrawNodes(area, cellPixels, nodes);
    }

    // 이 경로의 선 색. 다른 경로를 고른 동안에는 배경 쪽으로 죽여 고른 것만 앞으로 나오게 한다.
    private static Color LaneColor(int index, bool lit)
    {
        Color color = MapMakerPalette.Lane(index);
        if (lit)
        {
            return color;
        }

        return MapMakerPalette.Dim(color);
    }

    private void DrawLane(Rect area, int cellPixels, LaneData lane, Color color, bool lit)
    {
        if (!lane.IsValid)
        {
            DrawFailed(area, cellPixels, lane.Start, lit);
            return;
        }

        // 헤엄 경로를 먼저 깔아 둔다 — 걷기 선이 그 위를 덮어 그리므로, 물을 지나 갈라지는
        // 구간만 물색 선이 삐져나와 보인다(겹치는 구간은 걷기 선에 가려 평소와 같다).
        DrawSwimUnderlay(area, cellPixels, lane.SwimTiles, lit);
        DrawPath(area, cellPixels, lane.Tiles, color, lit);

        if (lit)
        {
            DrawFlow(area, cellPixels, lane.Tiles, color);
        }
    }

    // 헤엄 적이 실제로 밟는 물길. 물이 없는 레인은 걷기 경로와 완전히 겹쳐 그려도 티가 안 난다.
    private void DrawSwimUnderlay(Rect area, int cellPixels, IReadOnlyList<Tile> swimPath, bool lit)
    {
        if (swimPath.Count == 0)
        {
            return;
        }

        Color color = lit ? MapMakerPalette.Swim : MapMakerPalette.Dim(MapMakerPalette.Swim);
        DrawPath(area, cellPixels, swimPath, color, false);
    }

    // 죽인 선은 검은 테두리를 빼고 한 겹만 남긴다 — 테두리까지 그리면 죽여도 여전히 눈에 먼저 든다.
    private void DrawPath(Rect area, int cellPixels, IReadOnlyList<Tile> path, Color color, bool lit)
    {
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector2 from = CellCenter(area, cellPixels, path[i].Coord);
            Vector2 to = CellCenter(area, cellPixels, path[i + 1].Coord);

            float x = Mathf.Min(from.x, to.x);
            float y = Mathf.Min(from.y, to.y);
            float width = Mathf.Abs(from.x - to.x);
            float height = Mathf.Abs(from.y - to.y);

            if (lit)
            {
                EditorGUI.DrawRect(new Rect(x - 2.5f, y - 2.5f, width + 5f, height + 5f),
                    MapMakerPalette.PathEdge);
            }

            EditorGUI.DrawRect(new Rect(x - 1f, y - 1f, width + 2f, height + 2f), color);
        }
    }

    /// <summary>
    /// 고른 경로에 진행 방향 화살표를 얹는다.
    ///
    /// 선만 있으면 스폰과 본진이 둘 다 화면 끝에 있어 어느 쪽에서 어느 쪽으로 가는지 읽히지 않는다.
    /// 되돌아오는 구간에서는 같은 칸에 반대 방향 화살표가 겹쳐 그 자리가 왕복임을 드러낸다.
    /// </summary>
    private void DrawFlow(Rect area, int cellPixels, IReadOnlyList<Tile> path, Color color)
    {
        int gap = Mathf.Max(2, Mathf.CeilToInt(56f / Mathf.Max(cellPixels, 1)));
        float size = Mathf.Clamp(cellPixels * 0.46f, 7f, 13f);

        for (int i = 0; i + 1 < path.Count; i += gap)
        {
            Vector2 from = CellCenter(area, cellPixels, path[i].Coord);
            Vector2 to = CellCenter(area, cellPixels, path[i + 1].Coord);

            DrawArrow((from + to) * 0.5f, to - from, size + 3f, MapMakerPalette.PathEdge);
            DrawArrow((from + to) * 0.5f, to - from, size, color);
        }
    }

    // 삼각형 하나를 돌려 쓴다 — 4방향 격자라 각도가 네 가지뿐이다.
    private static void DrawArrow(Vector2 center, Vector2 step, float size, Color color)
    {
        var rect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        Matrix4x4 saved = GUI.matrix;

        GUIUtility.RotateAroundPivot(Angle(step), center);
        GUI.DrawTexture(rect, ArrowShape(), ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
        GUI.matrix = saved;
    }

    // GUI는 y가 아래로 커진다 — 오른쪽이 0도이고 시계 방향으로 돈다.
    private static float Angle(Vector2 step)
    {
        if (step.x > 0f)
        {
            return 0f;
        }

        if (step.x < 0f)
        {
            return 180f;
        }

        if (step.y > 0f)
        {
            return 90f;
        }

        return 270f;
    }

    // 오른쪽을 가리키는 흰 삼각형. 색은 그릴 때 입히므로 모양만 만든다.
    private static Texture2D ArrowShape()
    {
        if (_arrow != null)
        {
            return _arrow;
        }

        const int Size = 16;
        _arrow = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        var clear = new Color(1f, 1f, 1f, 0f);
        var solid = new Color(1f, 1f, 1f, 1f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float half = (Size - x) * 0.5f;
                bool inside = Mathf.Abs(y - (Size - 1) * 0.5f) <= half - 1f;
                _arrow.SetPixel(x, y, inside ? solid : clear);
            }
        }

        _arrow.Apply();
        return _arrow;
    }

    private static Texture2D _arrow;

    private void DrawFailed(Rect area, int cellPixels, Tile spawn, bool lit)
    {
        if (spawn == null)
        {
            return;
        }

        Color mark = lit
            ? MapMakerPalette.Problem
            : MapMakerPalette.Dim(MapMakerPalette.Problem);

        Rect rect = CellRect(area, cellPixels, spawn.Coord.x, spawn.Coord.y);
        Rect inner = Inset(rect, 3f);
        DrawBorder(inner, mark, 2f);
    }

    /// <summary>
    /// 사람이 그린 칸의 순서.
    ///
    /// 화살표는 어느 쪽으로 가는지만 말하고 몇 번째인지는 말하지 못한다 —
    /// 되돌아오는 구간에서는 두 방향이 같은 칸에 겹쳐 어느 쪽이 먼저인지 알 길이 없다.
    /// 번호는 칸 가운데를 피해 구석에 적는다. 가운데는 선과 화살표 자리다.
    /// 같은 칸을 다시 지나가면 번호가 여러 개라 지날 때마다 조금씩 밀어 적는다.
    /// </summary>
    private void DrawNodes(Rect area, int cellPixels, IReadOnlyList<RouteNode> nodes)
    {
        if (nodes == null)
        {
            return;
        }

        if (cellPixels < 16)
        {
            DrawDots(area, cellPixels, nodes);
            return;
        }

        var seen = new Dictionary<Vector2Int, int>();
        float shift = cellPixels * 0.3f;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector2Int coord = nodes[i].Coord;
            seen.TryGetValue(coord, out int again);
            seen[coord] = again + 1;

            Rect cell = CellRect(area, cellPixels, coord.x, coord.y);
            DrawOrder(new Vector2(cell.x + shift * again, cell.y + shift * again), i + 1);
        }
    }

    // 칸이 좁으면 숫자가 칸보다 커진다 — 그때는 그린 자리라는 것만 보라 점으로 남긴다.
    private void DrawDots(Rect area, int cellPixels, IReadOnlyList<RouteNode> nodes)
    {
        float size = Mathf.Max(5f, cellPixels * 0.4f);

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector2Int coord = nodes[i].Coord;
            Rect cell = CellRect(area, cellPixels, coord.x, coord.y);
            EditorGUI.DrawRect(new Rect(cell.x + 1f, cell.y + 1f, size, size), MapMakerPalette.Node);
        }
    }

    /// <summary>
    /// 보라 판에 검은 글자. 어떤 지형색·경로색 위에 얹혀도 같은 대비로 읽히게 판을 깐다.
    /// 판은 칸 왼쪽 위에서 시작한다 — 가운데는 선과 화살표 자리다.
    /// 같은 칸을 다시 지나가면 오른쪽 아래로 한 칸씩 밀어 쌓아, 앞 번호를 덮지 않고 순서대로 읽힌다.
    /// </summary>
    private static void DrawOrder(Vector2 corner, int order)
    {
        string text = order.ToString();
        var plate = new Rect(corner.x + 1f, corner.y + 1f, 7f + text.Length * 5f, 11f);

        EditorGUI.DrawRect(plate, MapMakerPalette.Node);
        GUI.Label(plate, text, OrderStyle());
    }

    private static GUIStyle OrderStyle()
    {
        if (_order != null)
        {
            return _order;
        }

        _order = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            padding = new RectOffset(0, 0, 0, 0)
        };
        _order.normal.textColor = Color.black;
        return _order;
    }

    private static GUIStyle _order;

    private void DrawHover(Rect area, int cellPixels, Vector2Int hover)
    {
        if (!GridCalculator.IsInGrid(hover, Cols, Rows))
        {
            return;
        }

        DrawBorder(CellRect(area, cellPixels, hover.x, hover.y), MapMakerPalette.Mark, 1f);
    }

    // ---- 자리 계산 ----

    private Rect CellRect(Rect area, int cellPixels, int col, int row)
    {
        float x = area.x + Pad + col * cellPixels;
        float y = area.y + Pad + (Rows - 1 - row) * cellPixels; // row 0 = 아래
        return new Rect(x, y, cellPixels - 1, cellPixels - 1);
    }

    private Vector2 CellCenter(Rect area, int cellPixels, Vector2Int coord)
    {
        Rect rect = CellRect(area, cellPixels, coord.x, coord.y);
        return rect.center;
    }

    // 좌표 라벨(짝수만) — 방향 감 잡기용
    private void DrawAxisLabels(Rect area, int cellPixels)
    {
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        for (int col = 0; col < Cols; col += 2)
        {
            var slot = new Rect(area.x + Pad + col * cellPixels, area.y, cellPixels, Pad);
            GUI.Label(slot, col.ToString(), style);
        }

        for (int row = 0; row < Rows; row += 2)
        {
            float y = area.y + Pad + (Rows - 1 - row) * cellPixels;
            GUI.Label(new Rect(area.x, y, Pad, cellPixels), row.ToString(), style);
        }
    }

    private static Rect Inset(Rect rect, float margin)
    {
        return new Rect(rect.x + margin, rect.y + margin,
            rect.width - 2f * margin, rect.height - 2f * margin);
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
