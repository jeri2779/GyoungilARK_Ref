using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct AttackDescription
{
    public Sprite icon;
    public string attackDescriptionKey;
}

[CreateAssetMenu(fileName = "HeroData", menuName = "HeroData/HeroData")]
public class HeroData : ScriptableObject
{

    public int UnitId;
    public int Tier;
    public string HeroName;
    public string HeroNameKey;
    public string HeroDescriptionKey;
    public int HeroType;
    public Sprite Icon;
    public GameObject HeroPrefab;
    public MergeKey MergeKey => new MergeKey(UnitId, Tier);
    public int PopulationCost => Tier;

    public List<AttackDescription> AttackDescriptions;
}