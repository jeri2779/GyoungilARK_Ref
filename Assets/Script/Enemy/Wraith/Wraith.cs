using UnityEngine;

public class Wraith : EnemyBase
{
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("RushAttackHit", at: transform.position);
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("GhostDie", at: transform.position);
    }
}
