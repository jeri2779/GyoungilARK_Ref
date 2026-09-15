using UnityEditor;
using UnityEngine;
using VContainer;

// 낮과 밤, 일차, 웨이브, 시간 배속을 즉시 바꾸는 치트 구역
public static class DayFlowCheatView
{
    private const float NormalTimeScale = 1f;
    private const float DoubleTimeScale = 2f;
    private const float FastTimeScale = 5f;
    private const float FlowButtonHeight = 24f;

    private static int pickedDayCount;

    // 낮밤 전환과 일차, 웨이브, 배속 구역을 통째로 그린다
    public static void DrawFlowSection(IObjectResolver container)
    {
        GameManager gameManager = container.Resolve<GameManager>();

        DrawDayNightRow(gameManager);
        DrawDayCountRow(gameManager);
        DrawWaveRow(gameManager);
        DrawTimeScaleRow();
    }

    // 낮과 밤으로 강제 전환하는 버튼들을 그린다
    private static void DrawDayNightRow(GameManager gameManager)
    {
        EditorGUILayout.LabelField($"낮밤 전환 (지금 건설가능 = {gameManager.CanBuild})", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawDayButton(gameManager);
        DrawNightButton(gameManager);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 낮으로 전환하는 버튼을 그린다
    private static void DrawDayButton(GameManager gameManager)
    {
        if (IsButtonPressed("낮으로")) gameManager.OnDay();
    }

    // 밤으로 전환하는 버튼을 그린다
    private static void DrawNightButton(GameManager gameManager)
    {
        if (IsButtonPressed("밤으로")) gameManager.OnNight();
    }

    // 현재 일차를 보여주고 올리거나 지정하는 칸을 그린다
    private static void DrawDayCountRow(GameManager gameManager)
    {
        EditorGUILayout.LabelField($"일차 (지금 {gameManager.DayCount}일차)", EditorStyles.boldLabel);
        pickedDayCount = EditorGUILayout.IntField("지정할 일차", pickedDayCount);

        EditorGUILayout.BeginHorizontal();
        DrawDayIncreaseButton(gameManager);
        DrawDayRestoreButton(gameManager);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 일차를 하나 올리는 버튼을 그린다
    private static void DrawDayIncreaseButton(GameManager gameManager)
    {
        if (IsButtonPressed("일차 +1")) gameManager.IncreaseDayCount();
    }

    // 일차를 입력한 값으로 지정하는 버튼을 그린다
    private static void DrawDayRestoreButton(GameManager gameManager)
    {
        if (IsButtonPressed("일차 지정")) gameManager.RestoreDayCount(pickedDayCount);
    }

    // 현재 일차 기준으로 웨이브를 즉시 소환하는 버튼을 그린다
    private static void DrawWaveRow(GameManager gameManager)
    {
        EditorGUILayout.LabelField($"웨이브 (스폰허용 = {gameManager.CanSpawnEnemy})", EditorStyles.boldLabel);

        if (IsButtonPressed("웨이브 즉시 소환")) gameManager.SpawnEnemy();
        EditorGUILayout.Space();
    }

    // 시간 배속 버튼들을 그린다
    private static void DrawTimeScaleRow()
    {
        EditorGUILayout.LabelField($"시간 배속 (지금 x{Time.timeScale})", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawTimeScaleButton("x1", NormalTimeScale);
        DrawTimeScaleButton("x2", DoubleTimeScale);
        DrawTimeScaleButton("x5", FastTimeScale);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 배속 하나를 지정하는 버튼을 그린다
    private static void DrawTimeScaleButton(string buttonLabel, float scale)
    {
        if (IsButtonPressed(buttonLabel)) SetTimeScale(scale);
    }

    // 게임 시간 배속을 지정한다
    private static void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
    }

    // 이번 프레임에 그 버튼이 눌렸는지 판단한다
    private static bool IsButtonPressed(string buttonLabel)
    {
        return GUILayout.Button(buttonLabel, GUILayout.Height(FlowButtonHeight));
    }
}
