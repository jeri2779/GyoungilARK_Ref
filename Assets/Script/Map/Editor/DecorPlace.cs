using UnityEditor;
using UnityEngine;

/// <summary>
/// 타일 위에 장식을 얹고 걷어내는 에디터 전용 도구.
///
/// 장식은 규칙 데이터가 아니다 — Tile 컴포넌트를 붙이지 않아 격자·경로·배치 판단에 아예 잡히지 않는다.
/// 타일의 자식으로도 넣지 않는다: 렌더러 바운즈가 커져 그 칸의 윗면 높이가 장식 높이로 밀린다
/// (ModuleScan과 런타임 WorldTop이 그 값을 쓴다). 그래서 모듈에 이미 있는 Decor 그룹 밑에 형제로 둔다.
///
/// 크기는 손대지 않는다. 지형 큐브는 칸을 꽉 채워야 해서 격자에 맞춰 늘리지만,
/// 나무·바위는 프리팹에 저작된 크기가 의도한 크기다.
///
/// 어느 칸의 장식인지는 그 칸 타일과의 수평 거리로 본다 — 장식에는 좌표를 새기지 않으므로
/// (Tile이 없어 새길 자리도 없다) 기준을 타일 실물에 두고 반 칸 안이면 그 칸으로 센다.
/// </summary>
public static class DecorPlace
{
    /// <summary>이 모듈의 장식 그룹. 없으면 null.</summary>
    public static Transform Find(Grid module)
    {
        Transform holder = Holder(module);
        foreach (Transform child in holder)
        {
            if (child.name.StartsWith("Decor"))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>이 모듈에 얹힌 장식 수 — 칸을 가리지 않은 전체.</summary>
    public static int Total(Grid module)
    {
        Transform group = Find(module);
        return group != null ? group.childCount : 0;
    }

    /// <summary>이 칸에 얹힌 장식 수.</summary>
    public static int Count(Grid module, Tile under)
    {
        Transform group = Find(module);
        if (group == null || under == null)
        {
            return 0;
        }

        float cell = module.cellSize.x;
        int found = 0;

        foreach (Transform child in group)
        {
            if (Over(child, under, cell))
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>이 칸 타일 윗면에 장식을 얹는다. 얹은 것을 돌려준다.</summary>
    public static GameObject Add(Grid module, Tile under, GameObject prefab)
    {
        if (under == null || prefab == null)
        {
            return null;
        }

        Transform group = Group(module);
        var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        Undo.RegisterCreatedObjectUndo(made, "Add Decor");

        Vector3 spot = under.transform.position;
        made.transform.position = new Vector3(spot.x, TileSwap.TopY(under), spot.z);

        return made;
    }

    /// <summary>이 칸에 얹힌 장식 중 제일 위 하나를 걷어낸다. 걷었으면 true.</summary>
    public static bool Remove(Grid module, Tile under)
    {
        Transform top = Top(module, under);
        if (top == null)
        {
            return false;
        }

        Undo.DestroyObjectImmediate(top.gameObject);
        return true;
    }

    /// <summary>이 칸에 얹힌 장식 중 제일 위 하나의 이름. 없으면 빈 문자열.</summary>
    public static string TopName(Grid module, Tile under)
    {
        Transform top = Top(module, under);
        return top != null ? top.name : string.Empty;
    }

    // 이 칸에 얹힌 장식 중 제일 위. 위에서부터 걷어내는 지우기 규칙과 같은 방향으로 고른다.
    private static Transform Top(Grid module, Tile under)
    {
        Transform group = Find(module);
        if (group == null || under == null)
        {
            return null;
        }

        float cell = module.cellSize.x;
        Transform top = null;

        foreach (Transform child in group)
        {
            if (!Over(child, under, cell))
            {
                continue;
            }

            if (top == null || child.position.y > top.position.y)
            {
                top = child;
            }
        }

        return top;
    }

    // 장식 그룹. 없으면 만든다 — 모듈 프리팹에는 보통 이미 있다.
    private static Transform Group(Grid module)
    {
        Transform found = Find(module);
        if (found != null)
        {
            return found;
        }

        var made = new GameObject("Decor");
        Undo.RegisterCreatedObjectUndo(made, "Add Decor Group");
        made.transform.SetParent(Holder(module), false);
        return made.transform;
    }

    // 장식 그룹이 매달리는 자리 — 모듈(Grid)의 부모. Grid 밑에 넣으면 타일과 섞인다.
    private static Transform Holder(Grid module)
    {
        Transform parent = module.transform.parent;
        return parent != null ? parent : module.transform;
    }

    // 이 장식이 그 칸 위에 있는가. 반 칸을 넘어가면 옆 칸으로 센다.
    private static bool Over(Transform decor, Tile under, float cell)
    {
        Vector3 spot = decor.position;
        Vector3 tile = under.transform.position;
        float half = cell * 0.5f;

        return Mathf.Abs(spot.x - tile.x) < half && Mathf.Abs(spot.z - tile.z) < half;
    }
}
