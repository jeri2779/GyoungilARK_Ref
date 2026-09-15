using System;

[Flags]
public enum HeroType
{
    SwordMan = 1 << 0,
    Archer = 1 << 1,
    SpearMan = 1 << 2,
    DualSwordMan = 1 << 3,
    Mage = 1 << 4,
    THS = 1 << 5,
    Healer = 1 << 6,
}