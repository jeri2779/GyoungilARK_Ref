using UnityEngine;

public class Spook : EnemyBase
{
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("GhostDie", at: transform.position);
    }
}
