using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스탯 감소 디버프. 시간 관리를 전부 BuffManager에 맡긴다 — 자체 타이머가 없다.
///
/// Slow·ASDown·ArmorBreak·Exhaust·Frost는 클래스를 따로 만들 이유가 없어 이 하나의 에셋 인스턴스로 만든다
/// (Slow = SPD 하나, Exhaust = AS·SPD·ATK 셋을 한 에셋에 넣으면 끝).
///
/// ※ 스탯 감소 디버프를 새로 만들 땐 반드시 아래 AllowedTypes에 그 종류를 더할 것.
///   빠뜨리면 DebuffTableImporter가 "이 파생형이 지원하지 않는다"며 에셋 자체를 안 만든다(조용히 없는 디버프가 된다).
///   여러 스탯을 한꺼번에 깎는 종류라면 EnemyDebuffBar.ExplainedStats에도 같이 등록해야
///   아이콘이 중복해서 뜨지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Debuff/Stat Debuff", fileName = "StatDebuff")]
public class StatDebuffSO : DebuffSO
{
    [Tooltip("깎을 스탯 목록. 이속만 깎으면 1개, 탈진처럼 여러 개면 여기 늘린다.")]
    public DebuffStatEffect[] effects;

    public override DebuffType AllowedTypes =>
        DebuffType.Slow | DebuffType.ATKDown | DebuffType.ASDown | DebuffType.ArmorBreak
        | DebuffType.Exhaust | DebuffType.Frost;

    protected override bool OnApply(in DebuffContext ctx, float duration, float scale)
    {
        if (ctx.unit == null || ctx.buffManager == null || effects == null) return false;

        int applied = 0;
        object stackKey = StackKey(ctx, duration);
        for (int i = 0; i < effects.Length; i++)
        {
            // 대상이 안 가진 스탯은 건너뛴다. 영웅은 제자리에 서 있는 유닛이라 SPD를 등록하지 않는데
            // (Hero는 HP·ATK·DEF·BLK·AS만 AddStat), 그대로 넘기면 BuffManager → StatContainer.AddModifier가
            // 딕셔너리 조회에서 KeyNotFoundException을 던진다. 예외가 여기서 새면 앞 칸은 이미 걸리고
            // 뒤 칸은 안 걸린 반쪽 상태로 스킬 코루틴까지 죽는다.
            if (!HasStat(ctx.unit.Stats, effects[i].statType))
            {
                DebuffDebug.Log($"{name}({type}) {effects[i].statType} 건너뜀 — 대상 " +
                    $"{(ctx.targetObject != null ? ctx.targetObject.name : "?")}에 그 스탯이 없다");
                continue;
            }

            // scale은 감소분에 곱한다 — Multiplier -0.3에 scale 1.67이면 -0.5(50% 감소)가 된다.
            ctx.buffManager.ApplyStackingModifier(ctx.unit, effects[i].statType, effects[i].modifierType,
                effects[i].value * scale, duration, effects[i].maxStacks, stackKey);
            applied++;
        }

        // 한 칸도 못 걸었으면 안 걸린 것이다 — true를 주면 아무 일도 안 하는 디버프가
        // 장부와 아이콘에만 남는다("보이는 것 == 걸린 것"이 깨진다).
        return applied > 0;
    }

    /// <summary>
    /// BuffManager가 스택을 찾는 열쇠. 그쪽은 Target + StatType + Source 셋이 같은 항목을 찾으므로,
    /// Source에 "누가 걸었나"를 넣으면 <b>같은 종류 디버프가 출처 수만큼 독립으로 쌓인다</b> —
    /// 장판(GroundZoneEffect)은 자기 인스턴스를 Source로 넘기기 때문에, 미친과학자의 PoisonZone 두 개가
    /// 같은 적을 덮으면 Slow_Heavy(-0.5)가 두 번 들어가 이속이 0이 됐다.
    ///
    /// 그래서 <b>종류</b>를 열쇠로 쓴다. 그러면 규칙이 그대로 떨어진다:
    ///   같은 종류  → 언제나 한 항목(maxStacks까지만 쌓이고 그 뒤엔 지속시간만 갱신)
    ///   다른 종류  → 열쇠가 달라 독립 중첩 (Slow와 Frost는 둘 다 SPD를 깎지만 서로를 덮지 않는다)
    /// 감소분끼리 곱해지는 것은 이 열쇠가 아니라 표의 ModifierType=Multiplier가 담당한다
    /// (Stat.Value가 Additive는 합산해 한 번 곱하고, Multiplier는 각각 곱한다).
    /// </summary>
    private object StackKey(in DebuffContext ctx, float duration)
    {
        // 무한 지속은 BuffManager가 만료시키지 않는다 — 호출부가 RemoveBuffs(target, source)로 직접 뗀다
        // (ZoneDebuffEffect의 지형 디버프가 durationOverride=Infinity로 그렇게 쓴다).
        // 그 경로에선 Source가 해제 열쇠이므로 바꾸면 디버프가 영영 안 풀린다.
        if (float.IsInfinity(duration)) return ctx.source ?? this;
        return TypeStackKey(type);
    }

