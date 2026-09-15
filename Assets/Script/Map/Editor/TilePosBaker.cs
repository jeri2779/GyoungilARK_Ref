using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 안 각 Grid를 독립 "모듈"로 보고, 그 하위 타일의 논리 좌표(Col/Row)를
/// 모듈 로컬 0-base로 계산해 새겨 넣는 에디터 전용 도구.
/// 모듈이 월드 어디에 어떤 각도로 놓여도, 각 모듈이 자기 원점(0,0)부터 시작한다.
/// 전제: Grid는 중첩하지 않는다(모듈당 1개, 상위 전역 Grid 없음).
/// Swizzle XZY 기준으로 cell.x → Col, cell.y → Row, 높이는 cell.z로 빠져 Col/Row를 흔들지 않는다.
/// </summary>
public static class TilePosBaker
{
    [MenuItem("Tools/Map/Bake Tile Positions (Active Scene)")]
    private static void BakeTilePositions()
    {
        Scene scene = SceneManager.GetActiveScene();

        // 씬의 모든 Grid = 모듈들. 각 Grid가 자기 모듈의 좌표공간을 정의한다.
        List<Grid> grids = CollectGrids(scene);
        if (grids.Count == 0)
        {
            WarnNoGrid();
            return;
        }

        Undo.SetCurrentGroupName("Bake Tile Positions");
        int group = Undo.GetCurrentGroup();

        int moduleCount = 0;
        int tileTotal = 0;
        foreach (Grid grid in grids)
        {
            int baked = BakeModule(grid); // -1=문제로 건너뜀, 0=타일 없음
            if (baked <= 0)
            {
                continue;
            }
            moduleCount++;
            tileTotal += baked;
        }

        Undo.CollapseUndoOperations(group);

        if (tileTotal == 0)
        {
            WarnNoTiles();
            return;
        }
        SaveResult(scene, moduleCount, tileTotal);
    }

