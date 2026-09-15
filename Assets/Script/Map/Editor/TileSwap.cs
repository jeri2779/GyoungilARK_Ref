using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 한 칸의 타일 실물을 지형 프리팹으로 갈아끼우는 에디터 전용 도구.
///
/// 지형 붓이 State.Terrain만 바꾸면 데이터는 High인데 큐브는 여전히 납작한 지상이다 —
/// 데이터와 겉모습이 갈라진다. 여기서 실물을 바꿔 둘을 맞춘다.
///
/// 한 칸은 오브젝트 하나가 아니다. 고지 칸은 밑판 + 그 위에 얹은 얇은 판 두 겹이고,
/// 외곽은 밑판 + 벽 두 겹이다. 그래서 "바꾸기"는 교체(밑판)와 쌓기·걷어내기(윗판)로 나뉜다.
///
/// 크기는 프리팹 저작값을 믿지 않고 그 모듈 격자에 맞춘다 — 셀 크기가 2인 맵에
/// 1짜리 큐브를 찍으면 반쪽이 박히기 때문이다. 고지 판의 두께는 한 단(셀의 절반)으로 맞춘다.
/// </summary>
public static class TileSwap
{
    /// <summary>고지 판 두께가 셀 크기에서 차지하는 비율(한 단 = 반 칸). 기존 저작값과 같다.</summary>
    private const float StepRatio = 0.5f;

    /// <summary>이 칸에 쌓인 타일 전부를 아래에서 위 순으로.</summary>
    public static List<Tile> Stack(Grid module, Vector2Int coord)
    {
        var layers = new List<Tile>();
        foreach (Tile tile in module.GetComponentsInChildren<Tile>(true))
        {
            if (tile.Coord == coord)
            {
                layers.Add(tile);
            }
        }

        layers.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        return layers;
    }

    /// <summary>
    /// 이 칸을 해당 지형으로 만든다. 새 대표 타일(제일 위)을 돌려준다. 아무것도 하지 않았으면 null.
    ///
    /// 붓 지형이 지금 보이는 겹과 같으면 겉모습만 바꾸는 뜻이라 그 겹을 갈아끼운다(겹 구조·데이터는 그대로).
    /// 지형을 바꾸는 뜻이면 얹혀 있던 고지 판을 걷고 밑판을 갈아끼운다.
    /// 벽이 얹힌 칸의 지형 바꾸기는 하지 않는다 — 밑판만 갈면 격자가 읽는 대표는 벽이라 겉과 속이 갈라진다.
    /// </summary>
    public static Tile Apply(Grid module, Vector2Int coord, GameObject prefab, TerrainType terrain)
    {
        List<Tile> layers = Stack(module, coord);
        if (layers.Count == 0)
        {
            return null;
        }

        float cell = module.cellSize.x;
        Tile top = layers[layers.Count - 1];

        if (terrain == TerrainType.High)
        {
            if (top.State.Terrain == TerrainType.High)
            {
                Tile made = Replace(top, prefab, terrain, cell);
                Scale(made.transform, cell * StepRatio, true);
                return made; // 같은 고지 외관만 교체하고 한 단 높이는 유지한다
            }

            return Raise(top, prefab, cell);
        }

        if (top.State.Terrain == terrain)
        {
            return Replace(top, prefab, terrain, cell); // 보이는 겹의 겉모습만 바꾼다
        }

        if (Walled(layers))
        {
            return null; // 예고줄이 "지우기로 먼저 걷으세요"라고 말한 그 경우다
        }

        return Lower(layers, prefab, terrain, cell);
    }

    /// <summary>
    /// 밑판 위에 고지가 아닌 겹(벽)이 얹혀 있는가.
    ///
    /// 그런 칸은 밑판을 갈아도 격자·경로가 읽는 대표 타일이 여전히 벽이라 지형이 바뀌지 않는다.
    /// 교체와 예고줄이 같은 값을 봐야 하므로 판단을 여기 한 곳에 둔다.
    /// </summary>
    public static bool Walled(List<Tile> layers)
    {
        if (layers.Count == 0)
        {
            return false;
        }

        Tile bottom = layers[0];
        foreach (Tile layer in layers)
        {
            if (layer != bottom && layer.State.Terrain != TerrainType.High)
            {
                return true;
            }
        }

        return false;
    }

