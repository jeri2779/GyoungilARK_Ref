using UnityEditor;
using UnityEngine;
using VContainer;

// 자원과 시민을 더하는 대신 값 자체를 지정하거나 되돌리는 치트 구역
public static class ResourceExtraCheatView
{
    private const int DefaultExactAmount = 1000000;
    private const int DefaultMaxCitizenAmount = 10;
    private const int EmptyUsedCitizen = 0;
    private const float ExtraButtonHeight = 24f;

    private static int exactAmount = DefaultExactAmount;
    private static int maxCitizenAmount = DefaultMaxCitizenAmount;

    // 자원 정밀 조정과 시민 상한 조정 구역을 통째로 그린다
    public static void DrawExtraSection(IObjectResolver container)
    {
        ResourcesManager resourcesManager = container.Resolve<ResourcesManager>();
        CitizenManager citizenManager = container.Resolve<CitizenManager>();

        DrawExactRow(resourcesManager);
        DrawRestoreRow(resourcesManager, citizenManager);
        DrawMaxCitizenRow(citizenManager);
    }

    // 자원 6종을 같은 값으로 덮어쓰는 칸과 버튼을 그린다
    private static void DrawExactRow(ResourcesManager resourcesManager)
    {
        EditorGUILayout.LabelField("자원 정밀 조정", EditorStyles.boldLabel);
        exactAmount = EditorGUILayout.IntField("지정할 값", exactAmount);

        if (IsButtonPressed("자원 6종을 이 값으로 지정")) SetExactResources(resourcesManager, exactAmount);
    }

    // 자원과 시민을 시작값으로 되돌리는 버튼을 그린다
    private static void DrawRestoreRow(ResourcesManager resourcesManager, CitizenManager citizenManager)
    {
        EditorGUILayout.BeginHorizontal();
        DrawResourceRestoreButton(resourcesManager);
        DrawCitizenRestoreButton(citizenManager);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 자원만 시작값으로 되돌리는 버튼을 그린다
    private static void DrawResourceRestoreButton(ResourcesManager resourcesManager)
    {
        if (IsButtonPressed("자원 시작값 복귀")) RestoreStartResources(resourcesManager);
    }

    // 시민만 시작값으로 되돌리는 버튼을 그린다
    private static void DrawCitizenRestoreButton(CitizenManager citizenManager)
    {
        if (IsButtonPressed("시민 시작값 복귀")) RestoreStartCitizen(citizenManager);
    }

    // 시민 상한 증가 칸과 사용량 초기화 버튼을 그린다
    private static void DrawMaxCitizenRow(CitizenManager citizenManager)
    {
        EditorGUILayout.LabelField($"시민 상한 (현재 {citizenManager.CurrentCitizen}/{citizenManager.MaxCitizen} · 사용 {citizenManager.UsedCitizen})", EditorStyles.boldLabel);
        maxCitizenAmount = EditorGUILayout.IntField("상한 증가량", maxCitizenAmount);

        EditorGUILayout.BeginHorizontal();
        DrawMaxCitizenButton(citizenManager);
        DrawUsedCitizenButton(citizenManager);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 시민 상한을 늘리는 버튼을 그린다
    private static void DrawMaxCitizenButton(CitizenManager citizenManager)
    {
        if (IsButtonPressed("상한 늘리기")) AddMaxCitizen(citizenManager, maxCitizenAmount);
    }

    // 시민 사용량을 비우는 버튼을 그린다
    private static void DrawUsedCitizenButton(CitizenManager citizenManager)
    {
        if (IsButtonPressed("사용량 비우기")) ClearUsedCitizen(citizenManager);
    }

    // 자원 6종을 같은 값으로 덮어쓴다
    private static void SetExactResources(ResourcesManager resourcesManager, int amount)
    {
        resourcesManager.RestoreResources(amount, amount, amount, amount, amount, amount);
    }

    // 자원을 이번 판 시작값으로 되돌린다
    private static void RestoreStartResources(ResourcesManager resourcesManager)
    {
        resourcesManager.Reset();
    }

    // 시민 수와 사용량을 이번 판 시작값으로 되돌린다
    private static void RestoreStartCitizen(CitizenManager citizenManager)
    {
        citizenManager.Reset();
    }

    // 시민 상한을 원하는 만큼 늘린다
    private static void AddMaxCitizen(CitizenManager citizenManager, int amount)
    {
        citizenManager.IncreaseMaxCitizen(amount);
    }

    // 건물과 영웅이 잡아둔 시민 사용량을 전부 비운다
    private static void ClearUsedCitizen(CitizenManager citizenManager)
    {
        citizenManager.RestoreUsedCitizen(EmptyUsedCitizen, EmptyUsedCitizen);
    }

    // 이번 프레임에 그 버튼이 눌렸는지 판단한다
    private static bool IsButtonPressed(string buttonLabel)
    {
        return GUILayout.Button(buttonLabel, GUILayout.Height(ExtraButtonHeight));
    }
}
