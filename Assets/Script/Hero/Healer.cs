using UnityEngine;

public class Healer : Hero
{
    protected override void Awake()
    {
        base.Awake();
        attackState = new HealerAttackState(this, stateMachine);
        context = new AttackContext
        {
            self = transform,
            target = null,
            anim = Anim,
            animEvents = AnimEvents,
            buffManager = buffManager,
            sc = SC,
            hero = this
        };
        occupantKind = OccupantKind.RangedHero; // 저지 담당이 아닌 서포터라 MeleeHero 대신 사용(BlockCapacity 0)
    }

    // 적이 아니라 범위 내 아군 중 체력이 가장 낮은 대상을 찾는다. FindLowestHpAlly가 이미 풀피인
    // 아군은 후보에서 제외하므로 반환되면 항상 다친 아군이다.
    protected override void AcquireTargetFromTiles()
    {
        Hero lowest = AttackDamageUtil.FindLowestHpAlly(GetObjectsInRange(transform.position, Range, RangeShape, RangeQueryAffinity.Ally));

        if (lowest != null)
        {
            target = lowest.gameObject;
            context.target = lowest.transform;
        }
    }

    protected override void CheckTargetStillInRange()
    {
        foreach (Tile tile in TileShapeQuery.GetTiles(Board, origin, Range, RangeShape))
        {
            if (tile.OccupantObject == target
                && target.GetComponent<Hero>() is Hero ally
                && !ally.IsDead
                && ally.Hp < ally.SC[StatType.HP])
                return;
        }

        target = null;
        context.target = null;
    }
}
