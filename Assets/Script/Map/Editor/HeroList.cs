using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 아군 탭. 티어별로 근접/원거리를 나눠 카드로 늘어놓고, 고른 카드의 스탯을 보여준다.
///
/// SpawnWaveList와 같은 짝 구조다 — 카드 목록과 상세는 따로 부른다(호출하는 쪽이 카드는 스크롤 안에서,
/// 상세는 그 밖에서 그린다). HeroData는 에셋 참조라 프레임마다 새로 찍히지 않으므로 선택 상태를
/// HeroData 자체로 들고 있어도 안전하다(WaveTable.Data처럼 별도 안정 키가 필요 없다).
/// </summary>
public static class HeroList
{
    private const int Card = 92;
    private const int Thumb = 64;

    /// <summary>카드 목록만 그린다. 고른 유닛을 돌려준다(안 골랐으면 받은 것 그대로).</summary>
    public static HeroData DrawCards(List<HeroReadout.Group> groups, HeroData chosen)
    {
        if (groups.Count == 0)
        {
            GUILayout.Label("아군 유닛 데이터가 없습니다 — Assets/HeroData에 HeroData 에셋이 있는지 확인하세요.",
                EditorStyles.wordWrappedMiniLabel);
            return chosen;
        }

        HeroData picked = chosen;

        for (int i = 0; i < groups.Count; i++)
        {
            picked = DrawSection($"티어 {groups[i].Tier} · 근접", groups[i].Melee, picked);
            picked = DrawSection($"티어 {groups[i].Tier} · 원거리", groups[i].Ranged, picked);
        }

        return picked;
    }

    /// <summary>고른 카드의 스탯. 목록이 비어 있으면 아무것도 그리지 않는다.</summary>
    public static void DrawDetail(List<HeroReadout.Group> groups, HeroData chosen)
    {
        HeroReadout.Entry entry = HeroReadout.Find(groups, chosen);
        if (entry == null)
        {
            return;
        }

        DrawStats(entry);
    }

    // 티어 한 묶음의 근접 또는 원거리 한 줄 — 이름표 한 줄 + 카드를 가로로 늘어놓는다. 비어 있으면 아예 안 그린다.
    private static HeroData DrawSection(string title, List<HeroReadout.Entry> entries, HeroData chosen)
    {
        if (entries.Count == 0)
        {
            return chosen;
        }

        GUILayout.Label(title, EditorStyles.miniBoldLabel);
        HeroData picked = chosen;

        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (DrawCard(entries[i], entries[i].Hero == chosen))
                {
                    picked = entries[i].Hero;
                }
            }

            GUILayout.FlexibleSpace();
        }

        return picked;
    }

    private static bool DrawCard(HeroReadout.Entry entry, bool chosen)
    {
        Rect slot = GUILayoutUtility.GetRect(Card, Thumb + 34, GUILayout.Width(Card), GUILayout.Height(Thumb + 34));

        var frame = new Rect(slot.x, slot.y, slot.width, Thumb);
        if (chosen)
        {
            EditorGUI.DrawRect(frame, MapMakerPalette.Mark);
        }

        var inner = new Rect(frame.x + 2f, frame.y + 2f, frame.width - 4f, frame.height - 4f);
        EditorGUI.DrawRect(inner, MapMakerPalette.Panel);
        DrawThumb(inner, entry.Hero);

        var typeLine = new Rect(slot.x, frame.yMax, slot.width, 15f);
        GUI.Label(typeLine, TypeWord(entry.Hero), EditorStyles.miniLabel);

        var nameLine = new Rect(slot.x, typeLine.yMax, slot.width, 15f);
        GUI.Label(nameLine, entry.Hero.HeroName, EditorStyles.miniBoldLabel);

        return GUI.Button(slot, GUIContent.none, GUIStyle.none);
    }

    private static void DrawThumb(Rect inner, HeroData hero)
    {
        if (hero.Icon == null)
        {
            return;
        }

        Texture2D shot = AssetPreview.GetAssetPreview(hero.Icon);
        if (shot != null)
        {
            GUI.DrawTexture(inner, shot, ScaleMode.ScaleToFit);
        }
    }

    private static string TypeWord(HeroData hero)
    {
        return hero.HeroType == 1 ? "원거리" : "근접";
    }

    private static void DrawStats(HeroReadout.Entry entry)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            GUILayout.Label($"{entry.Hero.HeroName}  (티어 {entry.Hero.Tier} · {TypeWord(entry.Hero)})",
                EditorStyles.boldLabel);

            if (entry.Stat == null)
            {
                GUILayout.Label("StatDataSO를 찾지 못했습니다 — Tools/Hero/Import HeroStatTable을 먼저 실행하세요.",
                    EditorStyles.miniLabel);
                return;
            }

            GUILayout.Label(
                $"체력 {entry.Stat.maxHp} · 공격 {entry.Stat.attackPower} · 방어 {entry.Stat.defence}",
                EditorStyles.miniLabel);
            GUILayout.Label(
                $"SP {entry.Stat.maxSp} · SP회복 {entry.Stat.spRecover} · 쿨감 {entry.Stat.coolDownPer}",
                EditorStyles.miniLabel);
            GUILayout.Label(
                $"공속 {entry.Stat.attackSpeed} · 막기 {entry.Stat.blockCount} · " +
                $"치확 {entry.Stat.criticalPer} · 치피 {entry.Stat.criticalDmg}",
                EditorStyles.miniLabel);
        }
    }
}
