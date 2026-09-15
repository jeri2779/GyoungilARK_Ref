using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 편집 대상에서 모듈(Grid)과 그 하위 타일을 모으는 에디터 전용 도구.
/// 전제: Grid 하나 = 모듈 하나(TilePosBaker와 같은 전제). 좌표는 TilePosBaker가 새긴 값을 그대로 믿는다.
///
/// 대상은 프리팹 스테이지를 먼저 본다. 모듈은 프리팹(MapModule_A~E)이고 여러 씬이 같은 프리팹을 쓰므로,
/// 씬 인스턴스를 고치면 오버라이드로만 남아 다른 씬에는 반영되지 않는다.
/// </summary>
public static class ModuleScan
{
    /// <summary>지금 편집 대상이 프리팹 스테이지인가. 창이 "프리팹/씬"을 구분해 띄우는 데 쓴다.</summary>
    public static bool IsPrefabStage()
    {
        return PrefabStageUtility.GetCurrentPrefabStage() != null;
    }

    /// <summary>편집 대상 이름. 프리팹 스테이지면 프리팹 에셋명, 아니면 활성 씬 이름.</summary>
    public static string TargetName()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            return System.IO.Path.GetFileNameWithoutExtension(stage.assetPath);
        }

        return SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// 이 모듈의 원본 프리팹 에셋. 테마가 "내가 기본인 모듈"을 가리킬 때 쓰는 값이다.
    /// 프리팹 스테이지면 지금 열어 둔 에셋, 씬이면 그 인스턴스의 원본이다.
    ///
    /// 계층 루트가 아니라 Grid에서 가장 가까운 인스턴스 뿌리를 본다 —
    /// 모듈이 MapRoot 같은 상위 프리팹 안에 들어 있으면 루트를 보면 전부 같은 프리팹으로 나온다.
    /// </summary>
    public static GameObject SourcePrefab(Grid module)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
        }

        GameObject near = PrefabUtility.GetNearestPrefabInstanceRoot(module.gameObject);
        if (near == null)
        {
            return null; // 프리팹에서 온 모듈이 아니다 — 가리킬 원본이 없다
        }

        return PrefabUtility.GetCorrespondingObjectFromSource(near);
    }

    // 목록에 띄울 모듈 이름. 상위에 묶어 두면 계층 꼭대기는 전부 같은 이름이라 프리팹 뿌리를 본다.
    public static string ModuleName(Grid module)
    {
        GameObject near = PrefabUtility.GetNearestPrefabInstanceRoot(module.gameObject);
        if (near != null)
        {
            return near.name;
        }

        return module.gameObject.name;
    }

    /// <summary>편집 대상 안의 모든 Grid = 모듈 목록.</summary>
    public static List<Grid> FindModules()
    {
        var modules = new List<Grid>();
        foreach (GameObject root in RootObjects())
        {
            modules.AddRange(root.GetComponentsInChildren<Grid>(true));
        }

        return modules;
    }

    /// <summary>이 모듈 하위 타일 전부(비활성 포함). 좌표를 아직 안 새긴 타일도 그대로 준다.</summary>
    public static List<Tile> CollectTiles(Grid module)
    {
        var tiles = new List<Tile>();
        tiles.AddRange(module.GetComponentsInChildren<Tile>(true));
        return tiles;
    }

    /// <summary>
    /// 타일을 좌표별로 배치해 격자를 만든다. 한 칸에 여럿이 겹치면 가장 높은 타일을 대표로 삼는다
    /// (MapBoard.Build와 같은 규칙 — 바닥 위에 고지 큐브를 쌓은 경우 위쪽이 실제로 보이는 면이다).
    /// </summary>
    public static Dictionary<Vector2Int, Tile> MapCells(List<Tile> tiles, out int cols, out int rows)
    {
        var cells = new Dictionary<Vector2Int, Tile>();
        var tops = new Dictionary<Vector2Int, float>();
        int maxCol = 0;
        int maxRow = 0;

        foreach (Tile tile in tiles)
        {
            Vector2Int coord = tile.Coord;
            float top = TopY(tile);

            bool taken = cells.ContainsKey(coord);
            if (!taken || top > tops[coord])
            {
                cells[coord] = tile;
                tops[coord] = top;
            }

            if (coord.x > maxCol)
            {
                maxCol = coord.x;
            }

            if (coord.y > maxRow)
            {
                maxRow = coord.y;
            }
        }

        cols = maxCol + 1;
        rows = maxRow + 1;
        return cells;
    }

    /// <summary>
    /// 이 타일의 윗면 높이. Tile.WorldTop을 쓰지 않는다 —
    /// 그 값은 MapBoard.Build가 SetTop으로 채워주는 런타임 캐시라 에디터에서는 모든 타일이 0이다.
    /// 0끼리 비교하면 전부 동점이 되어 계층 순서상 먼저 나온 밑판이 대표로 뽑힌다.
    /// 그래서 MapBoard.Build가 캐시에 넣는 값(렌더러 바운즈의 max.y)을 여기서 직접 구한다.
    /// 렌더러가 없는 타일은 비교할 면이 없으므로 피벗 높이를 쓴다.
    /// </summary>
    private static float TopY(Tile tile)
    {
        float top = tile.transform.position.y;
        bool measured = false;

        foreach (Renderer rend in tile.GetComponentsInChildren<Renderer>())
        {
            if (!measured)
            {
                top = rend.bounds.max.y;
                measured = true;
                continue;
            }

            if (rend.bounds.max.y > top)
            {
                top = rend.bounds.max.y;
            }
        }

        return top;
    }

    // 프리팹을 열어둔 상태면 그 사본 루트만, 아니면 활성 씬의 루트들.
    private static GameObject[] RootObjects()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            return new[] { stage.prefabContentsRoot };
        }

        return SceneManager.GetActiveScene().GetRootGameObjects();
    }
}
