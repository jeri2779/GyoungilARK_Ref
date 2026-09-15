using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맵 메이커의 경로 도구가 RouteConfig의 저작 목록을 고치는 에디터 전용 도구.
///
/// SerializedObject로만 쓴다 — RouteConfig·RouteData의 필드는 private이고, 이쪽으로 고치면
/// 되돌리기와 프리팹 오버라이드 처리를 유니티가 맡는다(런타임 코드에 저작용 API를 만들지 않는다).
///
/// 저장 자리는 MapBoard가 붙은 오브젝트다. 모듈은 뿌리(MapModule_A)에 MapBoard·EnemyLanes를 두고
/// Grid는 그 자식이라, Grid에 붙이면 경로만 EnemyLanes와 다른 오브젝트로 갈라진다.
/// </summary>
public static class RouteEdit
{
    private const string RoutesField = "routes";
    private const string SpawnField = "spawn";
    private const string DayField = "day";
    private const string NodesField = "nodes";
    private const string ColField = "col";
    private const string RowField = "row";
    private const string WaitField = "waitTime";

    // 새로 찍은 칸이 멈추지 않는다는 뜻. 멈추게 하려면 사람이 목록에서 따로 적는다.
    private const float UnauthoredWaitTime = 0f;

    /// <summary>지금 볼 일차를 정한다. 아직 RouteConfig가 없으면 정할 것이 없다.</summary>
    public static void SetDay(RouteConfig config, int day)
    {
        if (config == null)
        {
            return;
        }

        config.SetActiveDay(day);
    }

    /// <summary>이 모듈의 RouteConfig. 아직 없으면 null.</summary>
    public static RouteConfig Find(Grid module)
    {
        MapBoard board = Owner(module);
        if (board == null)
        {
            return null;
        }

        return board.GetComponent<RouteConfig>();
    }

    /// <summary>저작할 RouteConfig를 확보한다. 없으면 MapBoard 옆에 새로 붙인다.</summary>
    public static RouteConfig Ensure(Grid module)
    {
        MapBoard board = Owner(module);
        if (board == null)
        {
            return null;
        }

        RouteConfig found = board.GetComponent<RouteConfig>();
        if (found != null)
        {
            return found;
        }

        Warn(board);
        return Undo.AddComponent<RouteConfig>(board.gameObject);
    }

    /// <summary>
    /// 조회 사전을 목록과 맞춘다. RouteConfig는 Awake·OnValidate에서만 사전을 다시 세우는데,
    /// 되돌리기는 OnValidate를 부른다는 보장이 없어 창이 지워진 경로를 계속 그리게 된다.
    /// </summary>
    public static void Sync(RouteConfig config)
    {
        if (config == null)
        {
            return;
        }

        config.Rebuild();
    }

    /// <summary>이 경로를 이 목록으로 통째로 바꾼다. 같은 좌표가 여러 번 들어와도 그대로 쓴다.</summary>
    public static void SetNodes(RouteConfig config, int routeIndex, IReadOnlyList<Vector2Int> stroke)
    {
        var owner = new SerializedObject(config);
        SerializedProperty nodes = GetRouteAt(owner, routeIndex).FindPropertyRelative(NodesField);

        nodes.arraySize = stroke.Count;
        for (int i = 0; i < stroke.Count; i++)
        {
            SetNode(nodes.GetArrayElementAtIndex(i), stroke[i]);
        }

        owner.ApplyModifiedProperties();
    }

    /// <summary>이 좌표를 맨 뒤에 쌓는다. 이미 들어 있어도 또 쌓는다 — 같은 칸을 다시 지나가는 경로다.</summary>
    public static void AddNode(RouteConfig config, int routeIndex, Vector2Int node)
    {
        var owner = new SerializedObject(config);
        SerializedProperty nodes = GetRouteAt(owner, routeIndex).FindPropertyRelative(NodesField);

        nodes.arraySize++;
        SetNode(nodes.GetArrayElementAtIndex(nodes.arraySize - 1), node);
        owner.ApplyModifiedProperties();
    }

    /// <summary>이 좌표를 slot 자리에 끼운다.</summary>
    public static void InsertNode(RouteConfig config, int routeIndex, Vector2Int node, int slot)
    {
        var owner = new SerializedObject(config);
        SerializedProperty nodes = GetRouteAt(owner, routeIndex).FindPropertyRelative(NodesField);

        Insert(nodes, node, slot);
        owner.ApplyModifiedProperties();
    }

