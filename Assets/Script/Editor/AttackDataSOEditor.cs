using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(AttackDataSO))]
public class AttackDataSOEditor : Editor
{
    private static bool sDetection = true;
    private static bool sPattern = true;
    private static bool sArea = true;
    private static bool sChain = true;
    private static bool sContinuous = true;
    private static bool sAnim = true;
    private static bool sFx = true;
    private static bool sSound = true;
    private static bool sMisc = true;
    private static bool sHeal = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        bool isArea = Prop("attackType").enumValueIndex == (int)AttackType.Area;
        bool isSameTarget = Prop("targetMode").enumValueIndex == (int)TargetMode.SameTarget;
        var shape = (AreaShape)Prop("areaShape").enumValueIndex;
        bool isContinuous = Prop("timingMode").enumValueIndex == (int)AttackTimingMode.Continuous;

        Group("탐지", ref sDetection, "range", "rangeShape", "unattackableTarget");
        Group("타격 형태", ref sPattern, "attackType", "attackPer", "attackCount",
            "targetMode", "targetCount", "shotInterval");

        Group("범위 공격", ref sArea, "areaShape",
            isArea ? "areaRange" : null,
            isArea && shape == AreaShape.Line ? "lineLength" : null,
            isArea && isSameTarget ? "areaCenterOnTarget" : null);

        if (shape == AreaShape.Chain)
            Group("체인", ref sChain, "chainRange", "chainCount", "chainFalloff",
                "chainEffectPrefab", "chainEffectLifetime");

        EditorGUILayout.PropertyField(Prop("timingMode"));
        if (isContinuous)
        {
            bool isBeam = Prop("continuousDelivery").enumValueIndex == (int)ContinuousDelivery.Beam;
            Group("채널링", ref sContinuous, "continuousDelivery", "continuousDuration", "continuousTickInterval",
                isBeam ? "beamEffectPrefab" : null);
        }

        Group("애니메이션", ref sAnim, "selectMode", "animTriggers", "clipLength");
        Group("이펙트", ref sFx, "attackEffect", "attackEffectLifetime", "hitEffect", "hitEffectLifetime");
        Group("사운드", ref sSound, "attackSoundKey");
        // projectilePrefab은 조건부로 숨기지 않는다 — 근접/원거리 구분이 데이터에 없어
        // (IAttackExecutor 주입이 유일한 권위) 숨길 조건을 만들 근거가 없다.
        Group("버프 / 장판 / 투사체", ref sMisc, "buffList", "targetDebuffs", "groundZonePrefab", "projectilePrefab");
        Group("아군 힐 / 피흡", ref sHeal, "lifestealPercent", "allyHealAmount",
            "allyHealRange", "allyHealRangeShape");

        serializedObject.ApplyModifiedProperties();
    }

    private SerializedProperty Prop(string name) => serializedObject.FindProperty(name);

    private void Group(string label, ref bool foldout, params string[] propertyNames)
    {
        foldout = EditorGUILayout.Foldout(foldout, label, true, EditorStyles.foldoutHeader);
        if (!foldout) return;

        EditorGUI.indentLevel++;
        foreach (string name in propertyNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            EditorGUILayout.PropertyField(Prop(name));
        }
        EditorGUI.indentLevel--;
    }
}