    // 종류별 열쇠를 캐시한다. BuffManager가 Source를 참조 비교(==)하므로 매번 새로 박싱하면
    // 같은 종류인데도 늘 다른 열쇠가 되어 아무 효과가 없다 — 종류당 딱 한 번만 박싱해 그것을 돌려쓴다.
    // 씬 오브젝트를 붙들지 않는 값 뿐이라 도메인 리로드를 꺼도 남아도 문제되지 않는다.
    private static readonly Dictionary<DebuffType, object> typeStackKeys = new();

    private static object TypeStackKey(DebuffType type)
    {
        if (!typeStackKeys.TryGetValue(type, out object key))
        {
            key = type;   // 박싱은 여기서 1회
            typeStackKeys[type] = key;
        }
        return key;
    }

    // 이 대상이 그 스탯을 가지고 있는가.
    // StatContainer에 조회용 API가 없어(GetValue/인덱서 모두 없는 키면 던진다) 읽어 보고 판단한다.
    // StatContainer는 GameLoop 쪽 파일이라 여기서 Has(StatType)를 넣을 수 없다 —
    // 그쪽에 TryGetValue가 생기면 이 메서드를 그걸로 갈아끼우면 된다.
    // 디버프를 거는 순간에만 불리므로(매 프레임이 아니다) 예외 비용은 문제가 되지 않는다.
    private static bool HasStat(StatContainer sc, StatType type)
    {
        if (sc == null) return false;
        try
        {
            _ = sc[type];
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }
}

[System.Serializable]
public struct DebuffStatEffect
{
    public StatType statType;

    [Tooltip("Flat = 절대값 가감(이속 5 → 3). Additive/Multiplier = 비율. " +
             "둘 다 value가 -0.3이면 30% 감소이고, 차이는 중첩 시에만 난다 — " +
             "Additive는 합산 후 한 번 곱하고(-0.3 두 개 = x0.4), Multiplier는 각각 곱한다(x0.49).\n" +
             "비율 감소는 반드시 Multiplier로 둘 것. Additive는 합산이라 -0.5 두 개면 정확히 0이 되어 " +
             "대상이 완전히 멈추고, 더 겹치면 음수가 된다. Multiplier는 몇 개가 겹쳐도 0에 닿지 않는다.")]
    public ModifierType modifierType;

    // Stat.cs의 계산식이 value *= (1f + mod.Value)라 비율도 "감소분"을 음수로 넣는다.
    // 0.7을 넣으면 x0.7이 아니라 x1.7이 되어 디버프가 버프로 뒤집힌다.
    [Tooltip("감소분이므로 항상 음수. Flat이면 -2(절대값), 비율이면 -0.3(=30% 감소). " +
             "배율 0.7을 넣으면 x1.7이 되어 거꾸로 강화된다.")]
    public float value;

    [Tooltip("한 종류가 이 대상에 몇 겹까지 쌓이는가. 1 = 몇 명이 걸어도 하나만 들어가고 지속시간만 갱신된다. " +
             "Unity 기본값 0은 BuffManager가 1로 보정한다.\n" +
             "출처(누가 걸었나)가 아니라 디버프 종류로 세므로(StatDebuffSO.StackKey), 장판 여러 개나 " +
             "영웅 여러 명이 같은 종류를 걸어도 이 상한을 넘지 않는다. 다른 종류는 서로 독립이다.")]
    public int maxStacks;
}
