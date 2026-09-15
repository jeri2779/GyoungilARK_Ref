using UnityEngine;

public class SpiderKing : EnemyBase
{
    public float hp;
    public float speed;
    public int attack;
    public int def; //테스트용 인스펙터 확인용 스탯들

    private void Start()
    {
        hp = Hp;
        speed = MoveSpeed;
        attack = AttackPower;
        def = Defense; //테스트용
    }
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("SpiderAttack", at: transform.position);
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("SpiderKingDie", at: transform.position);
    }
}