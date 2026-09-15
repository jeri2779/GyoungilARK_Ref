using UnityEngine;
using UnityEngine.UI;

// Image의 알파를 sin파로 은은하게 오르내려 발광하는 느낌을 낸다 (TutorialOverlayUI의 하이라이트
// 테두리 펄스와 같은 방식). Screen Space - Overlay 캔버스는 Bloom 포스트 프로세싱이 적용되지
// 않으므로, 셰이더/블룸 없이 UI에서 발광 효과를 내고 싶을 때 이 컴포넌트를 그 Image에 붙인다.
// BossAlert의 "Arrow"처럼 실제 아이콘보다 크고 옅은 색의 halo용 Image에 붙이면, 그 halo가
// 숨쉬듯 밝아졌다 옅어지며 테두리가 발광하는 것처럼 보인다.
public class UiGlowPulse : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;

    private Color baseColor;

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        baseColor = image.color;
    }

    // Time.unscaledTime 기준 - 게임이 일시정지(Time.timeScale=0)되어도 계속 펄스한다.
    private void Update()
    {
        float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f; // 0..1
        Color c = baseColor;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, wave) * baseColor.a;
        image.color = c;
    }
}
