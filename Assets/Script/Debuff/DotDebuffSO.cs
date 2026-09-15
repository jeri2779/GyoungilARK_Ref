using UnityEngine;

/// <summary>
/// 지속 피해 디버프. Poison·Ignite·Bleed는 수치만 다르므로 이 하나의 에셋 인스턴스로 만든다.
/// 틱은 DotRegistry가 굴린다 — 이 SO는 값만 넘긴다.
///
/// 틱 피해는 고정값이 아니라 <b>대상 최대 체력의 비율</b>이다. 고정값이던 시절엔 같은 독이
/// 체력 100짜리 잡몹에겐 치명적이고 5000짜리 보스에겐 없는 것과 같았고, 적 체력이 날마다
/// UpHealthScale로 불어나는 동안 지속 피해만 제자리였다. 비율로 두면 표를 안 고쳐도 같이 따라간다.
/// </summary>
[CreateAssetMenu(menuName = "Debuff/Dot Debuff", fileName = "DotDebuff")]
public class DotDebuffSO : DebuffSO
{
    [Tooltip("한 번 틱에 들어가는 피해 — 대상 최대 체력의 몇 %인가(2 = 2%, 0.5 = 0.5%). " +
             "방어력을 무시할지는 아래 ignoreGuard가 정한다. 실제 피해값은 틱마다 대상의 현재 최대 체력으로 다시 계산된다.\n" +
             "0으로 두면 이 몫이 빠지고 아래 atkPercent(공격력 비례)만 들어간다 — 최대 체력 비율 피해가 너무 셀 때 쓴다. " +
             "둘 다 0이면 피해가 없어 DotRegistry가 아예 안 건다.")]
    [Min(0f)] public float percentPerTick = 1f;

    [Tooltip("틱 간격(초). 0.05초 미만은 DotRegistry가 0.05로 보정한다.")]
    [Min(0.05f)] public float interval = 1f;

    [Tooltip("한 번 틱에 추가로 들어가는 피해 — 건 쪽 공격력의 몇 %인가(20 = 공격력의 20%). 위 칸과 같은 % 단위다. " +
             "위 %가 '맞는 쪽 최대 체력에 비례하는 몫'이라면 이건 '때린 쪽 공격력에 비례하는 몫'이고, 둘을 더한 값이 한 틱 피해다. " +
             "0이면 기존처럼 최대 체력 비율만 들어간다. 공격력을 안 넘겨주는 경로(불 칸 등)에서 걸리면 이 몫은 0이다.")]
    [Min(0f)] public float atkPercent = 0f;

    [Tooltip("체크하면 틱 피해에 방어력이 안 먹는다(표의 IgnoreGuard=True). " +
             "끄면 방어력이 적용된 피해로 들어간다 — 표에서 비워 두면 이쪽이 기본값이다.\n" +
             "끌 때 주의: 실제 피해는 Mathf.Max(1, 피해 - 방어력)이라 방어력이 높은 적에게는 " +
             "틱이 1까지 깎일 수 있다. 방어력 무시 여부와 무관하게 실드 감소량은 항상 적용된다.")]
    public bool ignoreGuard = false;

    public override DebuffType AllowedTypes =>
        DebuffType.Poison | DebuffType.Ignite | DebuffType.Bleed|DebuffType.SandStom;

    // 이펙트 표시는 DotRegistry가 전담한다 — 매 프레임 살아있는 목록을 DebuffEffectView에 밀어 주고,
    // 만료·사망·낮 전환으로 일찍 끊기는 경우까지 그쪽이 챙긴다. DebuffSO.Apply가 또 기록하면 시계가 둘이 된다.
    protected override bool DrivesOwnEffectView => true;

    protected override bool OnApply(in DebuffContext ctx, float duration, float scale)
    {
        if (ctx.damageable == null) return false;

        // 공격력 몫은 걸리는 지금 값으로 확정해 넘긴다(틱마다 다시 읽지 않는다) —
        // 시전자는 독보다 먼저 죽거나 풀에 반납될 수 있고, 그 오브젝트를 재사용한 다른 적의 공격력을
        // 읽어 버리면 남은 틱이 엉뚱한 세기로 바뀐다. 최대 체력 몫만 맞는 쪽에서 틱마다 다시 읽는다.
        //
        // scale을 곱하지 않는 이유: scale은 "강화된 시전자는 디버프도 세다"를 %에 반영하는 배율인데
        // (SpiderToxin이 StatRatio(ATK)를 넘긴다), ctx.attackerAtk는 이미 버프가 반영된 현재 공격력이다.
        // 여기에 또 곱하면 공격력 2배 버프가 4배로 들어간다.
        float atkDamage = ctx.attackerAtk * atkPercent * 0.01f;

        // 최소 1 피해 보장은 DotRegistry가 실제 피해로 환산하는 시점(틱)에 한다 —
        // 여기선 비율만 넘기므로 반올림할 것이 없다.
        // Apply의 결과를 그대로 돌려준다. 최대 체력을 못 읽어 거절당한 경우까지 true를 주면
        // 아무 피해도 안 들어가는 디버프가 장부와 아이콘에만 남는다("보이는 것 == 걸린 것"이 깨진다).
        return DotRegistry.Apply(ctx.damageable, type, percentPerTick * scale, interval, duration, ctx.gameManager,
            atkDamage, ignoreGuard);
    }
}
