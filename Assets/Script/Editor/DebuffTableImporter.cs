#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DebuffTable.csv → Resources/DebuffSO/{DebuffId}.asset. SkillTableImporter와 같은 구조다.
///
/// CSV는 DebuffId가 같은 줄을 여러 개 허용한다(탈진 = 스탯 3줄). 그룹 단위로 SO 하나를 만들고,
/// Category/Type/Duration은 그룹에서 처음 채워진 값을 쓴다.
///
/// 임포터가 필드를 코드로 넣으면 DebuffSO.OnValidate가 불리지 않으므로 종류 검사를 여기서 직접 한다 —
/// 안 하면 Category=Stun인데 Type=Slow인 에셋이 조용히 만들어져 장부에 엉뚱한 종류가 기록된다.
/// </summary>
public static class DebuffTableImporter
{
    private const string OutputFolder = "Assets/Resources/DebuffSO";

    [MenuItem("Tools/Debuff/Import DebuffTable")]
    public static void Import()
    {
        var table = new DebuffTable();
        table.Load(DataTableIds.Debuff);
        var all = table.GetAll();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning("DebuffTableImporter: DebuffTable 데이터가 없습니다.");
            return;
        }

        EnsureFolder();

        int created = 0, updated = 0, skipped = 0;
        foreach (var kv in all)
        {
            string id = kv.Key;
            List<DebuffTable.Data> rows = kv.Value;

            var soType = ResolveType(rows);
            if (soType == null)
            {
                Debug.LogWarning($"DebuffTableImporter: '{id}' 처리 불가 (Category='{FirstNonEmpty(rows, r => r.Category)}')");
                skipped++;
                continue;
            }

            string path = $"{OutputFolder}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DebuffSO>(path);

            // Category가 바뀌어 파생형이 달라졌으면 갈아끼운다(SkillTableImporter와 같은 처리).
            if (existing != null && existing.GetType() != soType)
            {
                AssetDatabase.DeleteAsset(path);
                existing = null;
            }

            bool isNew = existing == null;
            var so = isNew ? (DebuffSO)ScriptableObject.CreateInstance(soType) : existing;

            if (!ApplyFields(so, id, rows))
            {
                // 새로 만든 인스턴스는 에셋으로 저장하지 않았으므로 그냥 버린다.
                if (isNew) UnityEngine.Object.DestroyImmediate(so);
                skipped++;
                continue;
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(so, path);
                created++;
            }
            else
            {
                EditorUtility.SetDirty(so);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"DebuffTableImporter 완료 → 생성 {created}, 갱신 {updated}, 건너뜀 {skipped}");
    }

    private static Type ResolveType(List<DebuffTable.Data> rows)
    {
        switch (FirstNonEmpty(rows, r => r.Category))
        {
            case "Stat": return typeof(StatDebuffSO);
            case "Dot":  return typeof(DotDebuffSO);
            // 기절·속박·침묵이 한 클래스로 합쳐졌다. 기존 표의 "Stun"도 계속 받는다.
            case "State":
            case "Stun": return typeof(StateDebuffSO);
            default:     return null;
        }
    }

    /// <summary>필드를 채운다. CSV가 잘못돼 만들면 안 되는 에셋이면 false.</summary>
    private static bool ApplyFields(DebuffSO so, string id, List<DebuffTable.Data> rows)
    {
        string typeName = FirstNonEmpty(rows, r => r.Type);
        if (!TryParseName(typeName, out DebuffType type))
        {
            Debug.LogWarning($"DebuffTableImporter: '{id}'의 Type '{typeName}'이 DebuffType 이름이 아니다 (Slow, Exhaust, Poison, Stun ...)");
            return false;
        }

        // 종류 하나만 허용한다 — 마스크(Slow|Stun)는 장부 슬롯을 짚을 수 없다.
        int bits = (int)type;
        if (bits <= 0 || (bits & (bits - 1)) != 0)
        {
            Debug.LogWarning($"DebuffTableImporter: '{id}'의 Type '{typeName}'은 단일 종류가 아니다");
            return false;
        }
        if ((type & so.AllowedTypes) == 0)
        {
            Debug.LogWarning($"DebuffTableImporter: '{id}'의 Type '{type}'은 {so.GetType().Name}이 지원하지 않는다 (지원: {so.AllowedTypes})");
            return false;
        }

        float duration = FirstValue(rows, r => r.Duration) ?? 0f;
        if (duration <= 0f)
        {
            Debug.LogWarning($"DebuffTableImporter: '{id}'의 Duration이 비었거나 0 이하다");
            return false;
        }

        so.type = type;
        so.duration = duration;

        // 방어무시는 피해를 넣는 디버프(Dot)에만 의미가 있다. 스탯·상태이상 줄에 True를 적어 두면
        // 조용히 무시되어 "켰는데 왜 안 되지"가 되므로, 막지는 않고 알려만 준다.
        if (so is not DotDebuffSO && (FirstValue(rows, r => r.IgnoreGuard) ?? false))
            Debug.LogWarning($"DebuffTableImporter: '{id}'의 IgnoreGuard=True는 무시된다 — " +
                $"방어무시는 피해를 넣는 Category=Dot에만 적용된다({so.GetType().Name}은 피해를 넣지 않는다)");

        switch (so)
        {
            case StatDebuffSO stat:
                return ApplyStatEffects(stat, id, rows);

            case DotDebuffSO dot:
                dot.percentPerTick = FirstValue(rows, r => r.PercentPerTick) ?? 0f;
                dot.interval = FirstValue(rows, r => r.Interval) ?? 1f;
                // 공격력 몫은 선택 칸이다 — 비우면 0이라 최대 체력 비율 피해만 들어간다(기존 거동).
                dot.atkPercent = Mathf.Max(0f, FirstValue(rows, r => r.AtkPercent) ?? 0f);
                // 방어무시도 선택 칸이다 — 비우면 false라 방어력이 적용된 피해로 들어간다.
                dot.ignoreGuard = FirstValue(rows, r => r.IgnoreGuard) ?? false;
                // PercentPerTick만 0인 것은 정상 저작이다 — 공격력 비례(AtkPercent)로만 굴리는 디버프.
                // 둘 다 비어 있을 때만 피해가 0이라 거절한다.
                if (dot.percentPerTick <= 0f && dot.atkPercent <= 0f)
                {
                    Debug.LogWarning($"DebuffTableImporter: '{id}'의 PercentPerTick과 AtkPercent가 둘 다 비었거나 0 이하다 " +
                        $"— 피해가 0이라 디버프가 걸리지 않는다 (PercentPerTick은 대상 최대 체력의 %, AtkPercent는 시전자 공격력의 %)");
                    return false;
                }
                // 고정 피해 시절 값(4, 5 ...)을 그대로 옮겨 적으면 한 틱에 최대 체력의 4~5%가 들어간다.
                // 틀렸다고 단정할 순 없으니 막지는 않고, 단위를 착각한 것 같으면 알려만 준다.
                if (dot.percentPerTick > 100f)
                {
                    Debug.LogWarning($"DebuffTableImporter: '{id}'의 PercentPerTick이 {dot.percentPerTick}다 — " +
                        $"이 칸은 대상 최대 체력의 비율(%)이라 100을 넘으면 한 틱에 최대 체력 이상이 들어간다. 고정 피해값을 적은 게 아닌지 확인할 것");
                }
                return true;

            case StateDebuffSO:
                return true;   // 지속시간 외에 채울 게 없다

            default:
                Debug.LogWarning($"DebuffTableImporter: '{id}' — {so.GetType().Name} 처리 분기가 없다");
                return false;
        }
    }

