using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 시뮬레이터의 "상대 적" 선택 UI와, 고른 적의 일차 반영 방어력 계산을 전담한다.
public class HeroSimEnemyPicker
{
    private const string EnemyLabel = "상대 적";
    private const string EnemyDayLabel = "적 일차";
    private const string NoEnemyLabel = "없음(방어 0)";
    private const string DefenseLabel = "적용 방어력";
    private const string ScopeNote = "Play 모드가 아니면 해금 지역 배율은 반영되지 않습니다(1배 고정)";
    private const int MinimumDayCount = 0;
    private const int NoEnemyIndex = 0;
    private const int NameToEntryOffset = 1;

    private List<EnemyTable.Data> entries = new();
    private string[] options = { NoEnemyLabel };
    private int enemyIndex = NoEnemyIndex;
    private int dayCount;

    public int Defense => ComputeDefense();

    // 고른 적의 일차 반영 체력. 안 골랐으면 0(=TTK 계산에서 "적 없음" 신호로 쓴다).
    public float Hp => ComputeHp();

    // 적 목록을 CSV 표에서 다시 읽는다.
    public void Load()
    {
        entries = HeroSimEnemySource.CollectEntries();
        options = BuildOptions();
    }

    // 적 선택 드롭다운과 일차 입력, 계산된 방어력을 그린다.
    public void Draw()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            enemyIndex = EditorGUILayout.Popup(EnemyLabel, enemyIndex, options);
            dayCount = Mathf.Max(MinimumDayCount, EditorGUILayout.IntField(EnemyDayLabel, dayCount));
        }
        EditorGUILayout.LabelField($"{DefenseLabel} {Defense}");
        EditorGUILayout.LabelField(ScopeNote, EditorStyles.wordWrappedMiniLabel);
    }

    // 적을 안 골랐는지 확인한다.
    private bool IsNoneSelected()
    {
        return enemyIndex < NameToEntryOffset || enemyIndex - NameToEntryOffset >= entries.Count;
    }

    // 고른 적의 일차 반영 방어력을 계산한다.
    private int ComputeDefense()
    {
        if (IsNoneSelected()) return 0;

        EnemyTable.Data data = entries[enemyIndex - NameToEntryOffset];
        EnemyStatScaling.Stats scaled = EnemyStatScaling.Compute(data, data.Class, dayCount);
        return Mathf.RoundToInt(scaled.Defense);
    }

    // 고른 적의 일차 반영 체력을 계산한다.
    private float ComputeHp()
    {
        if (IsNoneSelected()) return 0f;

        EnemyTable.Data data = entries[enemyIndex - NameToEntryOffset];
        EnemyStatScaling.Stats scaled = EnemyStatScaling.Compute(data, data.Class, dayCount);
        return scaled.Hp;
    }

    // 드롭다운에 올릴 이름 목록을 만든다.
    private string[] BuildOptions()
    {
        var built = new List<string> { NoEnemyLabel };
        for (int index = 0; index < entries.Count; index++)
        {
            built.Add(entries[index].Name);
        }
        return built.ToArray();
    }
}
