using UnityEditor;
using UnityEngine;
using VContainer;

// 기지 체력과 지역 확장, 진행 토글, 플레이어 마나를 즉시 바꾸는 치트 구역
public static class BaseStateCheatView
{
    private const int DefaultBaseHp = 20;
    private const float StateButtonHeight = 24f;

    private static int pickedBaseHp = DefaultBaseHp;

    // 기지 상태와 진행 토글, 마나 구역을 통째로 그린다
    public static void DrawStateSection(IObjectResolver container)
    {
        GameManager gameManager = container.Resolve<GameManager>();
        PlayerManaManager manaManager = container.Resolve<PlayerManaManager>();
        MapRegistry mapRegistry = container.Resolve<MapRegistry>();

        DrawBaseHpRow(gameManager);
        DrawUnlockRow(mapRegistry);
        DrawToggleRow(gameManager);
        DrawManaRow(manaManager);
    }

    // 기지 체력을 보여주고 지정하거나 회복하거나 게임오버로 만드는 칸을 그린다
    private static void DrawBaseHpRow(GameManager gameManager)
    {
        EditorGUILayout.LabelField($"기지 체력 (지금 {gameManager.Hp})", EditorStyles.boldLabel);
        pickedBaseHp = EditorGUILayout.IntField("지정할 체력", pickedBaseHp);

        EditorGUILayout.BeginHorizontal();
        DrawHpRestoreButton(gameManager);
        DrawHpFullButton(gameManager);
        DrawGameOverButton(gameManager);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 기지 체력을 입력한 값으로 지정하는 버튼을 그린다
    private static void DrawHpRestoreButton(GameManager gameManager)
    {
        if (IsButtonPressed("체력 지정")) gameManager.RestoreHp(pickedBaseHp);
    }

    // 기지 체력을 시작값으로 전부 회복하는 버튼을 그린다
    private static void DrawHpFullButton(GameManager gameManager)
    {
        if (IsButtonPressed("체력 전부 회복")) gameManager.ResetHpToFull();
    }

    // 기지 체력을 0으로 만들어 게임오버를 재현하는 버튼을 그린다
    private static void DrawGameOverButton(GameManager gameManager)
    {
        if (IsButtonPressed("게임오버")) gameManager.HpDamage(EnemyClass.Boss);
    }

    // 지역 확장 버튼을 그린다
    private static void DrawUnlockRow(MapRegistry mapRegistry)
    {
        EditorGUILayout.LabelField("해금", EditorStyles.boldLabel);
        DrawExpandMapButton(mapRegistry);
        EditorGUILayout.Space();
    }

    // 아직 안 열린 모듈 중 순서상 다음 하나를 즉시 여는 버튼을 그린다
    private static void DrawExpandMapButton(MapRegistry mapRegistry)
    {
        if (IsButtonPressed("지역 확장")) mapRegistry.UnlockNextModule();
    }

    // 건설 가능, 적 스폰 허용, 지역확장 토글을 그린다
    private static void DrawToggleRow(GameManager gameManager)
    {
        EditorGUILayout.LabelField("진행 토글", EditorStyles.boldLabel);
        DrawBuildToggle(gameManager);
        DrawSpawnToggle(gameManager);
        DrawSupportToggle(gameManager);
        EditorGUILayout.Space();
    }

    // 건설 가능 여부 토글을 그리고 바뀐 순간에만 지정한다
    private static void DrawBuildToggle(GameManager gameManager)
    {
        EditorGUI.BeginChangeCheck();
        bool pickedValue = EditorGUILayout.Toggle("건설 가능", gameManager.CanBuild);
        bool valuePicked = EditorGUI.EndChangeCheck();

        if (IsValuePicked(valuePicked)) gameManager.ChangeCanBuild(pickedValue);
    }

    // 적 스폰 허용 여부 토글을 그리고 바뀐 순간에만 지정한다
    private static void DrawSpawnToggle(GameManager gameManager)
    {
        EditorGUI.BeginChangeCheck();
        bool pickedValue = EditorGUILayout.Toggle("적 스폰 허용", gameManager.CanSpawnEnemy);
        bool valuePicked = EditorGUI.EndChangeCheck();

        if (IsValuePicked(valuePicked)) gameManager.ChangeCanSpawnEnemy(pickedValue);
    }

    // 지역확장 여부 토글을 그리고 바뀐 순간에만 지정한다 - 켜두면 다음 결과 화면 전환 때 모듈이 순서대로 하나 자동으로 열린다
    private static void DrawSupportToggle(GameManager gameManager)
    {
        EditorGUI.BeginChangeCheck();
        bool pickedValue = EditorGUILayout.Toggle("지역확장", gameManager.RequestSupport);
        bool valuePicked = EditorGUI.EndChangeCheck();

        if (IsValuePicked(valuePicked)) gameManager.ChangeRequest(pickedValue);
    }

    // 플레이어 마나를 가득 채우는 버튼을 그린다
    private static void DrawManaRow(PlayerManaManager manaManager)
    {
        EditorGUILayout.LabelField($"플레이어 마나 ({manaManager.CurrentMana} / {manaManager.MaxMana})", EditorStyles.boldLabel);

        if (IsButtonPressed("마나 가득")) manaManager.RefillMana();
    }

    // 토글 값이 이번 프레임에 바뀌었는지 판단한다
    private static bool IsValuePicked(bool valuePicked)
    {
        return valuePicked;
    }

    // 이번 프레임에 그 버튼이 눌렸는지 판단한다
    private static bool IsButtonPressed(string buttonLabel)
    {
        return GUILayout.Button(buttonLabel, GUILayout.Height(StateButtonHeight));
    }
}
