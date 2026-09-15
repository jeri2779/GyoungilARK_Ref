using UnityEngine;

public class Spore : EnemyBase
{
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("SporeDie", at: transform.position);
    }
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("SporeAttack", at: transform.position);
    }
}
