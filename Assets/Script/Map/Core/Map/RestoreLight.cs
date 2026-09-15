using UnityEngine;

// Play 진입 시 초기화되는 태양광 값을 원래대로 되돌린다.
public class RestoreLight : MonoBehaviour
{
    [SerializeField] private float intensity = 0.75f;
    [SerializeField] private Color tint = new Color(0.597f, 0.597f, 0.687f);

    private void Start()
    {
        Light light = GetComponent<Light>();
        light.intensity = intensity;
        light.color = tint;
    }
}
