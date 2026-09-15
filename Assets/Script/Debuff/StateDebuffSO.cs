using UnityEngine;

/// <summary>
/// 상태이상 디버프 — 기절·속박·침묵. 스탯을 건드리지 않고 "행동 차단"으로 작동한다.
///
/// 속박·침묵은 별도 인터페이스가 필요 없다. 장부(DebuffTracker)에 비트가 켜지는 것이 곧 효과이고,
/// EnemyBase의 차단 게이트(CannotMove/CannotAttack/CannotCast)가 매 프레임 그 장부를 읽는다.
/// 그래서 장부가 없는 대상(Hero — IDebuffCarrier 미구현)에게는 걸리지 않은 것으로 처리한다.
///
/// 기절만 IStunAble로 위임한다. 상태 기록 외에 애니 bool·별 이펙트·진행 중 스킬 취소가 딸려 있고,
/// 그건 EnemyBase.Stun이 이미 하고 있어서다(그쪽이 장부에도 기록하므로 시계는 여전히 하나다).
///
/// 폴링 방식이라 이미 시전된 스킬은 스스로 게이트를 봐야 한다 —
/// 대시(DashSkillDataSO)가 루프 안에서 owner.CannotMove를 확인해 그 자리에 멈추는 식이다.
/// </summary>
[CreateAssetMenu(menuName = "Debuff/State Debuff", fileName = "StateDebuff")]
public class StateDebuffSO : DebuffSO
{
    public override DebuffType AllowedTypes =>
        DebuffType.Stun | DebuffType.Root | DebuffType.Silence;

    // scale은 쓰지 않는다 — 상태이상의 세기는 지속시간 하나이므로 durationOverride로 조절한다.
    protected override bool OnApply(in DebuffContext ctx, float duration, float scale)
    {
        if (type == DebuffType.Stun)
        {
            if (ctx.stunnable == null) return false;   // 영웅은 아직 IStunAble을 구현하지 않아 여기서 걸러진다

            ctx.stunnable.Stun(duration);
            return true;
        }

        // 속박·침묵: 기록이 곧 효과다. 실제 기록은 DebuffSO.Apply가 같은 duration으로 넣는다.
        return ctx.ledger != null;
    }
}
