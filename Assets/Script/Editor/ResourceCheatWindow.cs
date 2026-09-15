using System;
using UnityEditor;
using UnityEngine;
using VContainer;

// 테스트용 치트를 탭으로 나눠 보여주는 에디터 전용 창
public class ResourceCheatWindow : EditorWindow
{
    private const string WindowTitle = "자원 치트";
    private const string ScopeSlotLabel = "조립 스코프";
    private const string EditModeText = "게임을 Play로 실행한 뒤에 사용할 수 있습니다.";
    private const string ScopeMissingText = "씬에서 GameLifeTimeScope를 찾지 못했습니다 — 게임 씬이 열려 있는지 확인하거나 위 칸에 직접 물려 주세요.";

    // 탭 이름과 아래 tabDrawers의 순서를 반드시 같게 유지한다
    private static readonly string[] TabLabels = { "자원·시민", "영웅 강화", "진행", "기지" };

    [SerializeField] private GameLifeTimeScope lifeTimeScope;
    [SerializeField] private int tabIndex;

    private Action<IObjectResolver>[] tabDrawers;

    // 메뉴에서 자원 치트 창을 연다
    [MenuItem("Tools/Debug/자원 치트")]
    public static void OpenCheatWindow()
    {
        GetWindow<ResourceCheatWindow>(WindowTitle);
    }

    // 탭 번호와 그리기 담당을 같은 순서로 묶어둔다
    private void OnEnable()
    {
        tabDrawers = new Action<IObjectResolver>[]
        {
            DrawBaseTab,
            HeroUpgradeCheatView.DrawUpgradeSection,
            DayFlowCheatView.DrawFlowSection,
            BaseStateCheatView.DrawStateSection,
        };
    }

    // 실행 상태와 스코프를 확인하고 고른 탭을 그린다
    private void OnGUI()
    {
        AssignScopeOnce();
        DrawScopeSlot();

        if (IsEditMode())
        {
            EditorGUILayout.HelpBox(EditModeText, MessageType.Info);
            return;
        }

        if (IsScopeMissing())
        {
            EditorGUILayout.HelpBox(ScopeMissingText, MessageType.Warning);
            return;
        }

        DrawTabBar();
        DrawSelectedTab(lifeTimeScope.Container);
    }

    // 슬롯이 비어 있을 때만 씬의 조립 스코프를 한 번 물린다
    private void AssignScopeOnce()
    {
        if (IsScopeMissing()) TakeSceneScope();
    }

    // 씬에 있는 조립 스코프를 슬롯에 담는다
    private void TakeSceneScope()
    {
        lifeTimeScope = FindFirstObjectByType<GameLifeTimeScope>();
    }

    // 컨테이너를 꺼낼 조립 스코프를 물리는 칸을 그린다
    private void DrawScopeSlot()
    {
        lifeTimeScope = (GameLifeTimeScope)EditorGUILayout.ObjectField(
            ScopeSlotLabel, lifeTimeScope, typeof(GameLifeTimeScope), true);
    }

    // 탭 이름 줄을 그리고 고른 번호를 기억한다
    private void DrawTabBar()
    {
        tabIndex = GUILayout.Toolbar(tabIndex, TabLabels);
        EditorGUILayout.Space();
    }

    // 고른 탭의 그리기 담당을 호출한다
    private void DrawSelectedTab(IObjectResolver container)
    {
        tabDrawers[tabIndex](container);
    }

    // 자원과 시민 탭을 그린다
    private void DrawBaseTab(IObjectResolver container)
    {
        ResourceGrantView.DrawGrantSection(container);
        ResourceExtraCheatView.DrawExtraSection(container);
    }

    // 지금이 편집 모드인지 판단한다
    private bool IsEditMode()
    {
        return Application.isPlaying == false;
    }

    // 조립 스코프가 아직 안 물렸는지 판단한다
    private bool IsScopeMissing()
    {
        return lifeTimeScope == null;
    }
}