    // 얹혀 있던 고지 판을 걷고 밑판을 갈아끼운다(위에 남은 것이 고지뿐일 때만 부른다).
    private static Tile Lower(List<Tile> layers, GameObject prefab, TerrainType terrain, float cell)
    {
        Tile bottom = layers[0];
        bool spawn = bottom.isEnemySpawn;

        foreach (Tile layer in layers)
        {
            if (layer == bottom)
            {
                continue;
            }

            spawn |= layer.isEnemySpawn;
            Undo.DestroyObjectImmediate(layer.gameObject);
        }

        Tile made = Replace(bottom, prefab, terrain, cell);

        // 걷어낸 판이 들고 있던 스폰까지 합친다 — 판을 지우면서 그 칸의 스폰이 같이 사라지지 않게.
        made.isEnemySpawn = spawn;
        EditorUtility.SetDirty(made);
        return made;
    }

    /// <summary>
    /// 이 칸에서 제일 위 한 겹을 없앤다. 지운 뒤 남은 대표 타일을 돌려준다(다 지웠으면 null).
    ///
    /// 한 번에 한 겹만 지운다 — 고지 칸에서 판만 걷고 밑판은 남기고 싶은 경우가 대부분이고,
    /// 통째로 지우는 것은 두 번 누르면 되지만 잘못 지운 것은 눈치채기 어렵다.
    /// </summary>
    public static Tile Erase(Grid module, Vector2Int coord)
    {
        List<Tile> layers = Stack(module, coord);
        if (layers.Count == 0)
        {
            return null;
        }

        Undo.DestroyObjectImmediate(layers[layers.Count - 1].gameObject);
        return layers.Count >= 2 ? layers[layers.Count - 2] : null;
    }

    // 밑판을 같은 자리에 다른 프리팹으로 교체한다. 저작값은 그대로 옮기고 지형만 붓이 새로 쓴다.
    // 지형이 실제로 바뀐 칸만 배치 허용을 기본으로 다시 깐다 — 겉모습만 갈아끼울 때 저작을 지우면 안 된다.
    private static Tile Replace(Tile old, GameObject prefab, TerrainType terrain, float cell)
    {
        Transform from = old.transform;
        Tile made = Create(prefab, from.parent, from.position, from.rotation, cell);

        made.transform.SetSiblingIndex(from.GetSiblingIndex());
        made.name = old.name; // 계층에서 같은 칸을 계속 같은 이름으로 찾게 한다
        Carry(old, made);
        made.State.Terrain = terrain;

        if (old.State.Terrain != terrain)
        {
            TileTerrainDefault.Apply(made);
        }

        EditorUtility.SetDirty(made);

        Undo.DestroyObjectImmediate(old.gameObject);
        return made;
    }

    /// <summary>
    /// 그 칸에 저작해 둔 값을 새 타일로 옮긴다. 지형만 붓이 새로 쓰고 나머지는 원래 것을 지킨다.
    ///
    /// 겉모습을 갈아끼우려는 클릭이 배치 허용·스폰·경로 표식을 지워서는 안 된다 —
    /// 지워지면 되돌리기 전까지 알아채기 어렵고, 모듈을 통째로 치환하면 저작이 전부 날아간다.
    /// </summary>
    private static void Carry(Tile old, Tile made)
    {
        TileState was = old.State;
        TileState now = made.State;

        now.Col = was.Col;
        now.Row = was.Row;
        now.EnemyLane = was.EnemyLane;
        now.CanMelee = was.CanMelee;
        now.CanRanged = was.CanRanged;
        now.CanBuild = was.CanBuild;
        now.Pass = was.Pass;
        now.Gimmick = was.Gimmick;
        now.Flags = was.Flags;
        now.Occupant = was.Occupant;

        made.isEnemySpawn = old.isEnemySpawn;
        made.UnitPrefab = old.UnitPrefab;
        made.UnitKind = old.UnitKind;
    }

