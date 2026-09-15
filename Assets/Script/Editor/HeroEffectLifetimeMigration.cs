using UnityEditor;
using UnityEngine;

// 영웅 이펙트 lifetime 필드들을 전부 0.5로 통일하는 일회성 에디터 마이그레이션 도구.
// AttackDataMigration.cs와 동일한 패턴(에셋 순회 -> SerializedObject 수정 -> 저장)을 따른다.
// GroundZoneEffect 프리팹은 hitEffectLifetime이 이미 개별 튜닝되어 있어 대상에서 제외한다.
public static class HeroEffectLifetimeMigration
{
    private const float TargetLifetime = 0.5f;

    [MenuItem("Tools/AttackData/2. Fix Effect Lifetimes To 0.5")]
    public static void Run()
    {
        int soChanged = FixAttackDataSOAssets();
        int prefabChanged = FixProjectilePrefabs();
        AssetDatabase.SaveAssets();
        Debug.Log($"이펙트 Lifetime 0.5 통일 완료 - AttackDataSO {soChanged}건, Projectile 프리팹 {prefabChanged}건 변경");
    }

    private static int FixAttackDataSOAssets()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AttackDataSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<AttackDataSO>(path);
            if (data == null) continue;

            var so = new SerializedObject(data);
            bool dirty = false;
            dirty |= SetIfDifferent(so, "attackEffectLifetime", TargetLifetime);
            dirty |= SetIfDifferent(so, "hitEffectLifetime", TargetLifetime);
            dirty |= SetIfDifferent(so, "chainEffectLifetime", TargetLifetime);
            if (!dirty) continue;

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[set ] {path}");
            changed++;
        }
        return changed;
    }

    private static int FixProjectilePrefabs()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !prefab.TryGetComponent(out Projectile projectile)) continue;

            var so = new SerializedObject(projectile);
            bool dirty = false;
            dirty |= SetIfDifferent(so, "hitEffectLifetime", TargetLifetime);
            dirty |= SetIfDifferent(so, "flashEffectLifetime", TargetLifetime);
            if (!dirty) continue;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            Debug.Log($"[set ] {path}");
            changed++;
        }
        return changed;
    }

    private static bool SetIfDifferent(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null || Mathf.Approximately(prop.floatValue, value)) return false;
        prop.floatValue = value;
        return true;
    }
}
