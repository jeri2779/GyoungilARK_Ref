using UnityEditor;
using UnityEngine;

// 계산 결과를 표로 그리는 출력 담당. 아무것도 계산하지 않고 받은 값만 늘어놓는다.
public static class HeroSimTable
{
    private const float NameWidth = 104f;
    private const float TierWidth = 34f;
    private const float StatWidth = 116f;
    private const float DamageWidth = 74f;
    private const float CostWidth = 96f;
    private const float TraitWidth = 150f;
    private const float RowHeight = 82f;
    private const float IconSize = 48f;

    private const string IntegerFormat = "N0";
    private const string StatFormat = "N1";
    private const string SpeedFormat = "N3";

    private const string NameHeader = "영웅";
    private const string TierHeader = "티어";
    private const string HpHeader = "HP";
    private const string AttackHeader = "ATK";
    private const string DefenceHeader = "DEF";
    private const string SpeedHeader = "AS";
    private const string BlockHeader = "BLK";
    private const string NormalHeader = "평타 1타";
    private const string AverageHeader = "평균 1타";
    private const string DpsHeader = "초당 피해";
    private const string KillHeader = "처치 시간";
    private const string CostHeader = "누적 비용";
    private const string TraitHeader = "트레잇";

    private const float GridLineThickness = 1f;
    private static readonly Color GridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    // 표의 머리줄을 그린다.
    public static void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            DrawCell(NameHeader, NameWidth, EditorStyles.boldLabel);
            DrawCell(TierHeader, TierWidth, EditorStyles.boldLabel);
            DrawCell(HpHeader, StatWidth, EditorStyles.boldLabel);
            DrawCell(AttackHeader, StatWidth, EditorStyles.boldLabel);
            DrawCell(DefenceHeader, StatWidth, EditorStyles.boldLabel);
            DrawCell(SpeedHeader, StatWidth, EditorStyles.boldLabel);
            DrawCell(BlockHeader, TierWidth, EditorStyles.boldLabel);
            DrawCell(NormalHeader, DamageWidth, EditorStyles.boldLabel);
            DrawCell(AverageHeader, DamageWidth, EditorStyles.boldLabel);
            DrawCell(DpsHeader, DamageWidth, EditorStyles.boldLabel);
            DrawCell(KillHeader, DamageWidth, EditorStyles.boldLabel);
            DrawCell(CostHeader, CostWidth, EditorStyles.boldLabel);
            DrawCell(TraitHeader, TraitWidth, EditorStyles.boldLabel);
        }
    }

    // 결과 한 줄을 그린다.
    public static void DrawRow(HeroSimResult result)
    {
        using (new EditorGUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
        {
            DrawNameCell(result.Icon, result.HeroName);
            DrawCell(result.Tier.ToString(), TierWidth, EditorStyles.label);
            DrawStatCell(result.MaxHp.ToString(IntegerFormat), result.HpGrowth, StatWidth);
            DrawStatCell(result.AttackPower.ToString(StatFormat), result.AttackGrowth, StatWidth);
            DrawStatCell(result.Defence.ToString(StatFormat), result.DefenceGrowth, StatWidth);
            DrawStatCell(result.AttackSpeed.ToString(SpeedFormat), result.AttackSpeedGrowth, StatWidth);
            DrawCell(result.BlockCount.ToString(IntegerFormat), TierWidth, EditorStyles.label);
            DrawCell(result.NormalHitDamage.ToString(IntegerFormat), DamageWidth, EditorStyles.label);
            DrawCell(result.AverageHitDamage.ToString(StatFormat), DamageWidth, EditorStyles.label);
            DrawCell(result.DamagePerSecond.ToString(IntegerFormat), DamageWidth, EditorStyles.label);
            DrawCell(result.TimeToKill, DamageWidth, EditorStyles.label);
            DrawCell(result.CumulativeCost.ToString(IntegerFormat), CostWidth, EditorStyles.label);
            DrawCell(result.TraitNote, TraitWidth, EditorStyles.label);
        }
    }

    // 칸 하나를 정해진 너비로 그리고 오른쪽·아래쪽에 표 선을 긋는다.
    private static void DrawCell(string text, float width, GUIStyle style)
    {
        GUILayout.Label(text, style, GUILayout.Width(width), GUILayout.ExpandHeight(true));
        DrawCellGrid(GUILayoutUtility.GetLastRect());
    }

    // 스탯 칸: 값을 위에, 강화 0단계 대비 증가율을 아래에 세로로 쌓고 칸 전체에 표 선을 긋는다.
    private static void DrawStatCell(string valueText, string growthText, float width)
    {
        using (var scope = new EditorGUILayout.VerticalScope(GUILayout.Width(width), GUILayout.Height(RowHeight)))
        {
            GUILayout.Label(valueText, EditorStyles.label, GUILayout.Width(width));
            GUILayout.Label(growthText, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(width));
            DrawCellGrid(scope.rect);
        }
    }

    // 맨 왼쪽 칸: 아이콘을 위에, 이름을 아래에 세로로 쌓고 칸 전체에 표 선을 긋는다.
    private static void DrawNameCell(Sprite icon, string heroName)
    {
        using (var scope = new EditorGUILayout.VerticalScope(GUILayout.Width(NameWidth), GUILayout.Height(RowHeight)))
        {
            DrawIcon(icon);
            GUILayout.Label(heroName, EditorStyles.boldLabel, GUILayout.Width(NameWidth));
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

    // 아이콘 미리보기를 정해진 크기로 그린다. Layout·Repaint 단계 모두 칸은 항상 확보하고,
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
