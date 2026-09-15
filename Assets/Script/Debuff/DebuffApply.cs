using UnityEngine;

/// <summary>
/// 유닛에 메서드를 두지 않고도 디버프를 걸 수 있는 공용 입구.
/// EnemyBase.ApplyDebuffTo와 하는 일이 같고, 그쪽은 buffManager/gameManager를 자기가 들고 있어 인수가 적을 뿐이다.
///
/// 영웅 공격 코드(AttackDataSO 등)가 적에게 디버프를 걸 때 쓴다 — Hero.cs를 수정하지 않아도 된다.
/// AttackContext.buffManager를 그대로 넘기면 되고, gameManager는 AttackContext에 없어서 null이어도 동작한다
/// (DotRegistry가 낮 전환 구독을 못 걸 뿐이며, 그 구독은 적이 한 번이라도 DoT를 걸면 그때 붙는다).
/// </summary>
public static class DebuffApply
{
    /// <param name="attackerAtk">건 쪽의 현재 공격력. 지속 피해의 공격력 몫(DotDebuffSO.atkPercent)에만 쓰인다.
    /// 안 넘기면 0 — 그 몫이 빠지고 최대 체력 비율 피해만 들어간다.</param>
    public static void To(Component target, DebuffSO debuff, BuffManager buffManager, GameManager gameManager = null,
        float durationOverride = 0f, float scale = 1f, object source = null, float attackerAtk = 0f)
    {
        if (debuff == null || target == null) return;

        debuff.Apply(DebuffContext.For(target, buffManager, gameManager, source ?? debuff, attackerAtk), durationOverride, scale);
    }
}
