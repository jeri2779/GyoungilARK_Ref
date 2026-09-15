using UnityEditor;
using UnityEngine;

// 평문 JSON을 스크롤 가능한 텍스트 영역에 보여주는 에디터 창.
public class SaveDumpWindow : EditorWindow
{
    private string json;
    private Vector2 scrollPosition;

    // 전달받은 JSON을 표시하는 창을 연다
    public static void ShowJson(string title, string json)
    {
        SaveDumpWindow window = GetWindow<SaveDumpWindow>(true, title);
        window.json = json;
        window.Show();
    }

    // 창 안에 스크롤되는 읽기전용 텍스트로 JSON을 그린다
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.TextArea(json, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
}
