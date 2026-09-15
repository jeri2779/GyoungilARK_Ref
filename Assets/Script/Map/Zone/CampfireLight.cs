using UnityEngine;

// 모닥불 장식에 붙는 불빛 하나. 켜고 끄기와 비추는 범위 조절만 다룬다.
[RequireComponent(typeof(Light))]
public class CampfireLight : MonoBehaviour
{
    // 불빛을 켜거나 끈다.
    public void SetOn(bool on)
    {
        gameObject.SetActive(on);
    }

    // 불빛이 비추는 범위를 보호 반경(월드 단위)에 맞춘다.
    public void SetRange(float range)
    {
        GetComponent<Light>().range = range;
    }
}
