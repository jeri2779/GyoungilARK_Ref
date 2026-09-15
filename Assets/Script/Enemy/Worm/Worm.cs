using UnityEngine;

public class Worm : EnemyBase
{
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("WormAttack", at: transform.position);
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("WormDie", at: transform.position);
    }
}
