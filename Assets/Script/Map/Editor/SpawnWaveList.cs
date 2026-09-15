using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 탭. 기본/증원/보스를 구역별로 나눠 카드로 늘어놓고, 고른 카드의 스탯을 보여준다.
///
/// 카드 목록과 상세는 따로 부른다(인스펙터와 하이어라키가 별개 판이듯) — 상세를 카드와 같은
/// 스크롤 안에 두면 카드가 몇 장만 있어도 스크롤을 끝까지 내리거나 올려야 상세가 보인다.
/// 호출하는 쪽(MapMakerWindow)이 카드는 스크롤 영역 안에서, 상세는 그 밖에서 그린다.
///
/// 기본/증원/보스를 한 줄에 섞지 않는다 — 스폰 규칙(스케일 유무)이 서로 달라 섞으면 "왜 여기 낀 거지"가 생긴다.
/// 지역·라운드는 모듈에 저장된 값이 아니다 — 어느 지역인지는 WaveSpawner 인스펙터에만 있어서
/// 사람이 여기서 직접 골라 훑어보는 값이다.
/// </summary>
public static class SpawnWaveList
{
    private const int Card = 92;
    private const int Thumb = 64;

    /// <summary>
    /// 카드 목록만 그린다. 고른 웨이브 행을 돌려준다(안 골랐으면 받은 것 그대로).
    /// 고른 상태는 Entry가 아니라 WaveTable.Data로 주고받는다 — Entry는 이 창이 그려질 때마다
    /// 새로 찍히므로 그걸로 들고 있으면 다음 프레임에 "다른 객체"가 되어 선택이 매번 풀린다.
    /// </summary>
    public static WaveTable.Data DrawCards(SpawnWaveReadout.Groups groups, WaveTable.Data chosenWave)
    {
        if (groups.Total == 0)
        {
            GUILayout.Label($"지역 {groups.Region} · {groups.Round}라운드에 웨이브 데이터가 없습니다.",
                EditorStyles.wordWrappedMiniLabel);
            return chosenWave;
        }

        SpawnWaveReadout.Entry picked = groups.Find(chosenWave);

        picked = DrawSection("기본", groups.Base, picked);
        picked = DrawSection("증원 — 다른 지역 해금 시 이 지역이 보냄 · 스케일 없음", groups.Reinforce, picked);
        picked = DrawSection("보스 라운드 — 10라운드마다 겹쳐 등장 · 스케일 없음", groups.Boss, picked);

        if (picked == null)
        {
            return null;
        }

        return picked.Wave;
    }

    /// <summary>고른 카드의 스탯. 목록이 비어 있으면 아무것도 그리지 않는다.</summary>
    public static void DrawDetail(SpawnWaveReadout.Groups groups, WaveTable.Data chosenWave)
    {
        if (groups.Total == 0)
        {
            return;
        }

        SpawnWaveReadout.Entry entry = groups.Find(chosenWave) ?? groups.First;
        DrawDetail(entry);
    }

    // 구역 하나 — 이름표 한 줄 + 카드를 가로로 늘어놓는다. 목록이 비어 있으면 아예 안 그린다.
    private static SpawnWaveReadout.Entry DrawSection(
        string title, List<SpawnWaveReadout.Entry> entries, SpawnWaveReadout.Entry chosen)
    {
        if (entries.Count == 0)
        {
            return chosen;
        }

        GUILayout.Label(title, EditorStyles.miniBoldLabel);
        SpawnWaveReadout.Entry picked = chosen;

        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (DrawCard(entries[i], entries[i] == chosen))
                {
                    picked = entries[i];
                }
            }

            GUILayout.FlexibleSpace();
        }

        return picked;
    }

    private static bool DrawCard(SpawnWaveReadout.Entry entry, bool chosen)
    {
        Rect slot = GUILayoutUtility.GetRect(Card, Thumb + 34, GUILayout.Width(Card), GUILayout.Height(Thumb + 34));

        var frame = new Rect(slot.x, slot.y, slot.width, Thumb);
        if (chosen)
        {
            EditorGUI.DrawRect(frame, MapMakerPalette.Mark);
        }

        var inner = new Rect(frame.x + 2f, frame.y + 2f, frame.width - 4f, frame.height - 4f);
        EditorGUI.DrawRect(inner, MapMakerPalette.Panel);
        DrawThumb(inner, entry);

        var timeLine = new Rect(slot.x, frame.yMax, slot.width, 15f);
        GUI.Label(timeLine, TimeWord(entry), EditorStyles.miniLabel);

        var nameLine = new Rect(slot.x, timeLine.yMax, slot.width, 15f);
        GUI.Label(nameLine, $"{entry.Wave.MonsterName} ×{entry.Count}", EditorStyles.miniBoldLabel);

        return GUI.Button(slot, GUIContent.none, GUIStyle.none);
    }

    private static void DrawThumb(Rect inner, SpawnWaveReadout.Entry entry)
    {
        GameObject prefab = DataTableManager.WaveTable.GetMonsterPrefab(entry.Wave);
        if (prefab == null)
        {
            return;
        }

        Texture2D shot = AssetPreview.GetAssetPreview(prefab);
        if (shot != null)
        {
            GUI.DrawTexture(inner, shot, ScaleMode.ScaleToFit);
        }
    }

    private static string TimeWord(SpawnWaveReadout.Entry entry)
    {
        if (entry.Wave.SpawnTime <= 0f)
        {
            return "0초";
        }

        return $"{entry.Wave.SpawnTime}초 뒤";
    }

    private static void DrawDetail(SpawnWaveReadout.Entry entry)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (entry.Enemy == null)
            {
                GUILayout.Label($"{entry.Wave.MonsterName} — EnemyTable에 정보 없음", EditorStyles.boldLabel);
                return;
            }

            DrawStats(entry.Enemy);
        }
    }

    private static void DrawStats(EnemyTable.Data data)
    {
        GUILayout.Label($"{data.Name}  ({data.Class})", EditorStyles.boldLabel);
        GUILayout.Label($"체력 {data.Health} · 공격 {data.Attack} · 방어 {data.Defense}", EditorStyles.miniLabel);
        GUILayout.Label($"이속 {data.MoveSpeed} · 사거리 {data.Range} · {data.Type}", EditorStyles.miniLabel);
        DrawIfSet("속성", data.Attribute, "None");
        DrawIfSet("스킬", data.Skills, string.Empty);
    }

    private static void DrawIfSet(string label, string value, string blank)
    {
        if (string.IsNullOrEmpty(value) || value == blank)
        {
            return;
        }

        GUILayout.Label($"{label} {value}", EditorStyles.miniLabel);
    }
}
