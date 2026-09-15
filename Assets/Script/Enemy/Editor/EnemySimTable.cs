using UnityEditor;
using UnityEngine;

// 적 스탯 비교 표를 그리는 출력 담당. 아무것도 계산하지 않고 받은 값만 늘어놓는다.
public static class EnemySimTable
{
    private const float NameWidth = 104f;
    private const float ShortWidth = 60f;
    private const float AttributeWidth = 130f;
    private const float StatWidth = 66f;
    private const float RowHeight = 70f;
    private const float IconSize = 48f;

    private const string IntegerFormat = "N0";
    private const string SpeedFormat = "N2";

    private const string NameHeader = "이름";
    private const string TypeHeader = "타입";
    private const string ClassHeader = "등급";
    private const string AttributeHeader = "속성";
    private const string HpHeader = "HP";
    private const string AttackHeader = "ATK";
    private const string DefenceHeader = "DEF";
    private const string SpeedHeader = "AS";
    private const string MoveHeader = "이속";
    private const string RangeHeader = "사거리";

    private const float GridLineThickness = 1f;
    private static readonly Color GridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    // 표의 머리줄을 그린다.
    public static void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            DrawCell(NameHeader, NameWidth, EditorStyles.boldLabel);
            DrawCell(TypeHeader, ShortWidth, EditorStyles.boldLabel);
            DrawCell(ClassHeader, ShortWidth, EditorStyles.boldLabel);
            DrawCell(AttributeHeader, AttributeWidth, EditorStyles.boldLabel);
            DrawCell(HpHeader, StatWidth, EditorStyles.boldLabel);
            DrawCell(AttackHeader, StatWidth, EditorStyles.boldLabel);
            DrawCell(DefenceHeader, StatWidth, EditorStyles.boldLabel);
            DrawCell(SpeedHeader, ShortWidth, EditorStyles.boldLabel);
            DrawCell(MoveHeader, ShortWidth, EditorStyles.boldLabel);
            DrawCell(RangeHeader, ShortWidth, EditorStyles.boldLabel);
        }
    }

    // 적 한 마리의 표 한 줄을 그린다.
    public static void DrawRow(EnemyTable.Data data, Sprite icon, EnemyStatScaling.Stats stats)
    {
        using (new EditorGUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
        {
            DrawNameCell(icon, data.Name);
            DrawCell(data.Type, ShortWidth, EditorStyles.label);
            DrawCell(data.Class, ShortWidth, EditorStyles.label);
            DrawCell(data.Attribute, AttributeWidth, EditorStyles.label);
            DrawCell(stats.Hp.ToString(IntegerFormat), StatWidth, EditorStyles.label);
            DrawCell(stats.Attack.ToString(IntegerFormat), StatWidth, EditorStyles.label);
            DrawCell(stats.Defense.ToString(IntegerFormat), StatWidth, EditorStyles.label);
            DrawCell(data.AttackSpeed.ToString(SpeedFormat), ShortWidth, EditorStyles.label);
            DrawCell(data.MoveSpeed.ToString(SpeedFormat), ShortWidth, EditorStyles.label);
            DrawCell(data.Range.ToString(IntegerFormat), ShortWidth, EditorStyles.label);
        }
    }

    // 칸 하나를 정해진 너비로 그리고 오른쪽·아래쪽에 표 선을 긋는다.
    private static void DrawCell(string text, float width, GUIStyle style)
    {
        GUILayout.Label(text, style, GUILayout.Width(width), GUILayout.ExpandHeight(true));
        DrawCellGrid(GUILayoutUtility.GetLastRect());
    }

    // 맨 왼쪽 칸: 도감 아이콘을 위에, 이름을 아래에 세로로 쌓고 칸 전체에 표 선을 긋는다.
    private static void DrawNameCell(Sprite icon, string enemyName)
    {
        using (var scope = new EditorGUILayout.VerticalScope(GUILayout.Width(NameWidth), GUILayout.Height(RowHeight)))
        {
            DrawIcon(icon);
            GUILayout.Label(enemyName, EditorStyles.boldLabel, GUILayout.Width(NameWidth));
            DrawCellGrid(scope.rect);
        }
    }

    // 칸 오른쪽 끝과 아래쪽 끝에 얇은 선을 그어 표처럼 보이게 한다. Repaint 단계에서만 그린다.
    private static void DrawCellGrid(Rect cellRect)
    {
        if (Event.current.type != EventType.Repaint) return;

        EditorGUI.DrawRect(new Rect(cellRect.xMax - GridLineThickness, cellRect.y, GridLineThickness, cellRect.height), GridLineColor);
        EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - GridLineThickness, cellRect.width, GridLineThickness), GridLineColor);
    }

    // 도감 아이콘을 정해진 크기로 그린다. Layout·Repaint 단계 모두 칸은 항상 확보하고,
    // 준비된 그림이 있을 때만 그 위에 그려서 두 단계의 컨트롤 개수를 맞춘다.
    private static void DrawIcon(Sprite icon)
    {
        Rect iconRect = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
        if (icon == null) return;

        Texture2D preview = AssetPreview.GetAssetPreview(icon);
        if (preview == null) return;

        GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit);
    }
}
