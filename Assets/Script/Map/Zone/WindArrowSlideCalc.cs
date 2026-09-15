using UnityEngine;

// 흐른 시간을 화살표가 밀려난 거리와 진하기로 바꾸는 계산 전담.
public static class WindArrowSlideCalc
{
    private const float FadeInEnd = 0.18f;
    private const float FadeOutStart = 0.72f;

    // 한 바퀴 안에서 지금이 어디쯤인지 0~1로 돌려준다.
    public static float ReadProgress(float elapsedTime, float period)
    {
        return Mathf.Repeat(elapsedTime, period) / period;
    }

    // 진행도만큼 바람 방향으로 밀려난 거리를 돌려준다.
    public static float ReadOffset(float progress, float distance)
    {
        return progress * distance;
    }

    // 시작과 끝이 흐려지도록 진행도에 맞는 진하기를 돌려준다.
    public static float ReadAlpha(float progress)
    {
        float fadeIn = Mathf.InverseLerp(0f, FadeInEnd, progress);
        float fadeOut = Mathf.InverseLerp(1f, FadeOutStart, progress);
        return Mathf.Min(fadeIn, fadeOut);
    }
}
