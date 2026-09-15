using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맵 메이커 창의 화면 배치와 붓질 입력을 맡는다.
///
/// 유니티 에디터와 같은 짜임이다 — 위는 대상 선택, 왼쪽은 지형(큰 분류), 가운데는 격자,
/// 오른쪽은 배치 허용(작은 분류)과 집계, 아래는 지금 가리키는 칸의 상태.
/// 창이 좁아 셋을 나란히 못 놓으면 오른쪽 판만 격자 아래로 접는다(격자를 자르거나 줄이지 않는다).
///
/// 붓을 고르는 것이 곧 보기 모드를 고르는 것이다 — 그 붓과 상관없는 칸은 격자에서 죽는다.
/// None으로 두면 아무것도 기록하지 않는 읽기 전용이 된다.
///
/// 지형을 칠해도 Col/Row는 변하지 않는다(좌표는 TilePosBaker가 월드 위치에서 뽑는다).
/// 그래서 칠한 뒤 재베이크가 필요 없고, 매 리페인트마다 경로를 다시 계산해도 부담이 없다.
/// EnemyLane은 저작하지 않는다(런타임에 MapBoard.SetLanes가 경로에서 파생시키는 값).
/// </summary>
public class MapMakerWindow : EditorWindow
{
    private const float MinLeftWidth = 90f;
    private const float MaxLeftWidth = 260f;
    private float _leftWidth = 104f;
    private const int RightWidth = 128;
    private const int MinCell = 14;
    private const int MaxCell = 72;

    /// <summary>격자 위아래로 도구 줄·예고줄·상태줄이 늘 차지하는 높이 — 창에 맞출 때 이만큼 빼고 잰다.</summary>
    private const float ChromeHeight = 150f;

    // 손잡이로 끄는 선반 높이의 아래 한계. 위 한계는 창 높이가 그때그때 달라 MaxShelfNow()가 잰다.
    private const float MinShelf = 60f;

    /// <summary>선반의 탭. 셋 다 저작 중 계속 보고 싶은 것이라 접이식으로 자리를 나눠 쓴다.</summary>
    private enum ShelfTab
    {
        Prefab,
        Lane,
        Problem,
        Wave,
        Hero
    }

    private Grid[] _modules = System.Array.Empty<Grid>();
    private string[] _moduleNames = System.Array.Empty<string>();
    private int _moduleIndex;
    private Vector2 _scroll;

    private MapBrush _brush = MapBrush.None;
    private int _cellPixels = 20;
    private bool _showInert;
    // 창이 열리고 처음으로 칸 수를 알게 된 프레임에만 자동으로 맞춤을 적용한다.
    private bool _needsFit = true;

    private ShelfTab _shelf = ShelfTab.Prefab;
    private bool _shelfOpen = true;
    private float _shelfHeight = 168f;
    private bool _shelfDragging;
    private bool _leftWidthDragging;
    private Vector2 _terrainScroll;
    private Vector2 _placeScroll;
    private Vector2 _shelfScroll;
    private MapTool _tool = MapTool.Select;
    // 지금 창에서 보고 고치는 일차. 0 = 공통.
    private int _day;
    private List<TileTheme> _themes;
    private TileTheme _theme;
    private GameObject _pick;
    private GameObject _newPrefab;
    // 적 탭에서 훑어볼 지역·라운드. 모듈은 자기가 어느 지역인지 모르므로 직접 고른다.
    private int _waveRegion = 1;
    private int _waveRound = 1;
    // 고른 웨이브 행. Entry가 아니라 WaveTable.Data로 들고 있어야 다음 프레임에도 선택이 유지된다.
    private WaveTable.Data _waveChosen;
    // 아군 탭 데이터. HeroData·StatDataSO는 프로젝트 전역 값이라 모듈이 바뀌어도 다시 모을 필요가 없다.
    private List<HeroReadout.Group> _heroGroups;
    private HeroData _heroChosen;
    private readonly Dictionary<MapBrush, GameObject> _lastPicks = new();
    private int _themeModule = -1;
    private readonly HashSet<Vector2Int> _swapped = new();
    private int _strokeGroup;
    private Vector2Int _hover = new(-1, -1);
    // 이 붓질에서 방금 처리한 칸. 같은 칸 안에서 마우스가 흔들리기만 해도 드래그 이벤트가 계속 들어와
    // 매번 다시 찍고 매번 레인을 재계산하던 것을 막는다.
    private Vector2Int _strokeCell = new(int.MinValue, int.MinValue);
    // Shift+클릭 구간의 기준 칸 — 드래그로 지나간 칸이 아니라 마지막으로 "누른" 칸을 기억한다.
    private Vector2Int? _rangeAnchor;
    private string _targetSeen = string.Empty;
    // 편집 중인 레인의 신원. 좌표가 아니라 지정 항목 참조라 같은 스폰에 레인이 여러 개여도 갈린다.
    private RouteData _routeData;
    private int _routeModule = -1;
    private int _routeDay;
    private RouteMode _routeMode = RouteMode.Draw;

    // 그리는 중인 한 획. 스폰 칸에서 눌렀을 때만 열리고, 손을 뗄 때까지 지나간 칸이 쌓인다.
    private bool _routeDrawing;
    private Vector2Int _routeLast;
    private readonly List<Vector2Int> _stroke = new();

    // 이 프레임에 계산해 둔 레인. 클릭 처리와 예고줄이 둘 다 필요로 하는데
    // 인자로 흘려보내면 손대지 않는 메서드까지 서명이 길어져 여기 둔다(OnGUI 안에서만 쓴다).
    private IReadOnlyList<LaneData> _lanes = System.Array.Empty<LaneData>();

    // 이번에 그린 격자의 칸 수. "맞춤"이 도구 줄에서 계산할 때 필요한데 그 줄은 격자보다 먼저 그려진다.
    private int _cols = 1;
    private int _rows = 1;

    [MenuItem("Tools/Map/Map Maker")]
    private static void Open()
    {
        GetWindow<MapMakerWindow>("Map Maker").minSize = new Vector2(420, 320);
    }

    private void OnEnable()
    {
        wantsMouseMove = true; // 아래 상태줄이 마우스를 따라가려면 이동 이벤트가 필요하다

        // Ctrl+Z는 데이터를 되돌리지만 이 창은 그 사실을 모른다 —
        // 다시 그리라고 알려주지 않으면 값은 돌아갔는데 화면은 그대로여서 "안 되돌아간다"로 보인다.
        Undo.undoRedoPerformed += Repaint;
        Refresh();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
    }

    // 편집 대상이 바뀌면(씬 전환·프리팹 열기) 모듈 목록을 다시 잡는다.
    private void OnFocus()
    {
        Refresh();
    }

    private void Refresh()
    {
        _themes = null;    // 편집 대상이 바뀌었다 — 테마도 다시 모으고 모듈에 맞춰 다시 고른다
        _themeModule = -1;
        _rangeAnchor = null; // 대상이 바뀌면 기준 칸도 남의 모듈 좌표가 된다
        _heroGroups = null; // 새로고침을 누르면 새로 추가한 HeroData·StatDataSO도 다시 읽는다

        List<Grid> found = ModuleScan.FindModules();
        _modules = found.ToArray();
        _moduleNames = new string[_modules.Length];

        for (int i = 0; i < _modules.Length; i++)
        {
            _moduleNames[i] = ModuleScan.ModuleName(_modules[i]);
        }

        if (_moduleIndex >= _modules.Length)
        {
            _moduleIndex = 0;
        }
    }

    private void OnGUI()
    {
        // 편집 대상이 바뀌면(프리팹 열기·씬 전환) 창을 누르지 않아도 목록을 다시 잡는다.
        // 상단 표시는 매번 새로 읽는데 모듈 목록은 안 읽으면, 프리팹을 가리키면서 씬 모듈을 그리게 된다 —
        // 프리팹인 줄 알고 씬 인스턴스를 칠하는 바로 그 사고다.
        string target = TargetLabel();
        if (target != _targetSeen)
        {
            _targetSeen = target;
            Refresh();
        }

        DrawToolbar(target);

        if (_modules.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "편집 대상에 Grid(모듈)가 없습니다. 씬을 열거나 모듈 프리팹을 연 뒤 새로고침을 누르세요.",
                MessageType.Info);
            return;
        }

        Grid module = _modules[_moduleIndex];
        if (module == null)
        {
            Refresh();
            return;
        }

        EnsureThemes(module);
        EnsureHeroGroups();
        SyncRoute();
        DrawToolRow();
        SyncPick();

        List<Tile> tiles = ModuleScan.CollectTiles(module);
        Dictionary<Vector2Int, Tile> cells = ModuleScan.MapCells(tiles, out _, out _);
        CampfireData campfireData = BuildCampfire(module, cells);
        HashSet<Vector2Int> windwallShape = BuildWindwallShape(module, cells);

