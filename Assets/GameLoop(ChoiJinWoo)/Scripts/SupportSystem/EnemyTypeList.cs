using System;

[Flags]
public enum EnemyTypeList
{
    NormalSpider = 1 << 0,
    PoisonSpider = 1 << 1,
    KingSpider = 1 << 2,
    Bat = 1 << 3,
    Scorpion = 1 << 4,
    Golem = 1 << 5,
}