using UnityEngine;

public class DeathAttacker : EnemyBase
{
    public float hp;
    public float speed;
    public int attack;
    public int def; //테스트용 인스펙터 확인용 스탯들

    private void Start()
    {
        hp = Stats[StatType.HP];
        speed = Stats[StatType.SPD];
        attack =Mathf.FloorToInt(Stats[StatType.ATK]);
        def = Mathf.FloorToInt(Stats[StatType.DEF]);
    }
    public override void EnemySoundAttack()
    {
        EnemySoundManager.Play("DeathNormalAttack", at: transform.position);
        EnemySoundManager.Play("DeathNormalAttack2", at: transform.position);
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("DeathDie", at: transform.position);
    }
}
