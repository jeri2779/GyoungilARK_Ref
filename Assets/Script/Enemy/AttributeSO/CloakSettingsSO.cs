using UnityEngine;
[CreateAssetMenu(menuName = "Enemy/Cloak Settings", fileName = "CloakSettings")]
public class CloakSettingsSO : ScriptableObject
{
    public Material cloakMaterial;
    public float fadeSpeed = 4f;
}