    /// <summary>맨 뒤 한 칸을 뺀다 — 뒤로가기다. 뺄 것이 없으면 아무 일도 하지 않는다.</summary>
    public static void PopNode(RouteConfig config, int routeIndex)
    {
        var owner = new SerializedObject(config);
        SerializedProperty nodes = GetRouteAt(owner, routeIndex).FindPropertyRelative(NodesField);

        if (nodes.arraySize == 0)
        {
            return;
        }

        nodes.arraySize--;
        owner.ApplyModifiedProperties();
    }

    /// <summary>이 경로의 경유 노드를 전부 지운다. 경로 항목은 빈 채로 남는다.</summary>
    public static void ClearNodes(RouteConfig config, int routeIndex)
    {
        var owner = new SerializedObject(config);
        SerializedProperty route = GetRouteAt(owner, routeIndex);
        route.FindPropertyRelative(NodesField).ClearArray();
        owner.ApplyModifiedProperties();
    }

    /// <summary>이 순번 노드에서 멈출 초를 적는다.</summary>
    public static void SetWait(RouteConfig config, int routeIndex, int slot, float seconds)
    {
        var owner = new SerializedObject(config);
        SerializedProperty nodes = GetRouteAt(owner, routeIndex).FindPropertyRelative(NodesField);

        nodes.GetArrayElementAtIndex(slot).FindPropertyRelative(WaitField).floatValue = seconds;
        owner.ApplyModifiedProperties();
    }

    /// <summary>이 스폰에 빈 경로 항목을 하나 더 만들고 그 항목을 돌려준다.</summary>
    public static RouteData AddRoute(RouteConfig config, Vector2Int spawn, int day)
    {
        var owner = new SerializedObject(config);
        SerializedProperty routes = owner.FindProperty(RoutesField);

        routes.arraySize++;
        int made = routes.arraySize - 1;
        SerializedProperty added = routes.GetArrayElementAtIndex(made);
        added.FindPropertyRelative(SpawnField).vector2IntValue = spawn;
        added.FindPropertyRelative(DayField).intValue = day;
        added.FindPropertyRelative(NodesField).ClearArray();
        owner.ApplyModifiedProperties();

        config.Rebuild();
        return config.RouteAt(made);
    }

    // 경로가 붙어 사는 오브젝트. 비활성 모듈에서도 찾아야 한다(잠긴 모듈도 저작 대상이다).
    private static MapBoard Owner(Grid module)
    {
        return module.GetComponentInParent<MapBoard>(true);
    }

    // 씬 인스턴스에 컴포넌트를 새로 붙이면 그 씬에만 남는다. 값 오버라이드보다 눈에 안 띄어 미리 알린다.
    private static void Warn(MapBoard board)
    {
        if (ModuleScan.IsPrefabStage())
        {
            return;
        }

        Debug.LogWarning("[Map Maker] 씬 인스턴스에 RouteConfig를 새로 붙입니다 — " +
            "저작한 경로는 이 씬에만 남습니다. 모듈 프리팹을 열고 찍으면 모든 씬에 반영됩니다.", board);
    }

    // 번호로 경로 항목을 집는다. 번호는 RouteConfig.IndexOf가 준다 — 좌표로 찾지 않는다.
    private static SerializedProperty GetRouteAt(SerializedObject owner, int routeIndex)
    {
        return owner.FindProperty(RoutesField).GetArrayElementAtIndex(routeIndex);
    }

    private static void SetNode(SerializedProperty node, Vector2Int coord)
    {
        node.FindPropertyRelative(ColField).intValue = coord.x;
        node.FindPropertyRelative(RowField).intValue = coord.y;
        node.FindPropertyRelative(WaitField).floatValue = UnauthoredWaitTime;
    }

    // 정해진 자리에 좌표를 끼운다. 맨 뒤는 끼울 자리가 없으므로 목록을 늘려 붙인다.
    private static void Insert(SerializedProperty nodes, Vector2Int node, int slot)
    {
        if (slot >= nodes.arraySize)
        {
            nodes.arraySize++;
            SetNode(nodes.GetArrayElementAtIndex(nodes.arraySize - 1), node);
            return;
        }

        nodes.InsertArrayElementAtIndex(slot);
        SetNode(nodes.GetArrayElementAtIndex(slot), node);
    }
}
