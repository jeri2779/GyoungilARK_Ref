#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 공중·수영 적이 따라갈 경로를 모듈 격자 위에 그리는 창(Tools/Enemy/Enemy Route Maker).
///
/// 맵 메이커와 같은 조작을 쓴다 — 모듈을 고르고, 종류를 고르고, 스폰 칸에서 끌어 그리거나 한 칸씩 찍는다.
/// 격자 그리기와 마우스 히트 테스트는 맵 메이커의 TileGridView를 그대로 재사용한다(읽기만 한다).
/// 공중 보기는 붓을 None으로 두어 모든 칸을 밝게 둔다 — 공중은 고지·벽·물을 다 지나기 때문이다.
///
/// 저작 결과는 EnemyRouteSet 에셋에만 쓴다. 모듈 프리팹·씬은 건드리지 않으므로
/// 맵 담당 영역과 충돌하지 않는다(FlyingPathfinder·SwimPathfinder와 같은 선).
/// </summary>
public class EnemyRouteWindow : EditorWindow
{
    private const int MinCell = 14;
    private const int MaxCell = 72;
    private const float ChromeHeight = 170f;

    private enum Tool
    {
        Select, // 경로 고르기
        Draw,   // 끌어 그리기 — 지나간 칸을 전부 쌓는다
        Point,  // 한 칸씩 찍기 — 사이는 런타임이 자동으로 잇는다
    }

    private Grid[] _modules = System.Array.Empty<Grid>();
    private string[] _moduleNames = System.Array.Empty<string>();
    private int _moduleIndex;

    private EnemyRouteSet _set;
    private EnemyRouteKind _kind = EnemyRouteKind.Air;
    private Tool _tool = Tool.Draw;
    private int _cellPixels = 20;
    private bool _needsFit = true;

    private Vector2 _scroll;
    private Vector2Int _hover = new(-1, -1);

    // 지금 고른 경로(_set.Entries의 번호). -1이면 고른 것이 없다.
    private int _selected = -1;

    // 그리는 중인 한 획. 스폰 칸에서 눌렀을 때만 열린다.
    private bool _drawing;
    private Vector2Int _lastCell = new(int.MinValue, int.MinValue);
    private int _strokeGroup;

    // 이 프레임에 계산해 둔 것 — 격자 그리기와 클릭 처리가 같은 값을 봐야 한다.
    private Dictionary<Vector2Int, Tile> _cells = new();
    private readonly List<LaneData> _preview = new();
    private readonly List<int> _previewEntry = new();      // _preview[i]가 어느 Entry인지
    private readonly List<RouteNode> _nodes = new();
    private readonly List<Vector2Int> _skipped = new();
    private readonly HashSet<Vector2Int> _noOverrides = new();

    [MenuItem("Tools/Enemy/Enemy Route Maker")]
    private static void Open()
    {
        GetWindow<EnemyRouteWindow>("Enemy Route").minSize = new Vector2(460, 340);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnFocus()
    {
        Refresh();
    }

    private void Refresh()
    {
        List<Grid> found = ModuleScan.FindModules();
        _modules = found.ToArray();
        _moduleNames = new string[_modules.Length];
        for (int i = 0; i < _modules.Length; i++)
        {
            _moduleNames[i] = ModuleScan.ModuleName(_modules[i]);
        }

        _moduleIndex = Mathf.Clamp(_moduleIndex, 0, Mathf.Max(0, _modules.Length - 1));
        _needsFit = true;
    }

    private void OnGUI()
    {
        if (_modules.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "이 씬(또는 열어 둔 프리팹)에 모듈(Grid)이 없습니다. 맵이 있는 씬을 열거나 모듈 프리팹을 열어 주세요.",
                MessageType.Info);
            if (GUILayout.Button("다시 찾기")) Refresh();
            return;
        }

        Grid module = _modules[_moduleIndex];

        DrawTopBar(module);

        if (_set == null)
        {
            EditorGUILayout.HelpBox(
                "경로를 담을 EnemyRouteSet 에셋이 필요합니다. 위에서 하나를 꽂거나 '새로 만들기'를 누르세요.",
                MessageType.Info);
            return;
        }

        BuildCells(module);
        BuildPreview(module);

        DrawToolBar();

        var view = new TileGridView(_cells, ViewBrush());
        FitOnce(view);

        Rect area = GridArea(view);
        HandleMouse(view, area, module);
 
        view.Draw(area, _cellPixels, _preview, PreviewIndexOf(_selected), _nodes, _hover, _noOverrides, false);

        DrawStatus();
        DrawEntryList();
    }

