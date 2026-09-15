using UnityEngine;

public class Chicken : EnemyBase
{
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("SpiderAttack", at: transform.position);

    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("FlyDie", at: transform.position);
    }
}
