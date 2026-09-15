#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TMP_Text에 LocalizeText를 일괄로 붙여주는 에디터 툴.
///
/// 키는 손으로 입력하지 않는다 — StringTable.csv의 Kr 열을 역인덱싱해서,
/// 지금 화면에 적혀 있는 한글과 정확히 일치하는 행의 ID를 키로 자동 지정한다.
///
/// "Kr 원문과 정확히 일치할 때만 붙인다"는 규칙이 안전장치 역할을 한다.
/// 코드가 런타임에 .text로 채우는 TMP(WaveSpawner의 웨이브 정보, EnemyInfo의 이름·설명 등)는
/// 프리팹 상태에서 비어 있거나 플레이스홀더라 매칭되지 않고 자동으로 걸러진다.
/// 거기에 LocalizeText가 붙으면 언어 전환 때 코드가 채운 내용을 덮어써 버린다.
/// </summary>
public static class LocalizeTextBinder
{
    private const string CsvPath = "Assets/Resources/DataTable/StringTable.csv";
    private const string Ambiguous = "\0AMBIGUOUS\0"; // Kr이 여러 키에 중복될 때의 표식

    [MenuItem("Tools/Localize/선택 항목 검사 (드라이런)", priority = 0)]
    public static void DryRun() => Run(false);

    [MenuItem("Tools/Localize/선택 항목에 LocalizeText 부착", priority = 1)]
    public static void Apply() => Run(true);

    [MenuItem("Tools/Localize/열린 씬의 LocalizeText 키 검사", priority = 20)]
    public static void ValidateKeys()
    {
        if (!TryLoadTable(out _, out HashSet<string> ids)) return;

        var log = new StringBuilder("[LocalizeTextBinder] 키 검사\n");
        int bad = 0, empty = 0, ok = 0;

        foreach (LocalizeText lt in Object.FindObjectsByType<LocalizeText>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string key = new SerializedObject(lt).FindProperty("key").stringValue;
            if (string.IsNullOrEmpty(key)) { empty++; log.AppendLine($"  [키 비어있음] {Path(lt.transform)}"); }
            else if (!ids.Contains(key)) { bad++; log.AppendLine($"  [테이블에 없음] {Path(lt.transform)} → \"{key}\""); }
            else ok++;
        }

        log.AppendLine($"\n정상 {ok} / 키 없음 {empty} / 테이블에 없는 키 {bad}");
        if (bad + empty > 0) Debug.LogWarning(log.ToString());
        else Debug.Log(log.ToString());
    }

    private static void Run(bool apply)
    {
        if (!TryLoadTable(out Dictionary<string, string> krToId, out _)) return;

        Object[] selection = Selection.objects;
        if (selection == null || selection.Length == 0)
        {
            Debug.LogWarning("[LocalizeTextBinder] 선택된 항목이 없습니다. 하이어라키의 오브젝트나 프로젝트의 프리팹을 선택하세요.");
            return;
        }

        var log = new StringBuilder($"[LocalizeTextBinder] {(apply ? "부착 실행" : "드라이런 — 실제로 바꾸지 않음")}\n");
        var stat = new Stat();

        foreach (Object obj in selection)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            bool isPrefabAsset = !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");

            if (isPrefabAsset)
            {
                // 프리팹 에셋은 임시로 열어서 수정한 뒤 저장한다(인스턴스 오버라이드가 아니라 원본에 반영).
                GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    int before = stat.Bound;
                    Process(root, krToId, apply, useUndo: false, log, stat);
                    if (apply && stat.Bound > before) PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            else if (obj is GameObject go)
            {
                Process(go, krToId, apply, useUndo: true, log, stat);
            }
        }

        log.AppendLine($"\n{(apply ? "부착" : "부착 가능")} {stat.Bound} / 이미 있음 {stat.AlreadyHas} / 빈 텍스트 {stat.EmptyText} / 매칭 실패 {stat.Unmatched} / Kr 중복 {stat.AmbiguousCount}");
        if (!apply) log.AppendLine("실제로 붙이려면 Tools/Localize/선택 항목에 LocalizeText 부착 을 실행하세요.");
        Debug.Log(log.ToString());
    }

    private static void Process(GameObject root, Dictionary<string, string> krToId,
                                bool apply, bool useUndo, StringBuilder log, Stat stat)
    {
        foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponent<LocalizeText>() != null) { stat.AlreadyHas++; continue; }

