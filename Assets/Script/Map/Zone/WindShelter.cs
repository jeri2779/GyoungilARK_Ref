using System;

[Flags]
public enum WindShelter
{
    NoShelter = 0,
    FromNorth = 1,
    FromSouth = 2,
    FromWest = 4,
    FromEast = 8
}
