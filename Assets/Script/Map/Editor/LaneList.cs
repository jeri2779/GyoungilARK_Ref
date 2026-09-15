using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스폰마다 만들어진 경로를 한 줄씩 적는다.
///
/// 격자에서는 경로가 전부 같은 흰 선으로 겹쳐 그려져서, 둘 이상이면 어느 스폰에서 나온 길인지 가려지고
/// 막힌 스폰은 아무것도 그려지지 않아 화면에서 사라진다. 여기서 스폰별로 갈라 적어 그 둘을 드러낸다.
///
/// 줄을 누르면 그 경로가 편집 대상이 되고, 격자에서는 그 경로만 남고 나머지가 회색으로 죽는다.
/// </summary>
public static class LaneList
{
    /// <summary>경로 줄들. 고른 경로를 돌려준다(누르지 않았으면 받은 것 그대로).</summary>
    public static RouteData Draw(IReadOnlyList<LaneData> lanes, RouteData chosen, RouteConfig routes)
    {
        if (lanes.Count == 0)
        {
            GUILayout.Label("적 스폰이 없습니다 — 스폰 붓으로 시작 칸을 찍으면 경로가 여기 나옵니다.",
                EditorStyles.wordWrappedMiniLabel);
            return chosen;
        }

        RouteData picked = chosen;

        // 하나를 고르면 나머지가 죽으므로, 전부 다시 보는 길을 같은 자리에 둔다.
        using (new EditorGUI.DisabledScope(IsNone(chosen)))
        {
            if (GUILayout.Button("← 전체 보기 (모든 경로 다시 보기)", EditorStyles.miniButton))
            {
                picked = null;
            }
        }

        Vector2Int? focus = FocusSpawn(chosen);
        int index = 0;

        while (index < lanes.Count)
        {
            int end = GroupEnd(lanes, index);
            picked = Keep(picked, DrawGroup(lanes, index, end, chosen, routes, focus));
            index = end;
        }

        GUILayout.Label("줄을 누르면 그 경로만 남고 나머지는 회색으로 죽습니다. " +
            "[+갈래]는 그 스폰에 길을 하나 더 만듭니다.",
            EditorStyles.wordWrappedMiniLabel);
        DrawGuide();

        return picked;
    }

    // 지금 고른 경로가 나온 스폰. 고른 것이 없으면 null이라 어떤 스폰도 접지 않는다.
    private static Vector2Int? FocusSpawn(RouteData chosen)
    {
        if (IsNone(chosen))
        {
            return null;
        }

        return chosen.Spawn;
    }

    // 같은 스폰 좌표가 몇 번째까지 이어지는가. 목록은 이미 스폰 단위로 붙어 나온다(LaneBuilder).
    private static int GroupEnd(IReadOnlyList<LaneData> lanes, int start)
    {
        Vector2Int spawn = lanes[start].Start.Coord;
        int end = start + 1;

        while (end < lanes.Count && lanes[end].Start.Coord == spawn)
        {
            end++;
        }

        return end;
    }

    // 스폰 한 묶음. 초점이 없거나 이 스폰이 초점이면 갈래를 펼치고, 아니면 한 줄로 접는다.
    private static RouteData DrawGroup(
        IReadOnlyList<LaneData> lanes, int start, int end,
        RouteData chosen, RouteConfig routes, Vector2Int? focus)
    {
        bool expand = !focus.HasValue || lanes[start].Start.Coord == focus.Value;

        if (!expand)
        {
            return DrawFold(lanes, start, end);
        }

        if (end - start > 1)
        {
            GUILayout.Label($"스폰 {lanes[start].Start.Coord}", EditorStyles.miniBoldLabel);
        }

        RouteData picked = null;

        for (int i = start; i < end; i++)
        {
            picked = Keep(picked, DrawLane(lanes[i], i, RowStyle(lanes[i], chosen), routes));

            if (IsChosen(lanes[i], chosen))
            {
                DrawWaits(lanes[i], routes);
            }
        }

        return picked;
    }