            string text = Normalize(tmp.text);
            if (string.IsNullOrEmpty(text)) { stat.EmptyText++; continue; } // 코드가 채우는 동적 텍스트 — 건드리지 않는다

            if (!krToId.TryGetValue(text, out string key))
            {
                // 리치텍스트 태그가 섞여 있으면 태그를 벗긴 형태로 한 번 더 시도한다.
                string bare = text.IndexOf('<') >= 0 ? StripTags(text) : null;
                if (string.IsNullOrEmpty(bare) || !krToId.TryGetValue(bare, out key))
                {
                    stat.Unmatched++;
                    log.AppendLine($"  [매칭 실패] {Path(tmp.transform)} → \"{Shorten(text)}\"");
                    continue;
                }
            }
            if (key == Ambiguous)
            {
                stat.AmbiguousCount++;
                log.AppendLine($"  [Kr 중복] {Path(tmp.transform)} → \"{Shorten(text)}\" (키를 특정할 수 없음, 수동 지정 필요)");
                continue;
            }

            stat.Bound++;
            log.AppendLine($"  [{(apply ? "부착" : "부착 예정")}] {Path(tmp.transform)} → {key}");
            if (!apply) continue;

            LocalizeText lt = useUndo
                ? Undo.AddComponent<LocalizeText>(tmp.gameObject)
                : tmp.gameObject.AddComponent<LocalizeText>();

            // key는 private [SerializeField]라 SerializedObject로 넣는다(더티 처리·프리팹 반영까지 함께).
            var so = new SerializedObject(lt);
            so.FindProperty("key").stringValue = key;
            if (useUndo) so.ApplyModifiedProperties();
            else so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // StringTable.csv를 읽어 Kr → ID 역인덱스와 ID 집합을 만든다.
    // En/Jp에 쉼표가 들어가도 안전하도록 앞의 두 칸만 잘라 쓴다.
    private static bool TryLoadTable(out Dictionary<string, string> krToId, out HashSet<string> ids)
    {
        krToId = new Dictionary<string, string>();
        ids = new HashSet<string>();

        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"[LocalizeTextBinder] '{CsvPath}' 를 찾을 수 없습니다.");
            return false;
        }

        string[] lines = File.ReadAllLines(CsvPath, Encoding.UTF8);
        for (int i = 1; i < lines.Length; i++) // 0행은 헤더
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int c1 = line.IndexOf(',');
            if (c1 < 0) continue;
            int c2 = line.IndexOf(',', c1 + 1);
            if (c2 < 0) continue;

            string id = line.Substring(0, c1).Trim();
            string kr = Normalize(line.Substring(c1 + 1, c2 - c1 - 1));
            if (id.Length == 0) continue;

            ids.Add(id);
            if (kr.Length == 0) continue;

            // 같은 한글이 여러 키에 있으면 자동 지정이 불가능하므로 표식만 남긴다.
            krToId[kr] = krToId.ContainsKey(kr) ? Ambiguous : id;
        }

        if (ids.Count == 0)
        {
            Debug.LogError("[LocalizeTextBinder] StringTable에서 읽어온 행이 없습니다.");
            return false;
        }
        return true;
    }

    // CSV의 리터럴 "\n"과 TMP의 실제 개행을 같은 형태로 맞춰 비교한다.
    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Replace("\\n", "\n").Replace("\r\n", "\n").Replace('\r', '\n');
        // 폭 없는 문자(ZWSP·BOM 등)는 눈에 안 보이지만 Trim에 안 걸린다.
        // 지우지 않으면 사실상 빈 텍스트가 '매칭 실패'로 잡혀 리포트를 더럽힌다.
        s = s.Replace("​", string.Empty).Replace("‌", string.Empty)
             .Replace("‍", string.Empty).Replace("﻿", string.Empty);
        return s.Trim();
    }

    // <b>, <color=...> 같은 TMP 리치텍스트 태그를 벗겨낸 형태로도 한 번 더 대조한다.
    private static readonly System.Text.RegularExpressions.Regex TagPattern =
        new System.Text.RegularExpressions.Regex("<[^>]+>");

    private static string StripTags(string s) => Normalize(TagPattern.Replace(s, string.Empty));

    private static string Shorten(string s)
    {
        s = s.Replace("\n", "\\n");
        return s.Length <= 30 ? s : s.Substring(0, 30) + "...";
    }

    private static string Path(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private class Stat
    {
        public int Bound, AlreadyHas, EmptyText, Unmatched, AmbiguousCount;
    }
}
#endif
