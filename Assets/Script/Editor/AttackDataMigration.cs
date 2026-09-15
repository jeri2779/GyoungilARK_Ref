using UnityEditor;
using UnityEngine;

// AttackDataSO 단순화 작업(Phase B~C)에서 쓰는 일회성 에디터 마이그레이션 도구 모음.
public static class AttackDataMigration
{
    // continuousDelivery 필드 신설 이전에는 "projectilePrefab이 비어있으면 Beam, 채워져 있으면
    // ProjectileVolley"라는 암묵 규칙으로 갈렸다(ContinuousBeamStrategy 참고). 이 규칙을 그대로
    // 새 필드에 채워 넣는다 — timingMode가 Discrete인 에셋은 이 필드를 아예 읽지 않으므로 건드리지 않는다.
    [MenuItem("Tools/AttackData/1. Migrate ContinuousDelivery")]
    public static void MigrateContinuousDelivery()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AttackDataSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<AttackDataSO>(path);
            if (data == null || data.timingMode != AttackTimingMode.Continuous) continue;

            ContinuousDelivery want = data.projectilePrefab != null
                ? ContinuousDelivery.ProjectileVolley : ContinuousDelivery.Beam;

            var so = new SerializedObject(data);
            SerializedProperty p = so.FindProperty("continuousDelivery");
            if (p.enumValueIndex == (int)want)
            {
                Debug.Log($"[skip] {path} = {want}");
                continue;
            }
            p.enumValueIndex = (int)want;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[set ] {path} -> {want}");
            changed++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"ContinuousDelivery 마이그레이션 완료 - 변경 {changed}건");
    }
}