        if (cells.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "이 모듈에 Tile 컴포넌트를 가진 타일이 없습니다. 큐브에 Tile을 붙인 뒤 " +
                "Tools/Map/Bake Tile Positions (Active Scene)로 좌표를 새기세요.",
                MessageType.Warning);
            return;
        }

        var view = new TileGridView(cells, _brush);
        _cols = view.Cols;
        _rows = view.Rows;

        if (_needsFit)
        {
            _cellPixels = FitCell();
            _needsFit = false;
        }

        // 저작 경로를 그대로 반영해 그린다. 그리기 전에 사전을 맞춰야 되돌리기 직후에도 선이 진짜를 말한다.
        RouteConfig routes = RouteEdit.Find(module);
        RouteEdit.Sync(routes);
        DrawDayTabs(routes);
        RouteEdit.SetDay(routes, _day);
        List<LaneData> lanes = LaneQuery.BuildLanes(cells, routes);
        _lanes = lanes;

        // 씬 오버라이드는 씬 모드에서만 뜻이 있다 — 프리팹 스테이지는 비교할 프리팹이 없다.
        HashSet<Vector2Int> overrides = null;
        if (!ModuleScan.IsPrefabStage())
        {
            overrides = TileOverride.Collect(cells);
        }

        // 지금 칸 크기로 곁판 둘 + 격자가 한 줄에 안 들어가면 칸 크기만 줄인다 — 곁판 자리는 절대 안 바꾼다.
        float gridWidth = view.PixelWidth(_cellPixels);
        if (position.width < _leftWidth + gridWidth + RightWidth + 28)
        {
            _cellPixels = FitCell();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTerrainPanel(module, cells);
            DrawLeftGrip();
            DrawGridArea(view, cells, lanes, overrides, campfireData, windwallShape);
            DrawPlacePanel(cells);
        }

        List<string> problems = TileAuthorRule.FindProblems(cells, lanes, tiles.Count, _day);
        SpawnWaveReadout.Groups waveGroups = SpawnWaveReadout.Collect(_waveRegion, _waveRound);

        int overrideCount = overrides?.Count ?? 0;
        DrawActionPreview(module, cells);
        DrawMarkerLegend(overrideCount);
        DrawBrushNote();
        DrawShelf(module, lanes, routes, problems, waveGroups);
        DrawStatusBar(cells, view, lanes, problems.Count, overrideCount);
    }

    /// <summary>
    /// 판 구성이 바뀌었으니 이 프레임은 접고 처음부터 다시 그린다.
    ///
    /// IMGUI는 요소 자리를 Layout 이벤트에서 한 번 재서 목록으로 두고, 뒤따르는 이벤트(클릭·이동)가
    /// 그 목록을 순서대로 꺼내 쓴다. 그래서 클릭 도중에 줄이 하나 붙거나 떨어지면 이후 요소가 남의 자리를 집는다 —
    /// 폭이 엉뚱하게 늘어나고, 스크롤 판 자리에 일반 줄이 걸리면 형변환 오류로 그리기가 끊긴다.
    /// </summary>
    private void Relayout()
    {
        Repaint();
        GUIUtility.ExitGUI();
    }

    // ---- 위: 대상 ----

    private void DrawToolbar(string target)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            // 붓질 한 번 = 되돌리기 한 단계. 키보드 초점이 어디 있든 눌리도록 버튼으로도 둔다.
            // 둘 다 격자·모듈 목록을 바꾼다 — 바뀐 것으로 다시 재려면 이 프레임을 접어야 한다.
            if (GUILayout.Button("되돌리기", EditorStyles.toolbarButton, GUILayout.Width(56)))
            {
                Undo.PerformUndo();
                Relayout();
            }

            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(56)))
            {
                Refresh();
                Relayout();
            }

            if (_modules.Length > 0)
            {
                int picked = EditorGUILayout.Popup(
                    _moduleIndex, _moduleNames, EditorStyles.toolbarPopup, GUILayout.Width(140));

                if (picked != _moduleIndex)
                {
                    _moduleIndex = picked;
                    _rangeAnchor = null; // 다른 모듈의 좌표는 이 모듈에서 뜻이 다르다
                    Relayout();
                }
            }

            GUILayout.Label(target, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            GUILayout.Label($"칸 {_cellPixels}px", EditorStyles.miniLabel, GUILayout.Width(52));
            int cell = (int)GUILayout.HorizontalSlider(_cellPixels, MinCell, MaxCell, GUILayout.Width(120));

            // 슬라이더를 끝까지 밀어도 창이 좁으면 격자가 잘린다 — 창에 맞는 값을 직접 계산해 준다.
            if (GUILayout.Button("맞춤", EditorStyles.toolbarButton, GUILayout.Width(38)))
            {
                cell = FitCell();
            }

            if (cell != _cellPixels)
            {
                _cellPixels = cell;
                Relayout(); // 격자 폭이 바뀌면 곁판이 격자 옆에서 아래로 옮겨 앉는다
            }

            // 붓과 무관하게 무효 조합 칸(지상의 CanRanged 등)을 격자에 상시 표시한다.
            bool inert = GUILayout.Toggle(
                _showInert, "무효", EditorStyles.toolbarButton, GUILayout.Width(40));

            if (inert != _showInert)
            {
                _showInert = inert;
                Relayout(); // 표식 뜻풀이 줄이 생기거나 사라진다
            }
        }
    }

    // 일차 탭. 이미 쓰인 날짜만 버튼으로 늘어놓고, 공통은 항상 맨 앞이다.
    // 아직 탭이 없는 날짜는 오른쪽 숫자칸에 직접 입력해서 간다.
    private void DrawDayTabs(RouteConfig routes)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            DayButton("공통", 0);

            if (routes != null)
            {
                IReadOnlyList<int> usedDays = routes.UsedDays;
                for (int i = 0; i < usedDays.Count; i++)
                {
                    DayButton($"{usedDays[i]}일차", usedDays[i]);
                }
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("일차", EditorStyles.miniLabel, GUILayout.Width(28));
            int typed = EditorGUILayout.IntField(_day, EditorStyles.toolbarTextField, GUILayout.Width(30));

            if (typed != _day)
            {
                _day = Mathf.Max(typed, 0);
                Relayout(); // 날짜가 바뀌면 격자·경로·문제가 전부 그 날짜 것으로 바뀐다
            }
        }
    }

    private void DayButton(string label, int day)
    {
        bool pressed = GUILayout.Toggle(_day == day, label, EditorStyles.toolbarButton, GUILayout.Width(52));

        if (pressed && _day != day)
        {
            _day = day;
            Relayout();
        }
    }

    // 도구 줄. 팔레트(무엇을)와 도구(어떻게)를 나눠, 같은 클릭이 상황마다 다른 뜻이 되지 않게 한다.
    private void DrawToolRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            ToolButton(MapTool.Select);
            ToolButton(MapTool.Paint);
            ToolButton(MapTool.Swap);
            ToolButton(MapTool.Erase);
            ToolButton(MapTool.Pick);
            ToolButton(MapTool.Route);
            GUILayout.FlexibleSpace();

            if (_tool == MapTool.Route)
            {
                _routeMode = (RouteMode)GUILayout.Toolbar((int)_routeMode, RouteModes,
                    EditorStyles.miniButton, GUILayout.Width(104));
                GUILayout.Label(RouteTarget(), EditorStyles.miniLabel);
            }
        }
    }

    private static readonly string[] RouteModes = { "그리기", "점 찍기" };

    // 격자가 창에 통째로 들어오는 가장 큰 칸 크기. 곁판 두 장과 위아래 줄이 차지하는 자리를 빼고 잰다.
    private int FitCell()
    {
        float wide = position.width - _leftWidth - RightWidth - TileGridView.Pad - 34f;
        float tall = position.height - ChromeHeight - ShelfSpace() - TileGridView.Pad;

        int byWidth = Mathf.FloorToInt(wide / _cols);
        int byHeight = Mathf.FloorToInt(tall / _rows);

        return Mathf.Clamp(Mathf.Min(byWidth, byHeight), MinCell, MaxCell);
    }

    // 아래 선반이 먹는 높이. 접어 두면 제목 줄만 남는다.
    private float ShelfSpace()
    {
        if (_shelfOpen)
        {
            return _shelfHeight + 24f;
        }

        return 24f;
    }

    // 지금 창 높이에서 선반이 가질 수 있는 최대 높이. 창을 키우면 선반도 더 커질 수 있다.
    private float MaxShelfNow()
    {
        return Mathf.Max(MinShelf, position.height - ChromeHeight);
    }

    // 지금 어느 스폰의 경로를 고치는 중인가. 대상 없이 찍으면 아무 일도 안 일어나므로 늘 띄운다.
    private string RouteTarget()
    {
        if (HasSelectedRoute())
        {
            return $"편집 중: 스폰 ({SelectedSpawn().x}, {SelectedSpawn().y})";
        }

        return "스폰 칸을 먼저 클릭하세요";
    }

    // 편집 대상 레인이 정해져 있는가.
    private bool HasSelectedRoute()
    {
        return _routeData != null;
    }

    // 편집 대상 레인이 출발하는 스폰 좌표.
    private Vector2Int SelectedSpawn()
    {
        return _routeData.Spawn;
    }

    // 이 스폰의 지정 항목. 아직 없으면 빈 항목을 만들어 신원을 확보한다.
    private RouteData EnsureRouteOfSpawn(Tile spawn)
    {
        RouteConfig config = RouteEdit.Ensure(_modules[_moduleIndex]);

        if (config.TryGetOwnRoute(spawn.Coord, out List<RouteData> found))
        {
            return found[0];
        }

        return RouteEdit.AddRoute(config, spawn.Coord, _day);
    }

    // 편집 중인 경로의 목록 번호. 저작 API가 번호로 집는다.
    private int SelectedRouteIndex(RouteConfig config)
    {
        return config.IndexOf(_routeData);
    }

    // 편집 대상 레인이 아직 정해지지 않았는가.
    private bool IsRouteUnselected()
    {
        return _routeData == null;
    }

    // 모듈을 갈아타거나 날짜를 바꾸면 편집 중이던 경로를 놓는다 —
    // 남의 모듈 좌표나 다른 날짜의 경로를 그대로 붙잡고 고치지 않는다.
    private void SyncRoute()
    {
        if (IsRouteContextCurrent())
        {
            return;
        }

        _routeModule = _moduleIndex;
        _routeDay = _day;
        _routeData = null;
        _routeDrawing = false;
        _stroke.Clear();
    }

    // 지금 고른 경로가 지금 모듈·날짜 것과 같은가.
    private bool IsRouteContextCurrent()
    {
        if (_routeModule != _moduleIndex)
        {
            return false;
        }

        return _routeDay == _day;
    }

    private void ToolButton(MapTool tool)
    {
        bool pressed = GUILayout.Toggle(
            _tool == tool, MapToolWord.Name(tool), EditorStyles.miniButton, GUILayout.Width(58));

        if (pressed)
        {
            _tool = tool;
        }
    }

    // ---- 테마 ----

    private void EnsureThemes(Grid module)
    {
        if (_themes == null)
        {
            _themes = ThemeIO.LoadAll();
        }

        SyncTheme(module);
    }

    // 모듈을 갈아타면 그 모듈이 기본으로 쓰는 테마로 옮긴다. 가리키는 테마가 없으면 지금 것을 그대로 둔다.
    private void SyncTheme(Grid module)
    {
        if (_themeModule == _moduleIndex && _theme != null)
        {
            return;
        }

        _themeModule = _moduleIndex;
        TileTheme best = ThemeIO.Best(_themes, module);

        if (best != null)
        {
            _theme = best;
            _pick = null;
            return;
        }

        if (_theme == null && _themes.Count > 0)
        {
            _theme = _themes[0];
        }
    }

    // 고른 프리팹이 지금 붓·테마의 목록에 없으면(붓을 바꿨거나 딴 테마로 옮겼다) 이 붓에서
    // 마지막으로 골랐던 프리팹으로 되돌린다. 그마저 없으면 첫 번째로 돌린다.
    private void SyncPick()
    {
        if (_theme == null)
        {
            _pick = null;
            return;
        }

        if (_pick != null && _theme.Has(_brush, _pick))
        {
            _lastPicks[_brush] = _pick;
            return;
        }

        _pick = RecallPick(_brush);
    }

    private GameObject RecallPick(MapBrush brush)
    {
        GameObject last;
        if (_lastPicks.TryGetValue(brush, out last) && _theme.Has(brush, last))
        {
            return last;
        }

        return _theme.First(brush);
    }

    private void Extract(Grid module)
    {
        TileTheme made = ThemeIO.Extract(module, _moduleNames[_moduleIndex]);

        _themes = null;
        _theme = made;
        _themeModule = _moduleIndex;
        _pick = null;

        Selection.activeObject = made;
        EditorGUIUtility.PingObject(made);
        Relayout(); // 테마 탭이 하나 늘었다
    }

    /// <summary>
    /// 지금 무엇을 고치고 있는지. 모듈 이름은 프리팹이든 씬 인스턴스든 똑같이 MapModule_A로 나오므로,
    /// 대상 종류를 같이 띄우지 않으면 프리팹인 줄 알고 씬 인스턴스를 칠하는 사고가 난다
    /// (그 경우 오버라이드로만 남아 같은 프리팹을 쓰는 다른 씬에는 영영 반영되지 않는다).
    /// </summary>
    private static string TargetLabel()
    {
        if (ModuleScan.IsPrefabStage())
        {
            return $"◆ 프리팹 «{ModuleScan.TargetName()}» — 원본에 기록";
        }

        return $"○ 씬 «{ModuleScan.TargetName()}» — 오버라이드로 남음";
    }

    // ---- 아군 ----

    // 아군 유닛 데이터를 한 번만 모아 둔다 — HeroData·StatDataSO는 프로젝트 전역 값이라 모듈이 바뀌어도 다시 모을 필요가 없다.
    private void EnsureHeroGroups()
    {
        if (_heroGroups == null)
        {
            _heroGroups = HeroReadout.Collect();
        }
    }

    // ---- 왼쪽: 지형(큰 분류) ----

    private void DrawTerrainPanel(Grid module, Dictionary<Vector2Int, Tile> cells)
    {
        float gridHeight = TileGridView.Pad + _rows * _cellPixels + 6f;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(_leftWidth)))
        using (var scroll = new EditorGUILayout.ScrollViewScope(_terrainScroll, GUILayout.Height(gridHeight)))
        {
            _terrainScroll = scroll.scrollPosition;
            GUILayout.Label("지형", EditorStyles.miniBoldLabel);
            TerrainRow(cells, MapBrush.Ground, "지상", TerrainType.Ground);
            TerrainRow(cells, MapBrush.High, "고지", TerrainType.High);
            TerrainRow(cells, MapBrush.Special, "외곽", TerrainType.Special);
            TerrainRow(cells, MapBrush.Core, "본진", TerrainType.Core);
            TerrainRow(cells, MapBrush.Empty, "빈(벽)", TerrainType.Empty);

            GUILayout.Space(6);
            GUILayout.Label("겉모습", EditorStyles.miniBoldLabel);

            // 장식은 칸 수가 아니라 얹힌 개수를 센다 — 한 칸에 여러 개가 올라갈 수 있다.
            BrushRow(MapBrush.Decor, "장식", MapMakerPalette.Decor, DecorPlace.Total(module));

            GUILayout.Space(6);
            GUILayout.Label("통행", EditorStyles.miniBoldLabel);
            BrushRow(MapBrush.Swim, "물", MapMakerPalette.Swim, TileTally.CountPass(cells, PassType.Swim));

            GUILayout.Space(6);
            GUILayout.Label("기믹", EditorStyles.miniBoldLabel);
            BrushRow(MapBrush.Fire, "불", MapMakerPalette.Fire, TileTally.CountGimmick(cells, GimmickType.Fire));
            BrushRow(MapBrush.Campfire, "모닥불", MapMakerPalette.Campfire, TileTally.CountGimmick(cells, GimmickType.Campfire));
            DrawCampfireRange(module);
            BrushRow(MapBrush.Windwall, "가림막", MapMakerPalette.Windwall, TileTally.CountGimmick(cells, GimmickType.Windwall));

            GUILayout.Space(6);
            GUILayout.Label("표식", EditorStyles.miniBoldLabel);
            BrushRow(MapBrush.Spawn, "스폰", MapMakerPalette.Spawn, TileTally.CountSpawn(cells));

            GUILayout.Space(6);
            BrushRow(MapBrush.None, "읽기만", MapMakerPalette.Panel, cells.Count);

            GUILayout.Space(6);
            GUILayout.Label("Alt+클릭 = 끄기", EditorStyles.miniLabel);
        }
    }

    private void TerrainRow(Dictionary<Vector2Int, Tile> cells, MapBrush brush, string label, TerrainType terrain)
    {
        BrushRow(brush, label, MapMakerPalette.Terrain(terrain), TileTally.CountTerrain(cells, terrain));
    }

    // 왼쪽 패널 너비 손잡이. 좌우로 끌면 패널 너비가 늘거나 줄어 격자와 자리를 나눠 갖는다.
    private void DrawLeftGrip()
    {
        const float band = 9f;
        Rect handle = GUILayoutUtility.GetRect(band, 0f, GUILayout.ExpandHeight(true));
        var bar = new Rect(handle.x + (band - 5f) / 2f, handle.y, 5f, handle.height);
        EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.35f));
        EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);

        Event input = Event.current;

        if (input.type == EventType.MouseDown && handle.Contains(input.mousePosition))
        {
            _leftWidthDragging = true;
            input.Use();
        }

        if (input.type == EventType.MouseUp)
        {
            _leftWidthDragging = false;
        }

        if (_leftWidthDragging && input.type == EventType.MouseDrag)
        {
            _leftWidth = Mathf.Clamp(_leftWidth + input.delta.x, MinLeftWidth, MaxLeftWidth);
            input.Use();
            Relayout();
        }
    }

    // ---- 오른쪽: 배치 허용(작은 분류)과 집계 ----

    private void DrawPlacePanel(Dictionary<Vector2Int, Tile> cells)
    {
        float gridHeight = TileGridView.Pad + _rows * _cellPixels + 6f;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(RightWidth)))
        using (var scroll = new EditorGUILayout.ScrollViewScope(_placeScroll, GUILayout.Height(gridHeight)))
        {
            _placeScroll = scroll.scrollPosition;
            GUILayout.Label("배치", EditorStyles.miniBoldLabel);
            BrushRow(MapBrush.Melee, "근접", MapMakerPalette.Mark, TileTally.CountFlag(cells, MapBrush.Melee));
            BrushRow(MapBrush.Ranged, "원거리", MapMakerPalette.Mark, TileTally.CountFlag(cells, MapBrush.Ranged));
            BrushRow(MapBrush.Build, "생산", MapMakerPalette.Mark, TileTally.CountFlag(cells, MapBrush.Build));

            GUILayout.Space(6);
            GUILayout.Label("집계", EditorStyles.miniBoldLabel);
            FlagBar(cells, MapBrush.Melee, "근접");
            FlagBar(cells, MapBrush.Ranged, "원거리");
            FlagBar(cells, MapBrush.Build, "생산");

            GUILayout.Space(6);
            GUILayout.Label("■ 켜짐   □ 켜졌지만\n     지금은 효과 없음", EditorStyles.miniLabel);
            GUILayout.Label("켜진 붓을 다시 누르면 꺼집니다", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("붓 고른 뒤 타일 클릭 = 켜기/끄기 뒤집기", EditorStyles.wordWrappedMiniLabel);
        }
    }

    /// <summary>
    /// 이 허용이 실제로 효력을 갖는 칸 수 대비 얼마나 찍혔는지.
    /// 분모는 지형이 정한다 — 근접·생산은 지상 칸이, 원거리는 고지 칸이 모집단이다.
    /// </summary>
    private void FlagBar(Dictionary<Vector2Int, Tile> cells, MapBrush brush, string label)
    {
        int inert = TileTally.CountInert(cells, brush);
        int working = TileTally.CountFlag(cells, brush) - inert;
        int field = TileTally.CountField(cells, brush);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{working} / {field}", EditorStyles.miniLabel);
        }

        Color bar = MapMakerPalette.Dim(MapMakerPalette.Mark);
        if (_brush == brush)
        {
            bar = MapMakerPalette.Mark;
        }

        Rect track = GUILayoutUtility.GetRect(10f, 3f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(track, MapMakerPalette.Panel);
        float ratio = Mathf.Clamp01(working / Mathf.Max(1f, field));
        EditorGUI.DrawRect(new Rect(track.x, track.y, track.width * ratio, track.height), bar);

        if (inert > 0)
        {
            GUILayout.Label($"↑ 효과 없는 {inert}칸", EditorStyles.miniLabel);
        }
    }

    // 색 조각 + 토글 + 개수 한 줄. GUI.backgroundColor로 버튼을 물들이지 않는다 —
    // 어두운 스킨의 버튼 텍스처에 곱해져 색끼리 다 비슷하게 탁해진다.
    private void BrushRow(MapBrush brush, string label, Color swatch, int count)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            Rect chip = GUILayoutUtility.GetRect(11f, 15f, GUILayout.Width(11));
            EditorGUI.DrawRect(new Rect(chip.x, chip.y + 2f, 11f, 11f), swatch);

            bool active = _brush == brush;
            bool pressed = GUILayout.Toggle(active, label, EditorStyles.miniButton, GUILayout.MinWidth(36));
            GUILayout.Label(count.ToString(), EditorStyles.miniLabel, GUILayout.Width(24));

            if (pressed != active)
            {
                PickBrush(brush, pressed);
            }
        }
    }

    // 켜진 붓을 다시 누르면 아무 붓도 안 든 상태로 돌아간다 —
    // 끄는 길이 없으면 한번 고른 사람은 다른 붓으로 갈아타는 것 말고는 빠져나올 수 없다.
    // 붓을 고르는 순간 칠하기 모드로도 같이 넘어간다 — 붓만 고르고 격자를 눌러도 안 칠해지는 혼란을 없앤다.
    private void PickBrush(MapBrush brush, bool on)
    {
        _brush = MapBrush.None;

        if (on)
        {
            _brush = brush;
            _tool = MapTool.Paint;
        }

        Relayout(); // 붓에 따라 설명 줄이 붙거나 떨어진다
    }

    // ---- 가운데: 격자 ----

    private void DrawGridArea(TileGridView view, Dictionary<Vector2Int, Tile> cells,
        IReadOnlyList<LaneData> lanes,
        HashSet<Vector2Int> overrides,
        CampfireData campfireData,
        HashSet<Vector2Int> windwallShape)
    {
        // 프레임을 접을 때 짝이 맞게 풀리도록 스크롤 판을 scope로 연다 — 입력 처리가 이 안에서 프레임을 접는다.
        using (new EditorGUILayout.VerticalScope())
        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            Rect area = GUILayoutUtility.GetRect(view.PixelWidth(_cellPixels), view.PixelHeight(_cellPixels));
            HandleHover(area, view);
            HandleStroke(area, view, cells);
            view.Draw(area, _cellPixels, lanes, RouteIndex(lanes), RouteNodes(),
                _hover, overrides, _showInert, campfireData, windwallShape);
        }
    }

    // 현재 모듈의 실제 IceZone 값으로 에디터 보호 영역을 계산합니다.
    private static CampfireData BuildCampfire(
        Grid module,
        IReadOnlyDictionary<Vector2Int, Tile> cells)
    {
        IceZone iceZone = module.GetComponentInParent<IceZone>(true);
        if (iceZone == null)
        {
            return null;
        }

        return new CampfireCalc().BuildData(cells, iceZone.CampfireRange);
    }

    // 현재 모듈의 실제 DesertZone 값(가림막 사거리)으로 에디터 미리보기용 가림막 범위를 계산합니다.
    // 저작 창은 "오늘 바람"이 없으므로 네 방향 팔을 전부 계산해 둔다 — 어느 방향이든 확인할 수 있게.
    //
    // 화면 표시 전용으로 원점 칸(가림막 자신)을 팔 칸들과 하나로 합친다 — 실제 판정(WindwallData)은
    // 원점을 비보호로 그대로 두되(§4.4, 자기 자신은 안 막힘), 윤곽선은 원점까지 이어진 한 덩어리로 그려야
    // 원점 따로·팔 따로 닫힌 상자가 겹쳐 보이는 문제가 없다.
    private static HashSet<Vector2Int> BuildWindwallShape(
        Grid module,
        IReadOnlyDictionary<Vector2Int, Tile> cells)
    {
        DesertZone desertZone = module.GetComponentInParent<DesertZone>(true);
        if (desertZone == null)
        {
            return null;
        }

        WindwallData data = new WindwallCalc().BuildData(cells, desertZone.WindwallReach);
        var shape = new HashSet<Vector2Int>();

        foreach (KeyValuePair<Vector2Int, Tile> entry in cells)
        {
            if (entry.Value.IsHigh && entry.Value.IsWindwall)
            {
                shape.Add(entry.Key);
            }

            for (int index = 0; index < GridCalculator.Directions.Length; index++)
            {
                if (data.HasArm(GridCalculator.Directions[index], entry.Key))
                {
                    shape.Add(entry.Key);
                    break;
                }
            }
        }

        return shape;
    }

    // 별도 에디터 값 없이 IceZone의 실제 직렬화 범위를 편집합니다.
    private static void DrawCampfireRange(Grid module)
    {
        IceZone iceZone = module.GetComponentInParent<IceZone>(true);
        if (iceZone == null)
        {
            return;
        }

        int range = EditorGUILayout.IntField("보호 범위", iceZone.CampfireRange);
        range = Mathf.Max(0, range);
        if (range == iceZone.CampfireRange)
        {
            return;
        }

        Undo.RecordObject(iceZone, "Change Campfire Range");
        iceZone.SetRange(range);
        EditorUtility.SetDirty(iceZone);
    }

    // 지금 고른 경로의 번호. 고른 것이 없거나 그 레인이 사라졌으면 -1이다.
    private int RouteIndex(IReadOnlyList<LaneData> lanes)
    {
        if (HasSelectedRoute())
        {
            return IndexOfRoute(lanes);
        }

        return -1;
    }

    // 편집 중인 지정 항목을 들고 있는 레인의 번호. 좌표가 아니라 참조로 맞춘다.
    private int IndexOfRoute(IReadOnlyList<LaneData> lanes)
    {
        for (int index = 0; index < lanes.Count; index++)
        {
            if (lanes[index].Route == _routeData)
            {
                return index;
            }
        }

        return -1;
    }

    // 고른 경로에 사람이 찍어 둔 경유 칸. 저작이 없으면 null(자동 최단 경로다).
    private IReadOnlyList<RouteNode> RouteNodes()
    {
        if (HasSelectedRoute())
        {
            return _routeData.Nodes;   // 신원을 들고 있으므로 좌표로 다시 조회하지 않는다
        }

        return null;
    }

    // ---- 아래: 이 클릭이 할 일 ----

    /// <summary>
    /// 가리키는 칸을 클릭하면 무엇이 되는지 미리 알린다.
    /// 스왑이 켜지면 같은 클릭이 쌓기·걷어내기·교체로 갈리므로, 누르기 전에 보이지 않으면 눌러 보고서야 안다.
    /// </summary>
    private void DrawActionPreview(Grid module, Dictionary<Vector2Int, Tile> cells)
    {
        if (!cells.TryGetValue(_hover, out Tile tile))
        {
            return; // 가리키는 칸이 없으면 자리를 차지하지 않는다
        }

        bool turnOff = Event.current.alt;

        string action = ActionWord(module, tile, turnOff);
        string stack = TileActionPreview.DescribeStack(module, _hover, tile);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label($"{MapToolWord.Name(_tool)} ▶", EditorStyles.miniBoldLabel, GUILayout.Width(52));
            GUILayout.Label(action, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"지금 {stack}", EditorStyles.miniLabel);
        }
    }

    // 경로 도구는 타일을 바꾸지 않는다 — 이 칸이 무엇이 되는지가 아니라
    // 경유 순서의 어디에 들어가는지를 말해야 하므로 TileActionPreview에 맡기지 않는다.
    private string ActionWord(Grid module, Tile tile, bool turnOff)
    {
        if (_tool == MapTool.Route)
        {
            return RouteWord(tile, turnOff);
        }

        return TileActionPreview.Describe(module, _hover, tile, _tool, _brush, _pick, turnOff);
    }

    private string RouteWord(Tile tile, bool back)
    {
        if (tile.IsEnemySpawn)
        {
            return SpawnWord();
        }

        if (IsRouteUnselected())
        {
            return "스폰 칸을 먼저 눌러 어느 경로를 고칠지 정하세요";
        }

        if (_routeMode == RouteMode.Draw)
        {
            return "스폰에서 누른 채 끌면 지나간 칸이 그대로 경로가 됩니다 — 벽에 닿으면 선이 거기서 멈춥니다";
        }

        return PointWord(tile, back);
    }

    private string SpawnWord()
    {
        if (_routeMode == RouteMode.Draw)
        {
            return "여기서 누른 채 끌면 이 경로를 처음부터 다시 그립니다 (누르기만 하면 그대로 둡니다)";
        }

        return "이 스폰의 경로를 편집 대상으로 삼습니다";
    }

    // 점 찍기의 세 갈래. 누르기 전에 무엇이 될지 보이지 않으면 눌러 보고서야 알게 된다.
    private string PointWord(Tile tile, bool back)
    {
        IReadOnlyList<RouteNode> nodes = RouteNodes();
        int count = Count(nodes);

        if (back)
        {
            if (count == 0)
            {
                return "뺄 칸이 없습니다";
            }

            return $"맨 뒤 {count}번째 칸을 뺍니다 ({count} → {count - 1}개)";
        }

        if (!tile.Walkable)
        {
            return "지나갈 수 없는 칸입니다 — 여기는 못 찍습니다";
        }

        if (Event.current.control)
        {
            return SlotWord(nodes, RouteSlotOf(nodes, tile.Coord));
        }

        return $"맨 뒤에 쌓습니다 ({count} → {count + 1}개)  ·  Alt=뒤로  Ctrl=자리 자동";
    }

    // 넣을 자리를 미리 말한다. 자리를 눌러 보고서야 알게 되면 순서를 고칠 때마다 지웠다 다시 찍게 된다.
    private static string SlotWord(IReadOnlyList<RouteNode> nodes, int slot)
    {
        int count = Count(nodes);

        if (count == 0)
        {
            return "첫 칸으로 넣습니다 (0 → 1개)";
        }

        if (slot == 0)
        {
            return $"1번 칸 앞에 넣습니다 ({count} → {count + 1}개)";
        }

        if (slot >= count)
        {
            return $"맨 뒤에 넣습니다 ({count} → {count + 1}개)";
        }

        return $"{slot}번과 {slot + 1}번 칸 사이에 넣습니다 ({count} → {count + 1}개)";
    }

    private static int Count(IReadOnlyList<RouteNode> nodes)
    {
        if (nodes == null)
        {
            return 0;
        }

        return nodes.Count;
    }

    // 이 칸이 들어갈 자리. 도착점을 모르는 막힌 경로는 끼울 구간이 없으므로 맨 뒤에 붙인다.
    private int RouteSlotOf(IReadOnlyList<RouteNode> nodes, Vector2Int coord)
    {
        Tile goal = RouteGoal();
        if (goal == null)
        {
            return Count(nodes);
        }

        return RouteSlot.Best(nodes, SelectedSpawn(), goal.Coord, coord);
    }

    // 지금 고른 경로의 도착 칸. 경로가 막혔으면 null.
    private Tile RouteGoal()
    {
        int index = RouteIndex(_lanes);
        if (index < 0)
        {
            return null;
        }

        return _lanes[index].Goal;
    }

    // ---- 아래: 지금 가리키는 칸 ----

    private void DrawStatusBar(
        Dictionary<Vector2Int, Tile> cells,
        TileGridView view,
        IReadOnlyList<LaneData> lanes,
        int problemCount, int overrideCount)
    {
        string reading = "칸 위에 마우스를 올리면 그 칸의 상태가 여기 나옵니다";
        bool hovering = cells.TryGetValue(_hover, out Tile tile);
        if (hovering)
        {
            reading = TileCellReadout.Describe(tile);
        }

        string pathText = LaneText(lanes);

        // 오버라이드는 씬 모드에서만 센다 — 있으면 프리팹과 다른 칸이 몇인지 늘 띄운다(6-3식 사고 조기 발견).
        string overrideText = string.Empty;
        if (overrideCount > 0)
        {
            overrideText = $" · 오버라이드 {overrideCount}칸";
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label(reading, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"{view.Cols}×{view.Rows} · 칸 {cells.Count} · 경로 {pathText} · 문제 {problemCount}{overrideText}",
                EditorStyles.miniLabel);
        }
    }

    private static string LaneText(IReadOnlyList<LaneData> lanes)
    {
        int valid = 0;
        int tiles = 0;

        for (int i = 0; i < lanes.Count; i++)
        {
            if (!lanes[i].IsValid)
            {
                continue;
            }

            valid++;
            tiles += lanes[i].Tiles.Count;
        }

        if (lanes.Count == 0)
        {
            return "없음";
        }

        return $"{valid}/{lanes.Count}개 · 총 {tiles}칸";
    }

    // 격자에 켜져 있는 상시 표식의 뜻을 한 줄로 알린다. 표식이 없으면 자리를 차지하지 않는다.
    private void DrawMarkerLegend(int overrideCount)
    {
        bool showOverride = overrideCount > 0;
        if (!showOverride && !_showInert)
        {
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (showOverride)
            {
                LegendChip(MapMakerPalette.Override, "프리팹과 다름");
            }

            if (_showInert)
            {
                LegendChip(MapMakerPalette.Inert, "무효 조합");
            }

            GUILayout.FlexibleSpace();
        }
    }

    private void LegendChip(Color color, string label)
    {
        Rect chip = GUILayoutUtility.GetRect(11f, 13f, GUILayout.Width(11));
        EditorGUI.DrawRect(new Rect(chip.x, chip.y + 2f, 9f, 9f), color);
        GUILayout.Label(label, EditorStyles.miniLabel);
    }

    // 겉보기와 뜻이 다른 붓 둘. 고른 동안만 띄운다.
    private void DrawBrushNote()
    {
        if (_brush == MapBrush.Empty)
        {
            EditorGUILayout.HelpBox(
                "Empty는 '빈 칸'이 아니라 벽입니다. 통행은 Ground와 Core만 가능하므로 High와 똑같이 길을 막습니다.",
                MessageType.Info);
        }

        if (_brush == MapBrush.Core)
        {
            EditorGUILayout.HelpBox(
                "Core는 색이 아니라 경로의 도착점입니다. 여럿 찍으면 가장 가까운 곳이 목표가 되고, 배치는 전부 막힙니다.",
                MessageType.Info);
        }

        if (_brush == MapBrush.Decor)
        {
            EditorGUILayout.HelpBox(
                "장식은 규칙 데이터가 아닙니다 — 경로·배치·격자에 잡히지 않는 겉모습이라 교체 도구로만 얹히고, " +
                "지우기는 타일보다 장식을 먼저 걷습니다.",
                MessageType.Info);
        }
    }

    // ---- 입력 ----

    private void HandleHover(Rect area, TileGridView view)
    {
        Event input = Event.current;

        // 창을 벗어나면 가리키던 칸을 놓는다 — 안 놓으면 상태줄이 지난 칸을 계속 말한다.
        if (input.type == EventType.MouseLeaveWindow)
        {
            _hover = new Vector2Int(-1, -1);
            Relayout(); // 예고줄이 사라진다
            return;
        }

        // Alt는 예고 문구를 끄기 쪽으로 뒤집는다 — 마우스를 안 움직여도 눌린 순간 다시 그려야 한다.
        if (input.type == EventType.KeyDown || input.type == EventType.KeyUp)
        {
            Repaint();
            return;
        }

        if (input.type != EventType.MouseMove)
        {
            return;
        }

        Vector2Int coord = view.CoordAt(input.mousePosition, area, _cellPixels);
        if (coord == _hover)
        {
            return;
        }

        _hover = coord;
        Relayout(); // 격자 안팎을 넘나들면 예고줄이 생기거나 사라진다
    }

    // 누른 순간 되돌리기 그룹을 열고, 끄는 동안 지나간 칸을 찍고, 뗄 때 한 단계로 접는다.
    private void HandleStroke(Rect area, TileGridView view, Dictionary<Vector2Int, Tile> cells)
    {
        if (_shelfDragging)
        {
            return; // 선반 손잡이를 끄는 중이면 마우스가 격자 안으로 들어와도 칠하지 않는다.
        }

        if (MapToolWord.Writes(_tool) && _brush == MapBrush.None && _tool != MapTool.Erase)
        {
            return; // 칠할 것을 안 골랐다 — 지우기는 팔레트가 필요 없어 예외다
        }

        Event input = Event.current;
        if (input.button != 0)
        {
            return;
        }

        // 격자 밖 클릭·드래그는 여기서 처리할 일이 아니다 — 먼저 삼키면(Use) 이벤트 타입이 Used로
        // 바뀌어서 이 뒤에 그려지는 선반 손잡이·오른쪽 배치 버튼이 같은 클릭을 영영 못 받는다.
        bool inside = area.Contains(input.mousePosition);
        if ((input.type == EventType.MouseDown || input.type == EventType.MouseDrag) && !inside)
        {
            return;
        }

        // 이벤트를 먼저 삼킨다 — 찍은 뒤에는 프레임을 접으므로 이 줄로 돌아오지 않는다.
        if (input.type == EventType.MouseDown)
        {
            input.Use();
            Vector2Int coord = view.CoordAt(input.mousePosition, area, _cellPixels);

            // Shift+클릭 = 경로 도구가 아닌 한, 기준 칸부터 지금 칸까지 직사각형 전부에 한 번에 적용한다.
            if (input.shift && _tool != MapTool.Route && _rangeAnchor.HasValue)
            {
                StampRange(_rangeAnchor.Value, coord, cells, input.alt);
                _rangeAnchor = coord;
                return;
            }

            _swapped.Clear(); // 새 붓질 — 이번에 교체한 칸 기록을 비운다
            _strokeCell = new Vector2Int(int.MinValue, int.MinValue); // 새 붓질 — 마지막 칸 기록을 비운다
            _strokeGroup = TileStamp.BeginStroke();
            // 클릭 한 번(끌지 않음)은 지금 상태를 뒤집는다 — 켜진 칸을 누르면 바로 꺼진다.
            StampAt(input.mousePosition, area, view, cells, input.alt, true);
            _rangeAnchor = coord; // 다음 Shift+클릭이 여기부터 구간을 잡도록 기준으로 남긴다
            return;
        }

        if (input.type == EventType.MouseDrag)
        {
            input.Use();

            // 끌기는 지나간 칸 전부를 한 방향으로 맞춘다 — 칸마다 뒤집으면 여러 칸을 같은 값으로
            // 칠하려 할 때 이미 켜진 칸만 꺼져버려 얼룩덜룩해진다.
            if (_tool != MapTool.Route || _routeMode == RouteMode.Draw)
            {
                StampAt(input.mousePosition, area, view, cells, input.alt, false);
            }

            return;
        }

        if (input.type == EventType.MouseUp)
        {
            _routeDrawing = false; // 획이 끝났다 — 다음 획은 스폰부터 다시 시작해야 한다
            TileStamp.EndStroke(_strokeGroup);
            input.Use();
        }
    }

    private void StampAt(Vector2 mouse, Rect area, TileGridView view, Dictionary<Vector2Int, Tile> cells,
        bool turnOff, bool toggle)
    {
        Vector2Int coord = view.CoordAt(mouse, area, _cellPixels);
        bool exists = cells.TryGetValue(coord, out Tile tile);
        if (!exists)
        {
            return; // 격자 밖이거나 타일이 없는 칸 — 아직 없는 칸을 새로 만들지는 않는다
        }

        if (coord == _strokeCell)
        {
            return; // 방금 처리한 칸과 같다 — 마우스가 이 칸 안에서만 흔들린 것이다
        }

        _strokeCell = coord;
        ApplyToCell(coord, tile, cells, turnOff, toggle);
        Relayout(); // 경로가 바로 다시 계산돼 보이도록. 찍으면서 예고줄·붓이 바뀌므로 프레임을 접는다
    }

    // Shift+클릭 구간. 기준 칸과 지금 칸을 맞모서리로 삼은 직사각형 전부에 지금 도구를 한 번에 적용한다.
    // 한 붓질로 묶어야 되돌리기가 한 번에 통째로 풀린다.
    private void StampRange(Vector2Int from, Vector2Int to, Dictionary<Vector2Int, Tile> cells, bool turnOff)
    {
        _swapped.Clear();
        _strokeCell = new Vector2Int(int.MinValue, int.MinValue);
        int group = TileStamp.BeginStroke();

        int minX = Mathf.Min(from.x, to.x);
        int maxX = Mathf.Max(from.x, to.x);
        int minY = Mathf.Min(from.y, to.y);
        int maxY = Mathf.Max(from.y, to.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var coord = new Vector2Int(x, y);
                if (cells.TryGetValue(coord, out Tile tile))
                {
                    // 구간 채우기도 끌기와 같은 이유로 한 방향으로 맞춘다(칸별 뒤집기 아님).
                    ApplyToCell(coord, tile, cells, turnOff, false);
                }
            }
        }

        TileStamp.EndStroke(group);
        Relayout();
    }

    // 칸 하나에 지금 도구를 적용한다. 한 칸 클릭과 구간 적용이 이 한 곳을 같이 쓴다.
    // toggle=true(단일 클릭)면 지금 켜진 붓 값을 그대로 뒤집는다 — Alt를 누르면 여전히 무조건 끈다.
    private void ApplyToCell(Vector2Int coord, Tile tile, Dictionary<Vector2Int, Tile> cells, bool turnOff, bool toggle)
    {
        _hover = coord;

        switch (_tool)
        {
            case MapTool.Select:
                Selection.activeGameObject = tile.gameObject;
                EditorGUIUtility.PingObject(tile.gameObject);
                break;

            case MapTool.Pick:
                _brush = BrushOf(tile.Terrain);
                break;

            case MapTool.Erase:
                Wipe(coord, cells);
                break;

            case MapTool.Swap:
                TrySwap(coord, cells);
                break;

            case MapTool.Route:
                StampRoute(coord, tile, cells, turnOff);
                break;

            default:
                if (_brush == MapBrush.Decor)
                {
                    break; // 장식은 기록할 지형 값이 없다 — 예고줄이 교체로 얹으라고 말한다
                }

                bool on = !turnOff;
                if (toggle && !turnOff)
                {
                    on = !TileFlagQuery.IsOn(tile, _brush); // 지형 붓은 항상 꺼짐으로 읽혀 그대로 켜진다
                }

                bool hadCampfire = tile.State.Gimmick == GimmickType.Campfire;
                bool hadWindwall = tile.State.Gimmick == GimmickType.Windwall;
                TileStamp.Stamp(tile, _brush, on);

                if (_brush == MapBrush.Campfire)
                {
                    ApplyCampfireDecor(tile, on, hadCampfire);
                }

                if (_brush == MapBrush.Windwall)
                {
                    ApplyWindwallDecor(tile, on, hadWindwall);
                }

                break;
        }
    }

    // 모닥불 기믹을 찍고 끌 때 FireTorch_CampFire 장식도 같이 얹고 걷는다 — 데이터와 겉모습이 갈리지 않게.
    // 걷을 땐 그 칸 제일 위 장식 하나만 지운다 — 같은 칸에 다른 장식을 더 얹었다면 그게 지워질 수 있다.
    private void ApplyCampfireDecor(Tile tile, bool on, bool hadCampfire)
    {
        if (_theme == null || _theme.CampfirePrefab == null)
        {
            return;
        }

        Grid module = _modules[_moduleIndex];

        if (on && !hadCampfire)
        {
            DecorPlace.Add(module, tile, _theme.CampfirePrefab);
            return;
        }

        if (!on && hadCampfire)
        {
            DecorPlace.Remove(module, tile);
        }
    }

    // 가림막 기믹을 찍고 끌 때 WIndWall 장식도 같이 얹고 걷는다 — 데이터와 겉모습이 갈리지 않게.
    // 걷을 땐 그 칸 제일 위 장식 하나만 지운다 — 같은 칸에 다른 장식을 더 얹었다면 그게 지워질 수 있다.
    private void ApplyWindwallDecor(Tile tile, bool on, bool hadWindwall)
    {
        if (_theme == null || _theme.WindwallPrefab == null)
        {
            return;
        }

        Grid module = _modules[_moduleIndex];

        if (on && !hadWindwall)
        {
            DecorPlace.Add(module, tile, _theme.WindwallPrefab);
            return;
        }

        if (!on && hadWindwall)
        {
            DecorPlace.Remove(module, tile);
        }
    }

    // 제일 위 한 겹을 지운다. 한 붓질에 같은 칸을 두 번 지우지 않는다 —
    // 드래그로 지나가기만 해도 겹이 우수수 사라지면 되돌리기 전엔 알아채기 어렵다.
    private void Wipe(Vector2Int coord, Dictionary<Vector2Int, Tile> cells)
    {
        if (!_swapped.Add(coord))
        {
            return;
        }

        Grid module = _modules[_moduleIndex];

        // 장식이 얹혀 있으면 그것이 제일 위다 — 타일보다 먼저 걷는다.
        if (cells.TryGetValue(coord, out Tile on) && DecorPlace.Remove(module, on))
        {
            SceneView.RepaintAll();
            return;
        }

        Tile left = TileSwap.Erase(module, coord);
        if (left != null)
        {
            cells[coord] = left;
        }
        else
        {
            cells.Remove(coord); // 마지막 겹까지 지웠다 — 그 칸은 이제 타일이 없다
        }

        SceneView.RepaintAll();
    }

    // 스폰 칸은 편집 대상으로 삼고, 그 밖의 칸은 고른 방식대로 그리거나 하나씩 쌓는다.
    private void StampRoute(Vector2Int coord, Tile tile, Dictionary<Vector2Int, Tile> cells, bool back)
    {
        if (tile.IsEnemySpawn)
        {
            OpenStroke(tile);
            return;
        }

        if (IsRouteUnselected())
        {
            return; // 어느 스폰의 경로인지 정해지지 않았다 — 도구 줄이 스폰을 먼저 찍으라고 말한다
        }

        RouteConfig config = RouteEdit.Ensure(_modules[_moduleIndex]);
        if (config == null)
        {
            Debug.LogWarning("[Map Maker] 이 모듈에 MapBoard가 없어 경로를 저장할 자리가 없습니다.",
                _modules[_moduleIndex]);
            return;
        }

        if (_routeMode == RouteMode.Draw)
        {
            DrawStroke(config, cells, coord);
            return;
        }

        PointNode(config, tile, back);
    }

    // 스폰 칸을 눌렀다. 획은 아직 비어 있어서, 끌지 않고 떼면 만들어 둔 경로는 그대로 남는다.
    private void OpenStroke(Tile spawn)
    {
        _routeData = SpawnRoute(spawn);
        _routeLast = spawn.Coord;
        _routeDrawing = true;
        _stroke.Clear();
    }

    // 이 스폰에 그릴 갈래. 고른 갈래가 이 스폰 것이면 그것, 아니면 첫 갈래.
    private RouteData SpawnRoute(Tile spawn)
    {
        if (IsChosenSpawn(spawn))
        {
            return _routeData;
        }

        return EnsureRouteOfSpawn(spawn);
    }

    // 지금 고른 갈래가 이 스폰에서 나가는가.
    private bool IsChosenSpawn(Tile spawn)
    {
        if (IsRouteUnselected())
        {
            return false;
        }

        return _routeData.Spawn == spawn.Coord;
    }

    // 끌고 지나간 칸을 쌓아 이 스폰의 경로를 통째로 다시 쓴다. 같은 칸을 다시 지나가도 그대로 쌓인다.
    private void DrawStroke(RouteConfig config, Dictionary<Vector2Int, Tile> cells, Vector2Int coord)
    {
        if (!_routeDrawing)
        {
            return; // 스폰에서 시작하지 않은 획 — 도구 줄이 스폰부터 누르라고 말한다
        }

        List<Vector2Int> steps = RouteStroke.Between(_routeLast, coord);

        for (int i = 0; i < steps.Count; i++)
        {
            if (!Walkable(cells, steps[i]))
            {
                break; // 벽에 닿았다 — 선은 여기까지다
            }

            _routeLast = steps[i];
            Push(steps[i]);
        }

        RouteEdit.SetNodes(config, SelectedRouteIndex(config), _stroke);
    }

    // 이 칸으로 선이 들어갈 수 있는가. 격자 밖도 못 지나가는 칸과 같이 본다.
    private static bool Walkable(Dictionary<Vector2Int, Tile> cells, Vector2Int coord)
    {
        return cells.TryGetValue(coord, out Tile tile) && tile.Walkable;
    }

    // 방금 지나온 칸으로 되짚어 가면 쌓지 않고 그 걸음을 무른다(연필로 그은 선을 되짚어 지우는 것과 같다).
    private void Push(Vector2Int coord)
    {
        if (Retreat(coord))
        {
            _stroke.RemoveAt(_stroke.Count - 1);
            return;
        }

        _stroke.Add(coord);
    }

    // 이 칸이 바로 직전에 지나온 자리인가. 손이 밀려 한 칸 물러난 것과 일부러 돌아오는 것을 여기서 가른다 —
    // 일부러 도는 길은 한 칸이 아니라 여러 칸을 지나 되돌아오므로 이 판단에 걸리지 않는다.
    private bool Retreat(Vector2Int coord)
    {
        int last = _stroke.Count - 1;
        if (last < 0)
        {
            return false;
        }

        if (last == 0)
        {
            return coord == SelectedSpawn();
        }

        return _stroke[last - 1] == coord;
    }

    // 한 칸씩 쌓는다. Alt는 뒤로가기라 맨 뒤부터 빠지고, Ctrl은 들어갈 자리를 알아서 고른다.
    private void PointNode(RouteConfig config, Tile tile, bool back)
    {
        if (back)
        {
            RouteEdit.PopNode(config, SelectedRouteIndex(config));
            return;
        }

        if (!tile.Walkable)
        {
            return; // 못 지나가는 칸 — 넣으면 그 경로가 통째로 끊긴다
        }

        if (!Event.current.control)
        {
            RouteEdit.AddNode(config, SelectedRouteIndex(config), tile.Coord);
            return;
        }

        IReadOnlyList<RouteNode> nodes = RouteNodes();
        RouteEdit.InsertNode(config, SelectedRouteIndex(config), tile.Coord, RouteSlotOf(nodes, tile.Coord));
    }

    // 붓 이름. 왼쪽 판의 줄 이름과 같은 말을 쓴다.
    private static string BrushWord(MapBrush brush)
    {
        switch (brush)
        {
            case MapBrush.Ground: return "지상";
            case MapBrush.High: return "고지";
            case MapBrush.Special: return "외곽";
            case MapBrush.Core: return "본진";
            case MapBrush.Empty: return "빈(벽)";
            case MapBrush.Decor: return "장식";
            case MapBrush.Spawn: return "스폰";
            case MapBrush.Melee: return "근접";
            case MapBrush.Ranged: return "원거리";
            case MapBrush.Build: return "생산";
            case MapBrush.Swim: return "헤엄";
            case MapBrush.Fire: return "불";
            case MapBrush.Campfire: return "모닥불";
            case MapBrush.Windwall: return "가림막";
            default: return "읽기만";
        }
    }

    // 스포이드가 집어 온 지형에 맞는 팔레트.
    private static MapBrush BrushOf(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.High: return MapBrush.High;
            case TerrainType.Core: return MapBrush.Core;
            case TerrainType.Special: return MapBrush.Special;
            case TerrainType.Empty: return MapBrush.Empty;
            default: return MapBrush.Ground;
        }
    }

    // 이 칸의 실물을 고른 프리팹으로 갈아끼운다(장식은 위에 얹는다).
    // 한 붓질에 같은 칸을 두 번 교체하지 않는다 — 드래그 이벤트마다 갈면 GameObject가 우수수 생겼다 사라진다.
    private void TrySwap(Vector2Int coord, Dictionary<Vector2Int, Tile> cells)
    {
        if (!TileActionPreview.Swaps(_brush))
        {
            // 스폰·배치 허용은 같은 큐브 위의 데이터라 바꿀 실물이 없다.
            Debug.LogWarning("[Map Maker] 교체는 지형·장식 팔레트에만 씁니다 — " +
                "스폰·배치 허용은 칠하기로 바꾸세요.");
            return;
        }

        // 고른 프리팹이 없으면 아무것도 하지 않는다. 데이터만 칠하고 넘어가면 겉과 속이 갈라진다.
        if (_pick == null)
        {
            Debug.LogWarning("[Map Maker] 고른 프리팹이 없습니다 — 아래 선반의 프리팹 탭에서 하나 고르세요. " +
                "테마가 비어 있으면 '이 모듈에서 뽑기'로 채울 수 있습니다.", _theme);
            return;
        }

        if (!_swapped.Add(coord))
        {
            return; // 이번 붓질에 이미 손댄 칸 — 다시 만들지 않고 삼킨다
        }

        Grid module = _modules[_moduleIndex];

        if (_brush == MapBrush.Decor)
        {
            cells.TryGetValue(coord, out Tile under);
            DecorPlace.Add(module, under, _pick);
        }
        else
        {
            Tile made = TileSwap.Apply(module, coord, _pick, TileActionPreview.TerrainOf(_brush));
            if (made != null)
            {
                cells[coord] = made; // 붓질 내내 dict를 최신으로 — 뒤 이벤트가 파괴된 타일을 잡지 않도록
            }
        }

        _hover = coord;
        Repaint();

        // 실물이 바뀌었으니 씬 뷰에도 알린다 — 이 창만 다시 그리면
        // 오브젝트는 교체됐는데 화면은 그대로여서 "반영이 안 된다"로 보인다.
        SceneView.RepaintAll();
    }


    // ---- 아래: 선반 ----

    /// <summary>
    /// 창 아래 선반. 프리팹·경로·문제를 탭으로 나눠 남는 세로 공간에 놓는다.
    ///
    /// 격자는 어떤 경우에도 밀지 않는다 — 내용이 넘치면 선반 안에서만 스크롤한다.
    /// 탭 이름에 수를 붙인다: 탭을 열지 않아도 상태가 보이고, 문제가 0인 것과 아직 안 본 것이 구분된다.
    /// </summary>
    private void DrawShelf(Grid module, IReadOnlyList<LaneData> lanes, RouteConfig routes,
        List<string> problems, SpawnWaveReadout.Groups waveGroups)
    {
        int picks = _theme != null ? _theme.For(_brush).Length : 0;

        if (_shelfOpen)
        {
            DrawGrip();
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            ShelfButton(ShelfTab.Prefab, $"프리팹 {picks}");
            ShelfButton(ShelfTab.Lane, $"경로 {ValidLanes(lanes)}/{lanes.Count}");
            ShelfButton(ShelfTab.Problem, $"문제 {problems.Count}");
            ShelfButton(ShelfTab.Wave, $"적 {waveGroups.Total}종");
            ShelfButton(ShelfTab.Hero, $"아군 {HeroTotal()}종");

            GUILayout.FlexibleSpace();
            bool open = GUILayout.Toggle(
                _shelfOpen, _shelfOpen ? "접기" : "펼치기", EditorStyles.toolbarButton, GUILayout.Width(48));

            if (open != _shelfOpen)
            {
                _shelfOpen = open;
                Relayout(); // 선반 속이 통째로 생기거나 사라진다
            }
        }

        if (!_shelfOpen)
        {
            return;
        }

        using (var view = new EditorGUILayout.ScrollViewScope(_shelfScroll, GUILayout.Height(_shelfHeight)))
        {
            _shelfScroll = view.scrollPosition;

            switch (_shelf)
            {
                case ShelfTab.Lane:
                    _routeData = LaneList.Draw(lanes, _routeData, routes);
                    break;

                case ShelfTab.Problem:
                    DrawProblems(problems);
                    break;

                case ShelfTab.Wave:
                    DrawWaveShelf(waveGroups);
                    break;

                case ShelfTab.Hero:
                    _heroChosen = HeroList.DrawCards(_heroGroups, _heroChosen);
                    break;

                default:
                    DrawPrefabShelf(module);
                    break;
            }
        }

        // 상세는 스크롤 밖에 고정한다 — 인스펙터처럼, 카드 목록을 아무리 내려도 자리를 지켜야 한다.
        // 스크롤 안에 같이 두면 카드 몇 장만 있어도 상세를 보려고 끝까지 내려야 하는 문제가 생긴다.
        if (_shelf == ShelfTab.Wave)
        {
            SpawnWaveList.DrawDetail(waveGroups, _waveChosen);
        }

        if (_shelf == ShelfTab.Hero)
        {
            HeroList.DrawDetail(_heroGroups, _heroChosen);
        }
    }

    // 적 탭. 지역·라운드를 직접 고른다 — 모듈은 자기가 어느 지역인지 모른다(WaveSpawner 인스펙터에만 있다).
    private void DrawWaveShelf(SpawnWaveReadout.Groups groups)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("지역", GUILayout.Width(28));
            _waveRegion = Mathf.Max(1, EditorGUILayout.IntField(_waveRegion, GUILayout.Width(30)));
            GUILayout.Label("라운드", GUILayout.Width(40));
            _waveRound = Mathf.Max(1, EditorGUILayout.IntField(_waveRound, GUILayout.Width(30)));
            GUILayout.FlexibleSpace();
        }

        _waveChosen = SpawnWaveList.DrawCards(groups, _waveChosen);
    }

    // 선반 위 손잡이. 위아래로 끌면 선반 높이가 늘거나 줄어 격자와 자리를 나눠 갖는다.
    // 잡는 자리(9px)를 보이는 줄(5px)보다 넉넉히 둔다 — 줄 두께 그대로 잡는 판정은 마우스가 1px만 벗어나도 놓친다.
    private void DrawGrip()
    {
        const float band = 9f;
        Rect handle = GUILayoutUtility.GetRect(0f, band, GUILayout.ExpandWidth(true));
        var bar = new Rect(handle.x, handle.y + (band - 5f) / 2f, handle.width, 5f);
        EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.35f));
        EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeVertical);

        Event input = Event.current;

        if (input.type == EventType.MouseDown && handle.Contains(input.mousePosition))
        {
            _shelfDragging = true;
            input.Use();
        }

        if (input.type == EventType.MouseUp)
        {
            _shelfDragging = false;
        }

        if (_shelfDragging && input.type == EventType.MouseDrag)
        {
            _shelfHeight = Mathf.Clamp(_shelfHeight - input.delta.y, MinShelf, MaxShelfNow());
            _cellPixels = FitCell();
            input.Use();
            Relayout();
        }
    }

    private void ShelfButton(ShelfTab tab, string label)
    {
        bool pressed = GUILayout.Toggle(
            _shelf == tab, label, EditorStyles.toolbarButton, GUILayout.Width(80));

        if (pressed && _shelf != tab)
        {
            _shelf = tab;
            _shelfScroll = Vector2.zero;
            Relayout(); // 탭마다 속이 다르다
        }
    }

    // 프리팹 탭. 테마 탭을 여기 둔다 — 테마는 "무슨 프리팹을 쓸 수 있나"만 정하므로 프리팹과 붙어 있어야 한다.
    private void DrawPrefabShelf(Grid module)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (_themes.Count > 0)
            {
                GUILayout.Label("테마", EditorStyles.miniBoldLabel, GUILayout.Width(28));

                TileTheme picked = ThemeBar.DrawTabs(_themes, _theme);
                if (picked != _theme)
                {
                    _theme = picked;
                    Relayout(); // 테마가 가진 프리팹 수에 따라 썸네일 줄 수가 바뀐다
                }
            }
            else
            {
                GUILayout.Label("테마가 없습니다 — 이 모듈이 쓰는 프리팹으로 뽑을 수 있습니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("이 모듈에서 뽑기", EditorStyles.miniButton, GUILayout.Width(96)))
            {
                Extract(module);
            }

            if (_theme != null
                && GUILayout.Button("에셋", EditorStyles.miniButton, GUILayout.Width(34)))
            {
                Selection.activeObject = _theme;
                EditorGUIUtility.PingObject(_theme);
            }
        }

        if (!TileActionPreview.Swaps(_brush))
        {
            // 실물이 걸리지 않는 붓(스폰·배치 허용·읽기만)에는 고를 프리팹이 없다.
            GUILayout.Label($"{BrushWord(_brush)} 붓은 데이터만 바꿉니다 — 고를 프리팹이 없습니다.",
                EditorStyles.wordWrappedMiniLabel);
            return;
        }

        _pick = ThemeBar.DrawPicks(_theme, _brush, _pick, position.width - 24f);

        if (_theme != null)
        {
            DrawNewPrefabDrop();
        }
    }

    // 뽑기(모듈 전체 재추출)까지 안 가고 프리팹 하나만 즉시 등록한다 — 아직 맵에 안 쓴 프리팹도 바로 붓으로 쓰게 한다.
    private void DrawNewPrefabDrop()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("새 프리팹", EditorStyles.miniLabel, GUILayout.Width(56));
            _newPrefab = (GameObject)EditorGUILayout.ObjectField(
                _newPrefab, typeof(GameObject), false, GUILayout.Width(160));
        }

        if (_newPrefab != null)
        {
            RegisterDroppedPrefab();
        }
    }

    private void RegisterDroppedPrefab()
    {
        if (!_theme.Has(_brush, _newPrefab))
        {
            ThemeIO.RegisterPrefab(_theme, _brush, _newPrefab);
        }

        _pick = _newPrefab;
        _newPrefab = null;
    }

    private static int ValidLanes(IReadOnlyList<LaneData> lanes)
    {
        int valid = 0;
        for (int i = 0; i < lanes.Count; i++)
        {
            if (lanes[i].IsValid)
            {
                valid++;
            }
        }

        return valid;
    }

    private int HeroTotal()
    {
        int total = 0;
        for (int i = 0; i < _heroGroups.Count; i++)
        {
            total += _heroGroups[i].Melee.Count + _heroGroups[i].Ranged.Count;
        }

        return total;
    }

    private void DrawProblems(List<string> problems)
    {
        if (problems.Count == 0)
        {
            // 0을 빈 화면으로 두면 "깨끗한 것"과 "아직 안 돌린 것"이 구분되지 않는다.
            EditorGUILayout.HelpBox("문제 없음 — 경로·좌표·배치 허용 모두 지금 규칙에 맞습니다.", MessageType.Info);
            return;
        }

        foreach (string problem in problems)
        {
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }
    }
}
