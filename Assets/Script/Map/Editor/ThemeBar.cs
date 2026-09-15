using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 테마 탭과 그 테마가 가진 프리팹 썸네일을 그린다.
///
/// 고른 것이 곧 붓이다 — 썸네일을 누르면 그 다음 클릭이 그 프리팹을 찍는다(따로 적용 단계가 없다).
/// 테마·붓·프리팹 셋을 접어 감추지 않고 탭·목록·격자로 동시에 펼쳐 둔다.
/// 저작 중에는 "무슨 테마의 무슨 붓으로 무슨 프리팹을 찍는지"를 늘 보고 있어야 한다.
///
/// 썸네일에 이름을 같이 적는다 — 그림만으로는 Cube_Soil과 Cube_Ground가 구분되지 않는다.
/// 그래서 좁은 곁판이 아니라 창 아래 선반에 놓는다(프로젝트 창이 에셋을 보여주는 방식과 같다).
///
/// 줄은 부르는 쪽이 연다 — 탭 옆에 다른 버튼을 같이 놓을 수 있어야 한다.
/// </summary>
public static class ThemeBar
{
    private const int Thumb = 52;
    private const int Cell = 78;
    private const int Word = 15;

    private static GUIStyle _name;

    /// <summary>테마 탭들. 고른 테마를 돌려준다.</summary>
    public static TileTheme DrawTabs(List<TileTheme> themes, TileTheme current)
    {
        TileTheme picked = current;

        for (int i = 0; i < themes.Count; i++)
        {
            TileTheme theme = themes[i];

            // 늘어나지 않게 못박는다 — 아래 판이 창보다 넓어지면 그 여유 폭을 탭이 빨아들여 쭉 늘어난다.
            bool pressed = GUILayout.Toggle(
                theme == current, theme.Title, EditorStyles.miniButton,
                GUILayout.MinWidth(56), GUILayout.ExpandWidth(false));

            // 고른 탭은 매 프레임 true로 돌아온다 — 새로 눌린 것만 골라내야 선택이 뒤집히지 않는다.
            if (pressed && theme != current)
            {
                picked = theme;
            }
        }

        return picked;
    }

    /// <summary>
    /// 이 붓이 찍을 프리팹 썸네일들. 누른 프리팹을 돌려준다(안 눌렀으면 받은 것 그대로).
    /// 폭에 맞춰 줄을 나눈다 — 판이 좁아도 썸네일을 줄이지 않는다.
    /// </summary>
    public static GameObject DrawPicks(TileTheme theme, MapBrush brush, GameObject pick, float width)
    {
        if (theme == null)
        {
            return pick;
        }

        GameObject[] prefabs = theme.For(brush);
        int perRow = Mathf.Max(1, (int)(width / Cell));
        int drawn = 0;
        GameObject picked = pick;

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null)
            {
                continue; // 인스펙터에서 비워 둔 자리
            }

            if (drawn % perRow == 0)
            {
                if (drawn > 0)
                {
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
            }

            if (DrawThumb(prefab, prefab == pick))
            {
                picked = prefab;
            }

            drawn++;
        }

        if (drawn > 0)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return picked;
        }

        DrawHint(brush, width);
        return picked;
    }

    // 프리팹이 없다는 말. 짧게 적고 설명은 툴팁으로 넘긴다 —
    // 판보다 긴 글은 줄바꿈되지 않고 판을 밀어 넓혀, 창 전체 폭이 늘어나면서 위쪽 탭까지 늘어난다.
    private static void DrawHint(MapBrush brush, float width)
    {
        var text = new GUIContent("프리팹 없음", Hint(brush));
        GUILayout.Label(text, EditorStyles.miniLabel, GUILayout.Width(width));
    }

    // 썸네일 한 장과 그 아래 이름. 고른 것은 밝은 테두리를 둘러 어느 것이 붓인지 보이게 한다.
    private static bool DrawThumb(GameObject prefab, bool chosen)
    {
        Rect slot = GUILayoutUtility.GetRect(
            Cell, Thumb + Word, GUILayout.Width(Cell), GUILayout.Height(Thumb + Word));

        var frame = new Rect(slot.x, slot.y, slot.width, Thumb);
        if (chosen)
        {
            EditorGUI.DrawRect(frame, MapMakerPalette.Mark);
        }

        var inner = new Rect(frame.x + 2f, frame.y + 2f, frame.width - 4f, frame.height - 4f);
        EditorGUI.DrawRect(inner, MapMakerPalette.Panel);

        Texture2D shot = AssetPreview.GetAssetPreview(prefab);
        if (shot != null)
        {
            GUI.DrawTexture(inner, shot, ScaleMode.ScaleToFit);
        }

        // 이름은 잘라서 적는다 — 넘치는 글자가 판을 밀어 넓히면 창 전체 폭이 늘어난다.
        var line = new Rect(slot.x, frame.yMax, slot.width, Word);
        GUI.Label(line, prefab.name, Name());

        return GUI.Button(slot, new GUIContent(string.Empty, prefab.name), GUIStyle.none);
    }

    // 미리보기가 아직 없을 때(비동기 생성 중)도 이름만으로 고를 수 있으므로 따로 대체 그림을 두지 않는다.
    private static GUIStyle Name()
    {
        if (_name == null)
        {
            _name = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
        }

        return _name;
    }

    private static string Hint(MapBrush brush)
    {
        if (brush == MapBrush.Decor)
        {
            return "장식 프리팹이 없습니다 — 테마 에셋에 추가하세요";
        }

        return "이 붓의 프리팹이 없습니다 — 테마 에셋에 추가하세요";
    }
}