    private static bool ApplyStatEffects(StatDebuffSO so, string id, List<DebuffTable.Data> rows)
    {
        var effects = new List<DebuffStatEffect>();

        foreach (var r in rows)
        {
            string statName = (r.StatType ?? "").Trim();
            if (string.IsNullOrEmpty(statName)) continue;   // 스탯 칸을 안 채운 줄(그룹 머리글 등)은 건너뛴다

            if (!TryParseName(statName, out StatType statType))
            {
                Debug.LogWarning($"DebuffTableImporter: '{id}'의 StatType '{statName}'이 StatType 이름이 아니다 (SPD, AS, ATK, DEF ...) — 이 줄은 건너뜀");
                continue;
            }

            string modName = (r.ModifierType ?? "").Trim();
            if (!TryParseName(modName, out ModifierType modType))
            {
                Debug.LogWarning($"DebuffTableImporter: '{id}'의 ModifierType '{modName}'이 ModifierType 이름이 아니다 (Flat, Additive, Multiplier) — 이 줄은 건너뜀");
                continue;
            }

            float value = r.Value ?? 0f;
            // 감소분은 음수여야 한다. 양수를 넣으면 Stat.cs 계산식(value *= 1 + v)에서 강화로 뒤집힌다.
            if (value >= 0f)
                Debug.LogWarning($"DebuffTableImporter: '{id}'의 {statType} Value가 {value}다 — 디버프는 음수여야 강화로 뒤집히지 않는다");

            effects.Add(new DebuffStatEffect
            {
                statType = statType,
                modifierType = modType,
                value = value,
                maxStacks = Mathf.Max(1, r.MaxStacks ?? 1),
            });
        }

        if (effects.Count == 0)
        {
            Debug.LogWarning($"DebuffTableImporter: '{id}'에 유효한 스탯 줄이 없다");
            return false;
        }

        so.effects = effects.ToArray();
        return true;
    }

    // Enum.TryParse는 "2" 같은 숫자 문자열도 통과시키고("2" → Slow), 범위를 벗어난 값도 그대로 만들어낸다
    // ("99" → 정의되지 않은 값). 그 값이 StatType이면 StatContainer의 딕셔너리 조회가 런타임에 터진다.
    // 표에는 이름만 적게 하고 정의된 값인지까지 확인한다.
    private static bool TryParseName<T>(string raw, out T value) where T : struct, Enum
    {
        value = default;

        string s = (raw ?? "").Trim();
        if (s.Length == 0) return false;
        if (char.IsDigit(s[0]) || s[0] == '-') return false;   // 숫자로 적은 건 거부 — 이름만 받는다
        if (!Enum.TryParse(s, true, out value)) return false;

        return Enum.IsDefined(typeof(T), value);
    }

    // 그룹에서 처음 채워진 문자열. 2번째 줄부터 Category/Type/Duration을 비워둘 수 있게 해준다.
    private static string FirstNonEmpty(List<DebuffTable.Data> rows, Func<DebuffTable.Data, string> pick)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var v = (pick(rows[i]) ?? "").Trim();
            if (!string.IsNullOrEmpty(v)) return v;
        }
        return "";
    }

    private static T? FirstValue<T>(List<DebuffTable.Data> rows, Func<DebuffTable.Data, T?> pick) where T : struct
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var v = pick(rows[i]);
            if (v.HasValue) return v;
        }
        return null;
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder)) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateFolder("Assets/Resources", "DebuffSO");
    }
}
#endif
