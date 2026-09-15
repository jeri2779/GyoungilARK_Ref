using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 적 스탯을 Play Mode 없이 비교하는 창. 입력을 받아 EnemyStatScaling에 계산을 맡기고 표에 넘기기만 한다.
public class EnemySimWindow : EditorWindow
{
    private const string MenuPath = "Tools/Enemy/Enemy Simulator";
    private const string WindowTitle = "Enemy Simulator";
    private const string DayLabel = "일차(Day)";
    private const string ReloadLabel = "에셋 다시 읽기";
    private const string AllFilterLabel = "전체";
    private const string ConditionNote =
        "Play 모드가 아니면 해금 지역 배율은 반영되지 않습니다(1배 고정) · AttackSpeed·이속·사거리는 일차 배율이 없어 원본 그대로 표시";
    private const string MissingTableNote = "적 데이터를 찾지 못했습니다 — Assets/Resources/DataTable/EnemyTable.csv를 확인하세요.";
    private const string IconResourcePath = "EnemyIcons";

    private const int MinimumDayCount = 0;
    private const float DayLabelWidth = 96f;
    private const float DayFieldWidth = 160f;
    private const float DayStepButtonWidth = 40f;
    private const int AllFilterIndex = 0;

    // 일차를 한 번에 건너뛰는 버튼들. 값이 커질수록 필요한 버튼도 커진다.
    private static readonly int[] DaySteps = { 1, 10, 50, 100 };

    // 한 줄 = 적 하나. 도감 아이콘(EnemyIconBaker가 구운 Resources/EnemyIcons)까지 같이 들고 있는다.
    private class Row
    {
        public EnemyTable.Data Data;
        public Sprite Icon;
    }

    private List<Row> rows = new();
    private int dayCount;
    private Vector2 scroll;

    private string[] typeOptions = { AllFilterLabel };
    private string[] classOptions = { AllFilterLabel };
    private string[] attributeOptions = { AllFilterLabel };
    private int typeFilterIndex;
    private int classFilterIndex;
    private int attributeFilterIndex;

    // 메뉴에서 창을 연다.
    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        GetWindow<EnemySimWindow>(WindowTitle);
    }

    // 창이 켜질 때 적 데이터를 한 번 읽어둔다.
    private void OnEnable()
    {
        ReloadEntries();
    }

    // 프로젝트의 적 데이터와 도감 아이콘을 다시 읽고 분류 탭 목록도 다시 만든다.
    private void ReloadEntries()
    {
        rows = new List<Row>();
        List<EnemyTable.Data> entries = HeroSimEnemySource.CollectEntries();
        for (int index = 0; index < entries.Count; index++)
        {
            rows.Add(new Row
            {
                Data = entries[index],
                Icon = Resources.Load<Sprite>($"{IconResourcePath}/{entries[index].Name}"),
            });
        }

        typeOptions = BuildOptions(data => data.Type);
        classOptions = BuildOptions(data => data.Class);
        attributeOptions = BuildOptions(data => data.Attribute);
    }

    // 화면을 그린다.
    private void OnGUI()
    {
        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox(MissingTableNote, MessageType.Warning);
            return;
        }

        DrawControls();
        EditorGUILayout.LabelField(ConditionNote, EditorStyles.wordWrappedMiniLabel);
        EnemySimTable.DrawHeader();
        DrawRows();
    }

    // 타입/등급/속성 분류 탭과 새로고침 버튼, 일차 슬라이더를 그린다.
    private void DrawControls()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            typeFilterIndex = EditorGUILayout.Popup(typeFilterIndex, typeOptions, EditorStyles.toolbarPopup);
            classFilterIndex = EditorGUILayout.Popup(classFilterIndex, classOptions, EditorStyles.toolbarPopup);
            attributeFilterIndex = EditorGUILayout.Popup(attributeFilterIndex, attributeOptions, EditorStyles.toolbarPopup);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(ReloadLabel, EditorStyles.toolbarButton)) ReloadEntries();
        }

        EditorGUIUtility.labelWidth = DayLabelWidth;
        using (new EditorGUILayout.HorizontalScope())
        {
            dayCount = Mathf.Max(MinimumDayCount, EditorGUILayout.IntField(DayLabel, dayCount, GUILayout.Width(DayFieldWidth)));
            DrawDayStepButtons();
        }
    }

    // "+1"·"+10"처럼 일차를 한 번에 올리는 버튼들을 그린다.
    private void DrawDayStepButtons()
    {
        for (int index = 0; index < DaySteps.Length; index++)
        {
            int step = DaySteps[index];
            if (GUILayout.Button($"+{step}", EditorStyles.miniButton, GUILayout.Width(DayStepButtonWidth))) dayCount += step;
        }
    }

    // 필터를 통과한 적만 일차 배율을 계산해 표에 늘어놓는다.
    private void DrawRows()
    {
        using var scope = new EditorGUILayout.ScrollViewScope(scroll);
        scroll = scope.scrollPosition;

        for (int index = 0; index < rows.Count; index++)
        {
            Row row = rows[index];
            if (!PassesFilter(row.Data)) continue;

            EnemyStatScaling.Stats stats = EnemyStatScaling.Compute(row.Data, row.Data.Class, dayCount);
            EnemySimTable.DrawRow(row.Data, row.Icon, stats);
        }
    }

    // 타입·등급·속성 필터에 전부 걸리는 적인지 확인한다.
    private bool PassesFilter(EnemyTable.Data data)
    {
        if (typeFilterIndex != AllFilterIndex && typeOptions[typeFilterIndex] != data.Type) return false;
        if (classFilterIndex != AllFilterIndex && classOptions[classFilterIndex] != data.Class) return false;
        if (attributeFilterIndex != AllFilterIndex && attributeOptions[attributeFilterIndex] != data.Attribute) return false;
        return true;
    }

    // 실제 데이터에 존재하는 값만 모아 "전체" 뒤에 붙인 분류 목록을 만든다.
    private string[] BuildOptions(System.Func<EnemyTable.Data, string> pickField)
    {
        var options = new List<string> { AllFilterLabel };
        for (int index = 0; index < rows.Count; index++)
        {
            string value = pickField(rows[index].Data);
            if (!options.Contains(value)) options.Add(value);
        }
        return options.ToArray();
    }
}