    // 씬 안 모든 Grid = 모듈 목록. 전제: Grid는 중첩하지 않는다(모듈당 1개).
    private static List<Grid> CollectGrids(Scene scene)
    {
        var grids = new List<Grid>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            grids.AddRange(root.GetComponentsInChildren<Grid>(true));
        }
        return grids;
    }

    // 이 Grid(모듈) 하위 타일만 수집한다 — 씬 전체를 보지 않는다.
    private static List<Tile> CollectTiles(Grid grid)
    {
        var tiles = new List<Tile>();
        tiles.AddRange(grid.GetComponentsInChildren<Tile>(true));
        return tiles;
    }

    // 한 모듈 베이크. 반환: 새긴 타일 수(문제 시 -1, 타일 없으면 0).
    private static int BakeModule(Grid grid)
    {
        if (!CheckGrid(grid))
        {
            return -1;
        }

        List<Tile> tiles = CollectTiles(grid);
        if (tiles.Count == 0)
        {
            return 0;
        }

        AssignCoords(grid, tiles);  // 모듈 min→0 정규화
        CheckTileSize(grid, tiles); // 경고만 — 좌표는 이미 유효하다
        return tiles.Count;
    }

    // 격자 수학이 정사각 셀을 전제한다(맨해튼 거리·4방향 이웃). 직사각이면 거리가 뜻을 잃는다.
    private static bool CheckGrid(Grid grid)
    {
        if (!Mathf.Approximately(grid.cellSize.x, grid.cellSize.y))
        {
            Debug.LogError($"[TilePosBaker] 정사각 셀이 아닙니다 — cellSize {grid.cellSize}. " +
                "격자 거리 계산이 깨집니다. 이 모듈 베이크 중단.", grid);
            return false;
        }

        if (grid.cellGap != Vector3.zero)
        {
            Debug.LogError($"[TilePosBaker] Cell Gap이 0이 아닙니다 — {grid.cellGap}. 이 모듈 베이크 중단.", grid);
            return false;
        }
        return true;
    }

    // 각 타일의 월드 위치를 이 모듈 Grid에 물어 raw 셀을 얻고, 모듈 min을 빼 0-base로 새긴다.
    // → 모듈이 월드 어디에 있든(원점이 아니어도, 음수여도) 항상 (0,0)부터 시작한다.
    private static void AssignCoords(Grid grid, List<Tile> tiles)
    {
        // 1) raw 셀 + 모듈 min 산출
        var raw = new Vector3Int[tiles.Count];
        int minCol = int.MaxValue;
        int minRow = int.MaxValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            Vector3Int cell = grid.WorldToCell(tiles[i].transform.position);
            raw[i] = cell;
            if (cell.x < minCol) minCol = cell.x;
            if (cell.y < minRow) minRow = cell.y;
        }

        // 2) min→0 정규화해 State에 저장 (Undo 그룹은 호출부에서 묶는다)
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile tile = tiles[i];
            Undo.RecordObject(tile, "Bake Tile Position");
            tile.State ??= new TileState();

            tile.State.Col = raw[i].x - minCol;
            tile.State.Row = raw[i].y - minRow;

            EditorUtility.SetDirty(tile);
        }

        // 3) 모듈 min값 자체도 버리지 않고 MapBoard에 저장 — 런타임이 되짚어 추측하지 않고 이 값을 그대로 읽는다.
        SaveBoardOffset(grid, minCol, minRow);
    }

    // 방금 구한 모듈 min을 같은 모듈의 MapBoard에 건네준다. MapBoard가 없으면 조용히 건너뛴다(타일만 있는 임시 씬 등).
    // MapBoard는 Grid와 같은 오브젝트가 아니라 모듈 루트(부모)에 있다 — GetComponentInParent로 찾는다.
    private static void SaveBoardOffset(Grid grid, int minCol, int minRow)
    {
        MapBoard board = grid.GetComponentInParent<MapBoard>();
        if (board == null)
        {
            return;
        }

        Undo.RecordObject(board, "Bake Tile Position");
        board.SetBakedOffset(new Vector2Int(minCol, minRow));
        EditorUtility.SetDirty(board);
    }

    // 타일 실물 크기와 셀 간격이 어긋나면 화면에 틈이 생기거나 겹친다. 좌표와는 무관해 경고만 한다.
    private static void CheckTileSize(Grid grid, List<Tile> tiles)
    {
        float cell = grid.cellSize.x;
        foreach (Tile tile in tiles)
        {
            Renderer rend = tile.GetComponentInChildren<Renderer>();
            if (rend == null)
            {
                continue;
            }

            float width = rend.bounds.size.x;
            if (!Mathf.Approximately(width, cell))
            {
                Debug.LogWarning($"[TilePosBaker] 타일 크기 {width:0.###} ≠ 셀 간격 {cell:0.###} — '{tile.name}'. " +
                    (width < cell ? "타일 사이에 틈이 생깁니다." : "타일이 겹칩니다."), tile);
                return; // 첫 건만 알린다 — 보통 전부 같은 원인이다
            }
        }
    }

    private static void WarnNoGrid()
    {
        EditorUtility.DisplayDialog("Tile Pos Baker",
            "씬에 Grid가 없습니다. 각 모듈 루트의 Grid를 기준으로 좌표를 계산합니다.", "확인");
    }

    private static void WarnNoTiles()
    {
        EditorUtility.DisplayDialog("Tile Pos Baker",
            "Tile 컴포넌트를 가진 타일을 찾지 못했습니다. 큐브 프리팹에 Tile을 붙여 Grid 하위에 배치한 뒤 다시 실행하세요.",
            "확인");
    }

    private static void SaveResult(Scene scene, int moduleCount, int tileCount)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[TilePosBaker] 좌표 베이크 완료 — 모듈 {moduleCount}개 / Tile {tileCount}개 (모듈별 0-base).");
    }
}
