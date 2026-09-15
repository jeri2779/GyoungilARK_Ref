using UnityEngine;

// 슬라이더/PlayerPrefs에 저장되는 선형(0~1) 볼륨 값과 AudioMixer가 쓰는 dB 값 사이의 변환.
// 믹서에 값을 쓰는 곳(SettingUI, TitleUI)이 각자 변환하면 단위가 어긋나기 쉬워 한 곳으로 모은다.
public static class AudioVolumeUtil
{
    public static float LinearToDb(float linear)
    {
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }
}
