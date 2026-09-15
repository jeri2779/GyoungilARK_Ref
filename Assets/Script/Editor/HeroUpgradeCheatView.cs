using UnityEditor;
using UnityEngine;
using VContainer;

// 영웅 티어 강화와 직업 강화 레벨을 자원 소모 없이 즉시 지정하는 치트 구역
public static class HeroUpgradeCheatView
{
    private const int FirstTier = 1;
    private const int SecondTier = 2;
    private const int ThirdTier = 3;
    private const int FourthTier = 4;
    private const int MeleeHeroType = 0;
    private const int RangedHeroType = 1;
    private const int LowestLevel = 0;
    private const int LevelDisplayOffset = 1;
    private const int TopLevelOffset = 1;
    private const float UpgradeButtonHeight = 24f;

    // 티어 강화와 직업 강화 구역을 통째로 그린다
    public static void DrawUpgradeSection(IObjectResolver container)
    {
        HeroTierUpgradeState tierState = container.Resolve<HeroTierUpgradeState>();
        HeroClassUpgradeState classState = container.Resolve<HeroClassUpgradeState>();

        EditorGUILayout.LabelField("티어 강화 (비용 0 · 즉시 반영)", EditorStyles.boldLabel);
        DrawTierSlider(tierState, "티어 1", FirstTier);
        DrawTierSlider(tierState, "티어 2", SecondTier);
        DrawTierSlider(tierState, "티어 3", ThirdTier);
        DrawTierSlider(tierState, "티어 4", FourthTier);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("직업 강화 (비용 0 · 즉시 반영)", EditorStyles.boldLabel);
        DrawClassSlider(classState, "근접", MeleeHeroType);
        DrawClassSlider(classState, "원거리", RangedHeroType);

        EditorGUILayout.Space();
        DrawBulkRow(tierState, classState);
    }

    // 티어 하나의 강화 레벨 슬라이더를 그리고 값이 바뀐 순간에만 지정한다
    private static void DrawTierSlider(HeroTierUpgradeState tierState, string sliderLabel, int tier)
    {
        int currentLevel = tierState.GetLevel(tier);
        string levelLabel = $"{sliderLabel}  LV.{currentLevel + LevelDisplayOffset}";

        EditorGUI.BeginChangeCheck();
        int pickedLevel = EditorGUILayout.IntSlider(levelLabel, currentLevel, LowestLevel, TopLevelOf(tierState.MaxLevel));
        bool levelPicked = EditorGUI.EndChangeCheck();

        if (IsLevelPicked(levelPicked)) tierState.RestoreLevel(tier, pickedLevel);
    }

    // 직업 하나의 강화 레벨 슬라이더를 그리고 값이 바뀐 순간에만 지정한다
    private static void DrawClassSlider(HeroClassUpgradeState classState, string sliderLabel, int heroType)
    {
        int currentLevel = classState.GetLevel(heroType);
        string levelLabel = $"{sliderLabel}  LV.{currentLevel + LevelDisplayOffset}";

        EditorGUI.BeginChangeCheck();
        int pickedLevel = EditorGUILayout.IntSlider(levelLabel, currentLevel, LowestLevel, TopLevelOf(classState.MaxLevel));
        bool levelPicked = EditorGUI.EndChangeCheck();

        if (IsLevelPicked(levelPicked)) classState.RestoreLevel(heroType, pickedLevel);
    }

    // 전부 만렙과 전부 초기화 버튼을 나란히 그린다
    private static void DrawBulkRow(HeroTierUpgradeState tierState, HeroClassUpgradeState classState)
    {
        EditorGUILayout.BeginHorizontal();
        DrawMaxAllButton(tierState, classState);
        DrawClearAllButton(tierState, classState);
        EditorGUILayout.EndHorizontal();
    }

    // 강화를 전부 만렙으로 만드는 버튼을 그린다
    private static void DrawMaxAllButton(HeroTierUpgradeState tierState, HeroClassUpgradeState classState)
    {
        if (IsButtonPressed("전부 만렙")) SetAllLevels(tierState, classState, TopLevelOf(tierState.MaxLevel), TopLevelOf(classState.MaxLevel));
    }

    // 강화를 전부 0으로 되돌리는 버튼을 그린다
    private static void DrawClearAllButton(HeroTierUpgradeState tierState, HeroClassUpgradeState classState)
    {
        if (IsButtonPressed("전부 초기화")) SetAllLevels(tierState, classState, LowestLevel, LowestLevel);
    }

    // 티어 4종과 직업 2종의 강화 레벨을 한 번에 지정한다
    private static void SetAllLevels(HeroTierUpgradeState tierState, HeroClassUpgradeState classState, int tierLevel, int classLevel)
    {
        tierState.RestoreLevel(FirstTier, tierLevel);
        tierState.RestoreLevel(SecondTier, tierLevel);
        tierState.RestoreLevel(ThirdTier, tierLevel);
        tierState.RestoreLevel(FourthTier, tierLevel);
        classState.RestoreLevel(MeleeHeroType, classLevel);
        classState.RestoreLevel(RangedHeroType, classLevel);
    }

    // 설정된 최대 레벨에서 실제로 넣을 수 있는 마지막 내부 레벨을 계산한다
    private static int TopLevelOf(int maxLevel)
    {
        return maxLevel - TopLevelOffset;
    }

    // 슬라이더 값이 이번 프레임에 바뀌었는지 판단한다
    private static bool IsLevelPicked(bool levelPicked)
    {
        return levelPicked;
    }

    // 이번 프레임에 그 버튼이 눌렸는지 판단한다
    private static bool IsButtonPressed(string buttonLabel)
    {
        return GUILayout.Button(buttonLabel, GUILayout.Height(UpgradeButtonHeight));
    }
}
