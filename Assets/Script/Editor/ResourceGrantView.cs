using UnityEditor;
using UnityEngine;
using VContainer;

// 테스트용으로 자원과 시민을 즉시 지급하는 치트 구역
public static class ResourceGrantView
{
    private const int DefaultResourceAmount = 1000;
    private const int DefaultCitizenAmount = 10;
    private const float GrantButtonHeight = 24f;

    private static int resourceAmount = DefaultResourceAmount;
    private static int citizenAmount = DefaultCitizenAmount;

    // 자원과 시민 지급 구역을 통째로 그린다
    public static void DrawGrantSection(IObjectResolver container)
    {
        ResourcesManager resourcesManager = container.Resolve<ResourcesManager>();
        CitizenManager citizenManager = container.Resolve<CitizenManager>();

        DrawResourceSection(resourcesManager);
        DrawCitizenSection(citizenManager);
    }

    // 자원 지급 수량 칸과 버튼들을 그린다
    private static void DrawResourceSection(ResourcesManager resourcesManager)
    {
        EditorGUILayout.LabelField("자원", EditorStyles.boldLabel);
        resourceAmount = EditorGUILayout.IntField("지급 수량", resourceAmount);

        EditorGUILayout.BeginHorizontal();
        DrawResourceButton(resourcesManager, "나무", ProductionType.Wood);
        DrawResourceButton(resourcesManager, "식량", ProductionType.Food);
        DrawResourceButton(resourcesManager, "골드", ProductionType.Gold);
        DrawResourceButton(resourcesManager, "철", ProductionType.Iron);
        DrawResourceButton(resourcesManager, "석재", ProductionType.Stone);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawSpecialButton(resourcesManager);
        DrawGrantAllButton(resourcesManager);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 자원 한 종류를 지급하는 버튼을 그린다
    private static void DrawResourceButton(ResourcesManager resourcesManager, string buttonLabel, ProductionType productionType)
    {
        if (IsButtonPressed(buttonLabel)) GrantSingleResource(resourcesManager, productionType, resourceAmount);
    }

    // 특수자원을 지급하는 버튼을 그린다
    private static void DrawSpecialButton(ResourcesManager resourcesManager)
    {
        if (IsButtonPressed("특수자원")) GrantSpecialResource(resourcesManager, resourceAmount);
    }

    // 자원 5종을 한 번에 지급하는 버튼을 그린다
    private static void DrawGrantAllButton(ResourcesManager resourcesManager)
    {
        if (IsButtonPressed("자원 전부 지급")) GrantAllResources(resourcesManager, resourceAmount);
    }

    // 시민 추가 수량 칸과 버튼을 그린다
    private static void DrawCitizenSection(CitizenManager citizenManager)
    {
        EditorGUILayout.LabelField("시민", EditorStyles.boldLabel);
        citizenAmount = EditorGUILayout.IntField("추가 수량", citizenAmount);

        if (IsButtonPressed("시민 추가")) GrantCitizen(citizenManager, citizenAmount);
    }

    // 자원 한 종류를 원하는 만큼 더한다
    private static void GrantSingleResource(ResourcesManager resourcesManager, ProductionType productionType, int amount)
    {
        resourcesManager.ProductChanged(new[] { (productionType, amount) });
    }

    // 자원 5종을 같은 수량으로 한 번에 더한다
    private static void GrantAllResources(ResourcesManager resourcesManager, int amount)
    {
        resourcesManager.ProductChanged(new[]
        {
            (ProductionType.Wood, amount),
            (ProductionType.Food, amount),
            (ProductionType.Gold, amount),
            (ProductionType.Iron, amount),
            (ProductionType.Stone, amount),
        });
    }

    // 특수자원만 원하는 만큼 더한다
    private static void GrantSpecialResource(ResourcesManager resourcesManager, int amount)
    {
        resourcesManager.RestoreResources(
            resourcesManager.Wood,
            resourcesManager.Food,
            resourcesManager.Gold,
            resourcesManager.Iron,
            resourcesManager.Stone,
            resourcesManager.Special + amount);
    }

    // 시민을 원하는 만큼 더한다
    private static void GrantCitizen(CitizenManager citizenManager, int amount)
    {
        citizenManager.IncreaseCitizen(amount);
    }

    // 이번 프레임에 그 버튼이 눌렸는지 판단한다
    private static bool IsButtonPressed(string buttonLabel)
    {
        return GUILayout.Button(buttonLabel, GUILayout.Height(GrantButtonHeight));
    }
}
