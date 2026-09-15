using UnityEngine;

public class IceGolem : EnemyBase
{
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("GolemAttack", at: transform.position);
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("GolemDie", at: transform.position);
    }
}
