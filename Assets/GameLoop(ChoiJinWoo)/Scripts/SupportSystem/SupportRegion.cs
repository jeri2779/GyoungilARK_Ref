using UnityEngine;

[CreateAssetMenu(fileName = "SupportRegion", menuName = "Scriptable Objects/SupportRegion")]
public class SupportRegion : ScriptableObject
{
    [Header("해금되는 병종")]
    [SerializeField] private HeroType unlockHero;
    [Header("해금되는 지역")]
    [SerializeField] private int moduleID;
    [Header("해금되는 적 종류")]
    [SerializeField] private EnemyTypeList unlockEnemy;

    public HeroType UnlockHero => unlockHero;
    public int ModuleID => moduleID;
    public EnemyTypeList UnlockEnemy => unlockEnemy;
}
