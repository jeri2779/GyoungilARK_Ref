using UnityEngine;

/// <summary>
/// DebuffSO가 디버프를 걸 때 필요한 것들. AttackContext와 같은 취지 — SO는 상태를 못 들므로 밖에서 받아온다.
/// 종류별로 필요한 통로가 달라(스탯=IUnit, 지속피해=IDamageAble, 상태이상=IStunAble) 셋을 같이 들고 있고,
/// 없는 통로는 null이다. 해당 SO가 null을 보면 조용히 걸지 않는다.
/// </summary>
public struct DebuffContext
{
    public GameObject targetObject;
    // 통로를 찾은 유닛 본체(콜라이더가 아니라 Hero/EnemyBase 쪽). 이펙트 장부의 키로 쓴다 —
    // DotRegistry가 Host로 쓰는 것과 같은 오브젝트여야 한 대상이 두 항목으로 갈라지지 않는다.
    public Component host;
    public IUnit unit;
    public IDamageAble damageable;
    public IStunAble stunnable;
    public DebuffTracker ledger;      // 대상이 IDebuffCarrier가 아니면 null
    public DebuffType immuneMask;     // 대상이 안 걸리는 종류. DebuffSO.Apply가 여기서 걸러낸다
    public IDebuffCarrier carrier;    // 면역으로 막혔을 때 알려 줄 대상. ledger와 같은 곳에서 찾는다
    public BuffManager buffManager;
    public GameManager gameManager;
    public object source;             // BuffManager의 스택 판정 키. 보통 디버프를 건 SO 자신
    // 디버프를 건 쪽의 공격력. 지속 피해의 공격력 몫(DotDebuffSO.atkPercent)에만 쓴다.
    // 시전자 참조가 아니라 값을 찍어 넘기는 이유 — 그 유닛은 디버프보다 먼저 죽거나 풀에 반납될 수 있고,
    // 재사용된 오브젝트에서 다른 적의 공격력을 읽으면 남은 틱이 조용히 다른 세기가 된다.
    // 안 넘기는 경로(불 칸 등 시전자가 없는 것)는 0이라 공격력 몫이 빠지고 비율 피해만 들어간다.
    public float attackerAtk;

    /// <summary>
    /// 대상 컴포넌트에서 통로들을 찾아 컨텍스트를 만든다.
    /// 자식 콜라이더를 맞은 경우도 있어 자기 자신에서 못 찾으면 부모까지 올라간다.
    /// </summary>
    public static DebuffContext For(Component target, BuffManager buffManager, GameManager gameManager, object source,
        float attackerAtk = 0f)
    {
        var ctx = new DebuffContext
        {
            buffManager = buffManager,
            gameManager = gameManager,
            source = source,
            attackerAtk = attackerAtk,
        };
        if (target == null) return ctx;

        ctx.unit = target as IUnit ?? target.GetComponentInParent<IUnit>();
        // 유닛 본체를 찾았으면 나머지 통로도 거기서 찾는다 — 콜라이더에서 매번 부모를 다시 타는 것을 피한다.
        Component host = ctx.unit as Component ?? target;
        ctx.host = host;
        ctx.targetObject = host.gameObject;
        ctx.damageable = host as IDamageAble ?? host.GetComponentInParent<IDamageAble>();
        ctx.stunnable = host as IStunAble ?? host.GetComponentInParent<IStunAble>();

        IDebuffCarrier carrier = host as IDebuffCarrier ?? host.GetComponentInParent<IDebuffCarrier>();
        ctx.carrier = carrier;
        ctx.ledger = carrier?.Debuffs;
        ctx.immuneMask = carrier?.ImmuneDebuffs ?? DebuffType.None;

        return ctx;
    }
}