    // 밑판 윗면에 고지 판을 얹는다. 밑판은 그대로 두어 옆면이 뚫리지 않게 한다.
    // 밑판의 배치 허용은 옮기지 않고 고지 기본(원거리)으로 깐다 — 얹은 판은 교체가 아니라 새 겹이고,
    // 걷어내면 밑판의 저작이 그대로 다시 드러난다.
    private static Tile Raise(Tile support, GameObject prefab, float cell)
    {
        Transform under = support.transform;
        var spot = new Vector3(under.position.x, TopY(support), under.position.z);

        Tile made = Create(prefab, under.parent, spot, under.rotation, cell);
        Scale(made.transform, cell * StepRatio, true); // 한 단 두께로 눌러 놓는다

        made.State.Terrain = TerrainType.High;
        made.State.Col = support.State.Col;
        made.State.Row = support.State.Row;
        TileTerrainDefault.Apply(made);
        MoveSpawn(support, made);
        EditorUtility.SetDirty(made);
        return made;
    }

    /// <summary>
    /// 스폰 표식을 위 겹으로 옮긴다. 복사가 아니라 이동이다.
    ///
    /// MapBoard는 대표 타일이 아니라 표식이 붙은 타일을 전부 스폰으로 세므로 두 겹에 남으면 한 칸이 두 번 스폰한다.
    /// 반대로 밑에만 남겨 두면 격자·경로 검사는 대표 타일(위 겹)만 보므로 그 스폰을 못 본다 —
    /// 도구에는 안 보이는데 실제로는 적이 나오는 칸이 된다.
    /// </summary>
    private static void MoveSpawn(Tile from, Tile to)
    {
        if (!from.isEnemySpawn)
        {
            return;
        }

        Undo.RecordObject(from, "Swap Tile");
        from.isEnemySpawn = false;
        to.isEnemySpawn = true;
    }

    /// <summary>
    /// 프리팹 링크를 살려 생성한다 — 프리팹 모드면 원본에, 씬이면 그 씬에만 남는다(창의 ◆프리팹/○씬 표시가 그 경고다).
    ///
    /// Tile은 원본 큐브가 아니라 여기서 붙인다: 큐브 프리팹(KUBIKOS 등)에는 Tile이 없고,
    /// 모듈들도 큐브 인스턴스에 Tile을 얹는 방식으로 저작돼 있다. 원본 에셋은 건드리지 않는다.
    /// </summary>
    private static Tile Create(GameObject prefab, Transform parent, Vector3 spot, Quaternion turn, float cell)
    {
        var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(made, "Swap Tile");

        Transform tr = made.transform;
        tr.SetPositionAndRotation(spot, turn);
        Scale(tr, cell, false); // 격자 칸을 꽉 채우게 맞춘다

        Tile tile = made.GetComponent<Tile>();
        return tile != null ? tile : Undo.AddComponent<Tile>(made);
    }

    // 렌더러 실측을 재서 원하는 치수가 되도록 스케일을 곱한다. height=true면 높이를, 아니면 가로를 맞춘다.
    private static void Scale(Transform tr, float target, bool height)
    {
        float now = height ? SizeY(tr) : SizeX(tr);
        if (now <= 0.0001f)
        {
            return; // 잴 면이 없다 — 저작값 그대로 둔다
        }

        float factor = target / now;
        Vector3 scale = tr.localScale;

        if (height)
        {
            scale.y *= factor;
        }
        else
        {
            scale *= factor;
        }

        tr.localScale = scale;
    }

    private static float SizeX(Transform tr)
    {
        foreach (Renderer rend in tr.GetComponentsInChildren<Renderer>())
        {
            return rend.bounds.size.x;
        }

        return 0f;
    }

    private static float SizeY(Transform tr)
    {
        foreach (Renderer rend in tr.GetComponentsInChildren<Renderer>())
        {
            return rend.bounds.size.y;
        }

        return 0f;
    }

    /// <summary>이 타일의 윗면 높이 — 위에 판이나 장식을 얹을 자리.</summary>
    public static float TopY(Tile tile)
    {
        float top = tile.transform.position.y;
        bool measured = false;

        foreach (Renderer rend in tile.GetComponentsInChildren<Renderer>())
        {
            if (!measured || rend.bounds.max.y > top)
            {
                top = rend.bounds.max.y;
                measured = true;
            }
        }

        return top;
    }
}
