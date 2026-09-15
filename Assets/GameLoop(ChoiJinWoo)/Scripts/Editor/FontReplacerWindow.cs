using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 씬/프리팹에 흩어진 텍스트가 참조 중인 특정 폰트를 다른 폰트로 일괄 교체하는 툴.
// Text/TMP_Text는 폰트를 컴포넌트에 직접 직렬화해서 들고 있어서, Project Settings의
// 기본 폰트를 바꿔도 기존 오브젝트에는 반영되지 않는다 - 그래서 이 툴이 필요하다.
public class FontReplacerWindow : EditorWindow
{
    private enum Scope
    {
        OpenScenesOnly,
        OpenScenesAndPrefabs,
        AllScenesInProject,
        AllScenesAndPrefabs,
        PrefabsOnly
    }

    private TMP_FontAsset oldTmpFont;
    private TMP_FontAsset newTmpFont;
    private Font oldLegacyFont;
    private Font newLegacyFont;
    private Scope scope = Scope.OpenScenesAndPrefabs;
    private bool replaceTmp = true;
    private bool replaceLegacy;

    private readonly List<string> log = new List<string>();
    private Vector2 logScroll;

    [MenuItem("Tools/Font Replacer")]
    private static void Open()
    {
        GetWindow<FontReplacerWindow>("Font Replacer");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("씬/프리팹에서 특정 폰트를 참조하는 텍스트를 찾아 다른 폰트로 일괄 교체합니다.", MessageType.Info);

        EditorGUILayout.Space();
        replaceTmp = EditorGUILayout.ToggleLeft("TextMeshPro 폰트 교체", replaceTmp);
        using (new EditorGUI.DisabledScope(!replaceTmp))
        {
            EditorGUI.indentLevel++;
            oldTmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("교체 대상 (Old)", oldTmpFont, typeof(TMP_FontAsset), false);
            newTmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("새 폰트 (New)", newTmpFont, typeof(TMP_FontAsset), false);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        replaceLegacy = EditorGUILayout.ToggleLeft("레거시 UI Text 폰트 교체", replaceLegacy);
        using (new EditorGUI.DisabledScope(!replaceLegacy))
        {
            EditorGUI.indentLevel++;
            oldLegacyFont = (Font)EditorGUILayout.ObjectField("교체 대상 (Old)", oldLegacyFont, typeof(Font), false);
            newLegacyFont = (Font)EditorGUILayout.ObjectField("새 폰트 (New)", newLegacyFont, typeof(Font), false);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        scope = (Scope)EditorGUILayout.EnumPopup("적용 범위", scope);
        if (scope == Scope.AllScenesInProject || scope == Scope.AllScenesAndPrefabs)
        {
            EditorGUILayout.HelpBox("이 옵션은 프로젝트의 모든 씬을 순서대로 열었다 닫으며 저장합니다. 시간이 걸릴 수 있습니다.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!CanRun()))
        {
            if (GUILayout.Button("스캔 (미리보기, 저장 안 함)", GUILayout.Height(24)))
            {
                Run(true);
            }

            if (GUILayout.Button("교체 실행", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("폰트 교체", "선택한 범위에서 폰트를 교체하고 씬/프리팹을 저장합니다.\n계속할까요?", "실행", "취소"))
                {
                    Run(false);
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("로그", EditorStyles.boldLabel);
        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(220));
        foreach (var line in log)
        {
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private bool CanRun()
    {
        if (!replaceTmp && !replaceLegacy) return false;
        if (replaceTmp && (oldTmpFont == null || newTmpFont == null)) return false;
        if (replaceLegacy && (oldLegacyFont == null || newLegacyFont == null)) return false;
        return true;
    }

    private void Run(bool dryRun)
    {
        log.Clear();
        var tmpCount = 0;
        var legacyCount = 0;

        var includeOpenScenes = scope == Scope.OpenScenesOnly || scope == Scope.OpenScenesAndPrefabs;
        var includeAllScenes = scope == Scope.AllScenesInProject || scope == Scope.AllScenesAndPrefabs;
        var includePrefabs = scope == Scope.PrefabsOnly || scope == Scope.OpenScenesAndPrefabs || scope == Scope.AllScenesAndPrefabs;

        if (includeOpenScenes)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                ProcessScene(scene, dryRun, out var c1, out var c2);
                tmpCount += c1;
                legacyCount += c2;
            }
        }

        if (includeAllScenes)
        {
            if (!ProcessAllScenesInProject(dryRun, ref tmpCount, ref legacyCount))
            {
                log.Add("취소됨: 저장되지 않은 씬이 있습니다.");
                return;
            }
        }

        if (includePrefabs)
        {
            ProcessAllPrefabs(dryRun, ref tmpCount, ref legacyCount);
        }

        log.Add($"완료: TMP {tmpCount}개, Legacy {legacyCount}개 {(dryRun ? "발견" : "교체")}");
        Debug.Log($"[FontReplacer] TMP {tmpCount}개, Legacy {legacyCount}개 {(dryRun ? "발견됨 (미리보기)" : "교체 완료")}");
    }

    private bool ProcessAllScenesInProject(bool dryRun, ref int tmpCount, ref int legacyCount)
    {
        if (!dryRun && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        var originalScenePaths = new List<string>();
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            originalScenePaths.Add(SceneManager.GetSceneAt(i).path);
        }

        var sceneGuids = AssetDatabase.FindAssets("t:Scene");
        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            ProcessScene(scene, dryRun, out var c1, out var c2);
            tmpCount += c1;
            legacyCount += c2;

            if (!dryRun && c1 + c2 > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        if (!dryRun)
        {
            RestoreScenes(originalScenePaths);
        }

        return true;
    }

    private static void RestoreScenes(List<string> scenePaths)
    {
        var first = true;
        foreach (var path in scenePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            EditorSceneManager.OpenScene(path, first ? OpenSceneMode.Single : OpenSceneMode.Additive);
            first = false;
        }
    }

    private void ProcessAllPrefabs(bool dryRun, ref int tmpCount, ref int legacyCount)
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            var c1 = replaceTmp ? ReplaceTmpFonts(root.transform, dryRun, path) : 0;
            var c2 = replaceLegacy ? ReplaceLegacyFonts(root.transform, dryRun, path) : 0;

            if (!dryRun && c1 + c2 > 0)
            {
                EditorUtility.SetDirty(root);
            }

            tmpCount += c1;
            legacyCount += c2;
        }

        if (!dryRun)
        {
            AssetDatabase.SaveAssets();
        }
    }

    private void ProcessScene(Scene scene, bool dryRun, out int tmpCount, out int legacyCount)
    {
        tmpCount = 0;
        legacyCount = 0;
        if (!scene.IsValid()) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (replaceTmp) tmpCount += ReplaceTmpFonts(root.transform, dryRun, scene.path);
            if (replaceLegacy) legacyCount += ReplaceLegacyFonts(root.transform, dryRun, scene.path);
        }

        if (!dryRun && tmpCount + legacyCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private int ReplaceTmpFonts(Transform root, bool dryRun, string context)
    {
        var count = 0;
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t.font != oldTmpFont) continue;
            count++;
            log.Add($"[TMP] {context} :: {GetPath(t.transform)}");
            if (dryRun) continue;

            Undo.RecordObject(t, "Replace TMP Font");
            t.font = newTmpFont;
            EditorUtility.SetDirty(t);
        }
        return count;
    }

    private int ReplaceLegacyFonts(Transform root, bool dryRun, string context)
    {
        var count = 0;
        var texts = root.GetComponentsInChildren<Text>(true);
        foreach (var t in texts)
        {
            if (t.font != oldLegacyFont) continue;
            count++;
            log.Add($"[Legacy] {context} :: {GetPath(t.transform)}");
            if (dryRun) continue;

            Undo.RecordObject(t, "Replace Legacy Font");
            t.font = newLegacyFont;
            EditorUtility.SetDirty(t);
        }
        return count;
    }

    private static string GetPath(Transform t)
    {
        var path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
