using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 영웅 강화 결과를 Play Mode 없이 비교하는 창. 입력을 받아 조립을 시키고 표에 넘기기만 한다.
public class HeroSimWindow : EditorWindow
{
    private const string MenuPath = "Tools/Hero/Hero Simulator";
    private const string WindowTitle = "Hero Simulator";
    private const string TierLevelLabel = "티어 강화 LV (미사용)";
    private const string ClassLevelLabel = "직업 강화 LV";
    private const string TitleLabel = "타이틀 강화 칸";
    private const string ReloadLabel = "에셋 다시 읽기";
    private const string AllFilterLabel = "전체";
    private const string MeleeLabel = "근접";
    private const string RangedLabel = "원거리";
    private const string ConditionNote =
        "적을 고르면 그 적의(일차 반영) 방어력을 반영 · 안 고르면 방어력 0 기준 · 강공 재발동은 적 1마리 기준 · 스탠스 교체와 스택 버프, 오라/장판, 디버프는 미반영";
    private const string MissingConfigNote =
        "강화 설정 에셋을 찾지 못했습니다 — Assets/HeroData 아래 Config 두 개를 확인하세요.";

    private const int DisplayLevelOffset = 1;
    private const int MinimumUpgradeCount = 0;
    private const int AllFilterIndex = 0;
    private const int RangedFilterIndex = 2;
    private const float SliderLabelWidth = 96f;

    private List<HeroSimEntry> entries = new();
    private List<BaseUpgradeData> titleUpgrades = new();
    private HeroUpgradeConfig tierConfig;
    private HeroClassUpgradeConfig classConfig;

    private readonly HeroSimInput input = new();
    private readonly HeroSimEnemyPicker enemyPicker = new();
    private List<HeroSimResult> results = new();
    private string[] tierOptions = { AllFilterLabel };
    private readonly string[] typeOptions = { AllFilterLabel, MeleeLabel, RangedLabel };
    private int tierFilterIndex;
    private int typeFilterIndex;
    private Vector2 scroll;

    // 메뉴에서 창을 연다.
    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        GetWindow<HeroSimWindow>(WindowTitle);
    }

    // 창이 켜질 때 에셋을 한 번 읽어둔다.
    private void OnEnable()
    {
        ReloadAssets();
    }

    // 프로젝트의 영웅·설정·타이틀 강화 에셋을 다시 읽고 결과를 새로 만든다.
    private void ReloadAssets()
    {
        entries = HeroSimSource.CollectEntries();
        tierConfig = HeroSimSource.LoadTierConfig();
        classConfig = HeroSimSource.LoadClassConfig();
        titleUpgrades = HeroSimSource.LoadTitleStatUpgrades();
        tierOptions = BuildTierOptions();
        enemyPicker.Load();
        RebuildResults();
    }

    // 화면을 그린다.
    private void OnGUI()
    {
        if (tierConfig == null || classConfig == null)
        {
            EditorGUILayout.HelpBox(MissingConfigNote, MessageType.Warning);
            return;
        }

        DrawControls();
        EditorGUILayout.LabelField(ConditionNote, EditorStyles.wordWrappedMiniLabel);
        HeroSimTable.DrawHeader();
        DrawRows();
    }

    // 필터와 강화 레벨 입력을 그린다. 값이 바뀐 프레임에만 계산을 다시 돌린다.
    private void DrawControls()
    {
        EditorGUI.BeginChangeCheck();

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            tierFilterIndex = EditorGUILayout.Popup(tierFilterIndex, tierOptions, EditorStyles.toolbarPopup);
            typeFilterIndex = EditorGUILayout.Popup(typeFilterIndex, typeOptions, EditorStyles.toolbarPopup);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(ReloadLabel, EditorStyles.toolbarButton)) ReloadAssets();
        }

        EditorGUIUtility.labelWidth = SliderLabelWidth;
        using (new EditorGUI.DisabledScope(true))
        {
            DrawLevelSlider(TierLevelLabel, MinimumUpgradeCount, tierConfig.maxLevel);
        }
        input.TierUpgradeCount = MinimumUpgradeCount;
        input.ClassUpgradeCount = DrawLevelSlider(ClassLevelLabel, input.ClassUpgradeCount, classConfig.maxLevel);
        input.TitleUnlockCount = EditorGUILayout.IntSlider(TitleLabel, input.TitleUnlockCount,
            MinimumUpgradeCount, titleUpgrades.Count);
        enemyPicker.Draw();

        if (EditorGUI.EndChangeCheck()) RebuildResults();
    }

    // 화면에는 LV.1~LV.최대로 보여주고 내부에는 강화 횟수(0부터)로 보관한다.
    private static int DrawLevelSlider(string label, int upgradeCount, int maxLevel)
    {
        int displayed = EditorGUILayout.IntSlider(label, upgradeCount + DisplayLevelOffset,
            DisplayLevelOffset, maxLevel);
        return displayed - DisplayLevelOffset;
    }

    // 계산된 줄들을 스크롤 안에 늘어놓는다.
    private void DrawRows()
    {
        using var scope = new EditorGUILayout.ScrollViewScope(scroll);
        scroll = scope.scrollPosition;
        for (int index = 0; index < results.Count; index++)
        {
            HeroSimTable.DrawRow(results[index]);
        }
    }

    // 현재 입력과 필터로 표에 올릴 결과를 다시 만든다.
    private void RebuildResults()
    {
        results = new List<HeroSimResult>();
        float titleBonus = HeroSimCalc.CalculateTitleBonus(titleUpgrades, input.TitleUnlockCount);
        int enemyDefense = enemyPicker.Defense;
        float enemyHp = enemyPicker.Hp;

        for (int index = 0; index < entries.Count; index++)
        {
            if (!PassesFilter(entries[index])) continue;
            results.Add(HeroSimBuilder.BuildResult(entries[index], tierConfig, classConfig, input, titleBonus, enemyDefense, enemyHp));
        }
    }

    // 필터에 걸리는 영웅인지 확인한다.
    private bool PassesFilter(HeroSimEntry entry)
    {
        if (tierFilterIndex != AllFilterIndex && tierOptions[tierFilterIndex] != entry.HeroData.Tier.ToString()) return false;
        if (typeFilterIndex == AllFilterIndex) return true;
        return HeroSimSource.IsRangedHero(entry.HeroData) == (typeFilterIndex == RangedFilterIndex);
    }

    // 실제 존재하는 티어만 모아 필터 목록을 만든다.
    private string[] BuildTierOptions()
    {
        var tiers = new List<string> { AllFilterLabel };
        for (int index = 0; index < entries.Count; index++)
        {
            string tier = entries[index].HeroData.Tier.ToString();
            if (!tiers.Contains(tier)) tiers.Add(tier);
        }
        return tiers.ToArray();
    }
}
