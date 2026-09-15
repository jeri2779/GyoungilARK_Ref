using UnityEngine;


public interface IDamageAble
{
    public float Hp {get;}
    public int Defense{get;}
    public void TakeDamage(int damage,bool ignore = false);
    public void Die();
}


