using System;
using UnityEngine;

// 팔레트에 담기는 배치 슬롯 하나.
[Serializable]
public class Placeable
{
    public string label = "유닛";
    public GameObject prefab;
    public OccupantKind kind = OccupantKind.MeleeHero;
}
