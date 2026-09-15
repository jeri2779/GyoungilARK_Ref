using UnityEngine;

public static class HeroAnimHash
{
    public static int idle = Animator.StringToHash("Idle");
    public static int attack = Animator.StringToHash("Attack");
    public static int death = Animator.StringToHash("Death");
    public static int stun = Animator.StringToHash("Stun");
}
