using UnityEngine;

public class WormQueen : EnemyBase
{
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("WormAttack", at: transform.position);
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("WormQueenDie", at: transform.position);
    }
}