    // 접힌 스폰 한 줄. 갈래마다 점을 찍어 격자 선 색과 맞추고, 누르면 그 스폰이 초점이 된다.
    private static RouteData DrawFold(IReadOnlyList<LaneData> lanes, int start, int end)
    {
        bool hasRoute = lanes[start].Route != null;

        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = start; i < end; i++)
            {
                Rect mark = GUILayoutUtility.GetRect(9f, 13f, GUILayout.Width(9));
                EditorGUI.DrawRect(new Rect(mark.x, mark.y + 2f, 7f, 7f), MarkColor(lanes[i], i));
            }

            string word = $"스폰 {lanes[start].Start.Coord} · {end - start}개 경로";

            using (new EditorGUI.DisabledScope(!hasRoute))
            {
                if (GUILayout.Button(word, EditorStyles.miniButton))
                {
                    return lanes[start].Route;
                }
            }
        }

        return null;
    }

    // 고른 것이 없는가.
    private static bool IsNone(RouteData route)
    {
        return route == null;
    }

    // 경로를 적어 둘 자리가 아직 없는가.
    private static bool HasNoConfig(RouteConfig routes)
    {
        return routes == null;
    }

    // 이 줄이 지금 고른 경로인가.
    private static bool IsChosen(LaneData lane, RouteData chosen)
    {
        if (IsNone(chosen))
        {
            return false;
        }

        return lane.Route == chosen;
    }

    // 새로 고른 것이 있으면 그것, 없으면 하던 것.
    private static RouteData Keep(RouteData current, RouteData picked)
    {
        if (IsNone(picked))
        {
            return current;
        }

        return picked;
    }

    // 고른 줄만 굵게 쓴다.
    private static GUIStyle RowStyle(LaneData lane, RouteData chosen)
    {
        return LaneButtonStyle(IsChosen(lane, chosen));
    }

    private static GUIStyle _laneButton;
    private static GUIStyle _laneButtonChosen;

    // 옆의 +갈래·뒤로·비우기와 같은 버튼 모양을 쓴다. 한 번만 만들어 매 프레임 새로 짓지 않는다.
    private static GUIStyle LaneButtonStyle(bool chosen)
    {
        if (_laneButton == null)
        {
            _laneButton = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };
            _laneButtonChosen = new GUIStyle(_laneButton) { fontStyle = FontStyle.Bold };
        }

        return chosen ? _laneButtonChosen : _laneButton;
    }

    // 이 줄의 색점. 길이 끊겼으면 문제 색으로 찍는다.
    private static Color MarkColor(LaneData lane, int index)
    {
        if (IsValidLane(lane))
        {
            return MapMakerPalette.Lane(index);
        }

        return MapMakerPalette.Problem;
    }

    // 본진까지 이어진 레인인가.
    private static bool IsValidLane(LaneData lane)
    {
        return lane.IsValid;
    }

    // 경로 도구 사용법. 처음 여는 사람이 문서를 찾아가지 않아도 되게 쓰는 자리에 둔다.
    private static void DrawGuide()
    {
        EditorGUILayout.HelpBox(
            "경로 그리기 — 위 도구 줄에서 [경로]를 고릅니다.\n\n" +
            "· 그리기 : 스폰 칸을 누른 채 본진까지 끕니다. 지나간 칸이 그대로 경로가 됩니다.\n" +
            "· 같은 칸을 몇 번이고 다시 지나가도 됩니다 — 뱅글 도는 경로를 만들 수 있습니다.\n" +
            "· 스폰을 끌지 않고 누르기만 하면 그 경로를 고르기만 하고 지우지 않습니다.\n\n" +
            "· 점 찍기 : 한 칸씩 눌러 맨 뒤에 쌓습니다. Alt = 뒤로, Ctrl = 들어갈 자리 자동.\n\n" +
            "· [뒤로] : 마지막에 그린 칸부터 하나씩 빠집니다. Ctrl+Z로도 됩니다.\n" +
            "· [비우기] : 그린 것을 다 지웁니다 — 그 경로는 다시 자동 최단 경로가 됩니다.\n" +
            "· [← 전체 보기] : 고른 것을 놓고 모든 경로를 다시 봅니다.\n" +
            "· 끌다 만 나머지는 본진까지 자동으로 이어집니다.\n" +
            "· 지나갈 수 없는 칸에는 선이 안 들어갑니다 — 벽 앞에서 멈춥니다.",
            MessageType.None);
    }

    // 줄 하나. 눌렸으면 이제 고를 경로를 내고, 안 눌렸으면 null.
    private static RouteData DrawLane(LaneData lane, int index, GUIStyle style, RouteConfig routes)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            // 격자에 그려진 선과 같은 색을 찍는다 — 이 점이 줄과 선을 잇는 유일한 단서다.
            Rect mark = GUILayoutUtility.GetRect(11f, 13f, GUILayout.Width(11));
            EditorGUI.DrawRect(new Rect(mark.x, mark.y + 2f, 9f, 9f), MarkColor(lane, index));

            int nodes = NodeCount(lane, routes);

            if (GUILayout.Button(Word(lane, nodes), style))
            {
                return lane.Route;
            }

            // 갈래를 늘리는 유일한 자리. 만든 항목을 그대로 넘겨 번호를 거치지 않는다.
            using (new EditorGUI.DisabledScope(HasNoConfig(routes)))
            {
                if (GUILayout.Button("+갈래", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    return RouteEdit.AddRoute(routes, lane.Start.Coord, routes.SelectedDay);
                }
            }

            using (new EditorGUI.DisabledScope(nodes == 0))
            {
                // 뒤로가기는 맨 뒤 한 칸씩. 마지막에 그린 것부터 빠지므로 손이 기억하는 순서와 같다.
                if (GUILayout.Button("뒤로", EditorStyles.miniButton, GUILayout.Width(34)))
                {
                    RouteEdit.PopNode(routes, routes.IndexOf(lane.Route));
                    return lane.Route; // 줄어든 경로를 바로 보게 고른 상태로 넘긴다
                }

                if (GUILayout.Button("비우기", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    RouteEdit.ClearNodes(routes, routes.IndexOf(lane.Route));
                    return lane.Route;
                }
            }

            return null;
        }
    }

    // 고른 경로의 경유 칸을 한 줄씩 펼쳐 멈출 초를 받는다. 0초는 멈추지 않는 칸이다.
    private static void DrawWaits(LaneData lane, RouteConfig routes)
    {
        if (NodeCount(lane, routes) == 0)
        {
            return;
        }

        RouteData route = lane.Route;

        for (int i = 0; i < route.Nodes.Count; i++)
        {
            DrawWait(routes, route, i);
        }
    }

    // 경유 칸 한 줄. 초를 고쳐 넣은 프레임에만 저작에 적어 되돌리기가 한 번에 하나씩 쌓이게 한다.
    private static void DrawWait(RouteConfig routes, RouteData route, int slot)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(14f);
            GUILayout.Label($"{slot + 1}. {route.Nodes[slot].Coord}", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            float seconds = EditorGUILayout.FloatField(route.Nodes[slot].WaitTime, GUILayout.Width(40));

            if (EditorGUI.EndChangeCheck())
            {
                RouteEdit.SetWait(routes, routes.IndexOf(route), slot, seconds);
            }

            GUILayout.Label("초", EditorStyles.miniLabel, GUILayout.Width(16));
        }
    }

    // 이 스폰에 사람이 찍어 둔 경유 칸 수. 저작이 없으면 0(자동 최단 경로다).
    private static int NodeCount(LaneData lane, RouteConfig routes)
    {
        if (routes == null || lane.Start == null)
        {
            return 0;
        }

        if (lane.Route == null)
        {
            return 0;
        }

        return lane.Route.Nodes.Count;
    }

    private static string Word(LaneData lane, int nodes)
    {
        string from = lane.Start != null ? lane.Start.Coord.ToString() : "(스폰 없음)";
        if (!lane.IsValid)
        {
            return $"스폰 {from} · 막힘 — 본진까지 가는 길이 없습니다";
        }

        if (nodes == 0)
        {
            return $"스폰 {from} · {lane.Tiles.Count}칸 · 자동";
        }

        return $"스폰 {from} · {lane.Tiles.Count}칸 · 그린 {nodes}칸";
    }
}
