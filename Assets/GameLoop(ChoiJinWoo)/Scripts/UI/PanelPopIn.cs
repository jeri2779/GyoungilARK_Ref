using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 패널이 뜰 때 가이드 패널(GuidePanelEnter.anim)과 같은 느낌으로 살짝 튀어오르듯 나타나게 하는 유틸리티.
// 별도 Animator/애니메이션 클립 없이 패널의 "쉬는" 스케일/위치를 기준으로 상대적으로 보간하므로,
// 어떤 패널에 써도 그 패널 고유의 배치를 건드리지 않는다. SetActive(true) 직후에 호출한다.
public static class PanelPopIn
{
    private const float Duration = 0.3f;
    private const float StartScale = 0.92f;
    private const float StartOffsetY = -24f;

    private static readonly Dictionary<RectTransform, CancellationTokenSource> playing = new();
    // 재생 중 취소되면(연타) 그 순간의 스케일/위치가 남는데, EaseOutBack은 목표를 살짝 넘어섰다
    // (오버슈트) 돌아오는 곡선이라 취소 시점의 값이 원래 쉬는 값보다 커져 있을 수 있다. 그 값을
    // 다음 호출이 다시 "쉬는 값"으로 읽어버리면 연타할 때마다 조금씩 부풀어 폭주한다 - 그래서 같은
    // 연속 재생 구간(취소→재시작) 동안은 맨 처음 값만 쓰고 새로 읽지 않는다.
    private static readonly Dictionary<RectTransform, (Vector3 scale, Vector2 pos)> rest = new();

    public static void Play(RectTransform panel)
    {
        if (playing.TryGetValue(panel, out var prev))
        {
            prev.Cancel();
            prev.Dispose();
        }
        else
        {
            rest[panel] = (panel.localScale, panel.anchoredPosition);
        }

        var cts = new CancellationTokenSource();
        playing[panel] = cts;
        var (restScale, restPos) = rest[panel];
        PlayAsync(panel, restScale, restPos, cts.Token).Forget();
    }

    private static async UniTaskVoid PlayAsync(RectTransform panel, Vector3 restScale, Vector2 restPos, CancellationToken token)
    {
        try
        {
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float eased = EaseOutBack(Mathf.Clamp01(elapsed / Duration));
                panel.localScale = Vector3.LerpUnclamped(restScale * StartScale, restScale, eased);
                panel.anchoredPosition = restPos + Vector2.LerpUnclamped(new Vector2(0f, StartOffsetY), Vector2.zero, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        panel.localScale = restScale;
        panel.anchoredPosition = restPos;
        playing.Remove(panel);
        rest.Remove(panel);
    }

    // 가이드 패널과 같은 살짝 튀어오르는(오버슈트) 감속 곡선.
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
