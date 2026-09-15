using UnityEngine;

[CreateAssetMenu(fileName = "BaseUpgradeData", menuName = "Scriptable Objects/BaseUpgradeData")]
public class BaseUpgradeData : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public Color iconColor = Color.white;
    public int cost;
    public string description;
    public BaseUpgradeData[] prerequisites; // 이걸 해금해야 이게 열림
    public float effectAmount; // 이 단계가 해금되면 더해지는 값
    public bool isLast;
}