    // ---- 위: 대상 ----

    private void DrawTopBar(Grid module)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            int picked = EditorGUILayout.Popup(_moduleIndex, _moduleNames, EditorStyles.toolbarPopup,
                GUILayout.Width(150));
            if (picked != _moduleIndex)
            {
                _moduleIndex = picked;
                _selected = -1;
                _needsFit = true;
            }

            GUILayout.Label(ModuleScan.IsPrefabStage() ? "◆프리팹" : "○씬", EditorStyles.miniLabel,
                GUILayout.Width(52));

            var next = (EnemyRouteSet)EditorGUILayout.ObjectField(_set, typeof(EnemyRouteSet), false,
                GUILayout.Width(160));
            if (next != _set)
            {
                _set = next;
                _selected = -1;
            }

            if (GUILayout.Button("새로 만들기", EditorStyles.toolbarButton, GUILayout.Width(78)))
            {
                Create(module);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                AssetDatabase.SaveAssets();
            }
        }

        // 에셋이 다른 모듈 것이면 좌표계가 달라 엉뚱한 칸에 그려진다 — 미리 알린다.
        if (_set != null && _set.Module != null)
        {
            GameObject source = ModuleScan.SourcePrefab(module);
            if (source != null && _set.Module != source)
            {
                EditorGUILayout.HelpBox(
                    $"이 에셋은 '{_set.Module.name}' 모듈용입니다. 지금 고른 모듈은 '{source.name}'이라 " +
                    "좌표가 어긋날 수 있습니다.", MessageType.Warning);
            }
        }
    }

    private void Create(Grid module)
    {
        // Resources에 두지 않는다 — WaveSpawner의 인스펙터 참조로 닿으므로 경로 로드가 필요 없고,
        // Resources에 넣으면 쓰지 않는 경로 세트까지 빌드에 무조건 들어간다.
        const string folder = "Assets/EnemyRoutes";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "EnemyRoutes");
        }

        GameObject source = ModuleScan.SourcePrefab(module);
        string label = source != null ? source.name : ModuleScan.ModuleName(module);

        var made = CreateInstance<EnemyRouteSet>();
        made.Module = source;

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/EnemyRoutes_{label}.asset");
        AssetDatabase.CreateAsset(made, path);
        AssetDatabase.SaveAssets();

        _set = made;
        _selected = -1;
        EditorGUIUtility.PingObject(made);
    }

    // ---- 도구 줄 ----

    private void DrawToolBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            KindButton(EnemyRouteKind.Air, "공중");
            KindButton(EnemyRouteKind.Swim, "수영");

            GUILayout.Space(10);

            ToolButton(Tool.Select, "선택");
            ToolButton(Tool.Draw, "그리기");
            ToolButton(Tool.Point, "찍기");

            GUILayout.FlexibleSpace();

            GUILayout.Label("칸", EditorStyles.miniLabel, GUILayout.Width(16));
            _cellPixels = (int)GUILayout.HorizontalSlider(_cellPixels, MinCell, MaxCell, GUILayout.Width(80));
            if (GUILayout.Button("맞춤", EditorStyles.toolbarButton, GUILayout.Width(38))) _needsFit = true;
        }
    }

    private void KindButton(EnemyRouteKind kind, string label)
    {
        int count = _set != null ? _set.CountOf(kind) : 0;
        bool on = _kind == kind;
        bool now = GUILayout.Toggle(on, $"{label} {count}", EditorStyles.toolbarButton, GUILayout.Width(64));

        // 이미 켜진 탭을 다시 눌러도 아무 일이 없어야 한다 — 여기서 걸러야 고른 경로가 풀리지 않는다.
        if (on || !now) return;

        _kind = kind;
        _selected = -1;
    }

    private void ToolButton(Tool tool, string label)
    {
        bool on = _tool == tool;
        bool now = GUILayout.Toggle(on, label, EditorStyles.toolbarButton, GUILayout.Width(52));
        if (!on && now) _tool = tool;
    }

    // 공중은 고지·벽·물을 다 지나므로 모든 칸을 밝게 둔다(TileGridView는 None에서 전부 Lit).
    // 수영은 물 칸을 강조해 물길을 짚기 쉽게 한다.
    private MapBrush ViewBrush()
    {
        return _kind == EnemyRouteKind.Swim ? MapBrush.Swim : MapBrush.None;
    }

    // ---- 격자 ----

    private void BuildCells(Grid module)
    {
        List<Tile> tiles = ModuleScan.CollectTiles(module);
        _cells = ModuleScan.MapCells(tiles, out _, out _);

        // 창은 MapBoard.Build를 거치지 않아 이웃 연결이 비어 있다 — A*가 한 칸도 못 나아간다.
        // LaneQuery가 LaneBuilder를 부르기 전에 하는 것과 같은 처리다(직렬화되지 않는 런타임 캐시라 안전).
        TileLink.LinkNeighbors(_cells);
    }

    // 이 종류의 경로를 전부 미리보기 레인으로 만든다. 런타임과 같은 해석기를 써야 그린 대로 나오는지 알 수 있다.
    private void BuildPreview(Grid module)
    {
        _preview.Clear();
        _previewEntry.Clear();
        _nodes.Clear();
        _skipped.Clear();

        for (int i = 0; i < _set.Entries.Count; i++)
        {
            EnemyRouteSet.Entry entry = _set.Entries[i];
            if (entry == null || entry.Kind != _kind) continue;

            List<Tile> path = EnemyRoutePath.BuildTiles(_cells, entry, i == _selected ? _skipped : null);

            // 본진까지 못 이었으면 그린 칸만이라도 보여준다 — 빈 화면보다 "어디까지 그렸는지"가 낫다.
            if (path == null || path.Count == 0) path = DrawnTiles(entry);
            if (path.Count == 0) continue;

            _preview.Add(new LaneData(path[0], path[path.Count - 1], null, path, System.Array.Empty<Tile>()));
            _previewEntry.Add(i);
        }

        if (_selected >= 0 && _selected < _set.Entries.Count)
        {
            EnemyRouteSet.Entry entry = _set.Entries[_selected];
            if (entry != null && entry.Nodes != null)
            {
                for (int i = 0; i < entry.Nodes.Count; i++)
                {
                    _nodes.Add(new RouteNode(entry.Nodes[i].x, entry.Nodes[i].y, 0f));
                }
            }
        }
    }

    // 격자 조회 없이 그린 칸만 타일로. 경로 해석이 실패했을 때의 대체 표시.
    private List<Tile> DrawnTiles(EnemyRouteSet.Entry entry)
    {
        var list = new List<Tile>();
        if (_cells.TryGetValue(entry.Spawn, out Tile start)) list.Add(start);

        for (int i = 0; i < entry.Nodes.Count; i++)
        {
            if (_cells.TryGetValue(entry.Nodes[i], out Tile tile)) list.Add(tile);
        }

        return list;
    }

    private int PreviewIndexOf(int entryIndex)
    {
        for (int i = 0; i < _previewEntry.Count; i++)
        {
            if (_previewEntry[i] == entryIndex) return i;
        }

        return -1;
    }

    private void FitOnce(TileGridView view)
    {
        if (!_needsFit) return;
        _needsFit = false;

        float w = (position.width - TileGridView.Pad - 24) / Mathf.Max(1, view.Cols);
        float h = (position.height - ChromeHeight - TileGridView.Pad) / Mathf.Max(1, view.Rows);
        _cellPixels = Mathf.Clamp((int)Mathf.Min(w, h), MinCell, MaxCell);
    }

    private Rect GridArea(TileGridView view)
    {
        float w = view.PixelWidth(_cellPixels);
        float h = view.PixelHeight(_cellPixels);
        return GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
    }

    // ---- 입력 ----

    private void HandleMouse(TileGridView view, Rect area, Grid module)
    {
        Event e = Event.current;
        Vector2Int coord = view.CoordAt(e.mousePosition, area, _cellPixels);

        if (_hover != coord)
        {
            _hover = coord;
            Repaint();
        }

        if (coord.x < 0 || !_cells.ContainsKey(coord)) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            OnPress(coord);
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0 && _drawing && _tool == Tool.Draw)
        {
            if (coord != _lastCell)
            {
                _lastCell = coord;
                AppendNode(coord);
                Repaint();
            }

            e.Use();
            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0 && _drawing)
        {
            _drawing = false;
            _lastCell = new Vector2Int(int.MinValue, int.MinValue);
            Undo.CollapseUndoOperations(_strokeGroup); // 획 하나를 Ctrl+Z 한 번으로 되돌린다
            EditorUtility.SetDirty(_set);
            e.Use();
        }
    }

    private void OnPress(Vector2Int coord)
    {
        Tile tile = _cells[coord];

        // 스폰 칸을 누르면 그 스폰의 경로를 고른다. 이미 고른 스폰을 다시 누르면 다음 갈래로 넘긴다.
        if (tile.IsEnemySpawn)
        {
            if (_tool == Tool.Select) { CycleAt(coord); return; }
            StartRoute(coord);
            return;
        }

        if (_tool == Tool.Select) return;

        // 경로를 아직 고르지 않았으면 어디에 쌓을지 알 수 없다 — 스폰부터 누르라고 알린다.
        if (_selected < 0)
        {
            ShowNotification(new GUIContent("스폰 칸부터 누르세요"));
            return;
        }

        AppendNode(coord);
        EditorUtility.SetDirty(_set);
    }

    // 이 스폰에 그려 둔 이 종류 경로들을 순환하며 고른다. 없으면 -1로 둔다.
    private void CycleAt(Vector2Int spawn)
    {
        var found = new List<int>();
        for (int i = 0; i < _set.Entries.Count; i++)
        {
            EnemyRouteSet.Entry entry = _set.Entries[i];
            if (entry != null && entry.Kind == _kind && entry.Spawn == spawn) found.Add(i);
        }

        if (found.Count == 0) { _selected = -1; return; }

        int at = found.IndexOf(_selected);
        _selected = found[(at + 1) % found.Count];
    }

    // 이 스폰에서 새 경로를 시작한다. 그리기면 획을 열어 드래그로 이어 받는다.
    private void StartRoute(Vector2Int spawn)
    {
        // 같은 스폰의 빈 항목이 이미 고른 상태면 그걸 다시 쓴다 — 스폰 칸을 여러 번 눌러도 빈 항목이 쌓이지 않게.
        if (!ReuseEmpty(spawn))
        {
            Undo.RecordObject(_set, "Add Enemy Route");

            _set.Entries.Add(new EnemyRouteSet.Entry
            {
                Kind = _kind,
                Spawn = spawn,
                Nodes = new List<Vector2Int>(),
            });

            _selected = _set.Entries.Count - 1;
            EditorUtility.SetDirty(_set);
        }

        if (_tool != Tool.Draw) return;

        // 획을 열기 전에 묶음을 하나 넘긴다 — 안 넘기면 직전 편집이 이 획에 같이 접혀
        // Ctrl+Z 한 번에 무관한 작업까지 사라진다(TileStamp.BeginStroke의 주석과 같은 이유).
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Draw Enemy Route");
        _strokeGroup = Undo.GetCurrentGroup();

        _drawing = true;
        _lastCell = spawn;
    }

    // 이 스폰에 아직 아무것도 안 그린 항목이 있으면 그것을 골라 재사용한다.
    private bool ReuseEmpty(Vector2Int spawn)
    {
        for (int i = 0; i < _set.Entries.Count; i++)
        {
            EnemyRouteSet.Entry entry = _set.Entries[i];
            if (entry == null || entry.Kind != _kind) continue;
            if (entry.Spawn != spawn) continue;
            if (entry.Nodes.Count > 0) continue;

            _selected = i;
            return true;
        }

        return false;
    }

    private void AppendNode(Vector2Int coord)
    {
        if (_selected < 0 || _selected >= _set.Entries.Count) return;

        EnemyRouteSet.Entry entry = _set.Entries[_selected];
        if (entry.Nodes.Count > 0 && entry.Nodes[entry.Nodes.Count - 1] == coord) return;
        if (entry.Nodes.Count == 0 && coord == entry.Spawn) return; // 스폰은 시작점이라 노드로 쌓지 않는다

        Undo.RecordObject(_set, "Draw Enemy Route");
        entry.Nodes.Add(coord);
    }

    // ---- 아래: 상태·목록 ----

    private void DrawStatus()
    {
        string cell = _hover.x >= 0 && _cells.TryGetValue(_hover, out Tile tile)
            ? $"({_hover.x},{_hover.y}) {tile.Terrain}{(tile.State.Pass == PassType.Swim ? " · 물" : string.Empty)}" +
              $"{(tile.IsEnemySpawn ? " · 스폰" : string.Empty)}"
            : "—";

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(cell, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{KindWord(_kind)} 경로 {_preview.Count}", EditorStyles.miniLabel);
        }

        if (_skipped.Count > 0)
        {
            EditorGUILayout.HelpBox(
                $"고른 경로에 {KindWord(_kind)}으로 지날 수 없는 칸이 {_skipped.Count}개 있습니다 " +
                $"(첫 칸 {_skipped[0]}). 런타임에서는 그 칸을 건너뛰고 이어 붙습니다.",
                MessageType.Warning);
        }
    }

    private void DrawEntryList()
    {
        using (var scope = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scope.scrollPosition;

            for (int i = 0; i < _set.Entries.Count; i++)
            {
                EnemyRouteSet.Entry entry = _set.Entries[i];
                if (entry == null || entry.Kind != _kind) continue;

                using (new EditorGUILayout.HorizontalScope(i == _selected
                    ? EditorStyles.helpBox
                    : GUIStyle.none))
                {
                    if (GUILayout.Button($"스폰 ({entry.Spawn.x},{entry.Spawn.y}) · {entry.Nodes.Count}칸",
                        EditorStyles.miniButton, GUILayout.Width(160)))
                    {
                        _selected = i;
                    }

                    if (GUILayout.Button("뒤로", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        Undo.RecordObject(_set, "Undo Enemy Route Node");
                        if (entry.Nodes.Count > 0) entry.Nodes.RemoveAt(entry.Nodes.Count - 1);
                        EditorUtility.SetDirty(_set);
                    }

                    if (GUILayout.Button("비우기", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        Undo.RecordObject(_set, "Clear Enemy Route");
                        entry.Nodes.Clear();
                        EditorUtility.SetDirty(_set);
                    }

                    if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        Undo.RecordObject(_set, "Delete Enemy Route");
                        _set.Entries.RemoveAt(i);
                        if (_selected == i) _selected = -1;
                        else if (_selected > i) _selected--;
                        EditorUtility.SetDirty(_set);
                        break;
                    }
                }
            }
        }
    }

    private static string KindWord(EnemyRouteKind kind)
    {
        return kind == EnemyRouteKind.Air ? "공중" : "수영";
    }
}
#endif
