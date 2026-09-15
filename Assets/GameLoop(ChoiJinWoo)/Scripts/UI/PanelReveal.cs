using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 패널 루트에 붙여 켜질 때 스케일 0->1로 커지며 나타나고 꺼질 때 1->0으로 작아지며 사라지게 한다.
// HeroArchiveButton의 OpenArchiveCor/CloseArchiveCor와 같은 얼개. 여닫는 쪽 코드는 SetActive를
// 직접 부르지 말고 Show()/Hide()를 불러야 한다 - Unity는 SetActive(false) 이후엔 그 오브젝트의
// UniTask도 즉시 멈춘다.
public class PanelReveal : MonoBehaviour
{
    [SerializeField] private float scaleSpeed = 5f;

    private CancellationTokenSource cts;

    private void OnDisable()
    {
        ResetCts();
    }

    public void Show()
    {
        ResetCts();

        if (!gameObject.activeSelf)
        {
            // 비활성 상태로 시작한 패널은 안 보이는 동안 아무도 스케일을 신경 안 써 저작 시점 기본값
            // (보통 1,1,1)이 그대로 남아 있다 - 그대로 두면 Progress01이 1을 읽어 확대 모션 없이
            // 바로 나타나 버리므로, 처음 열릴 때만 0에서 시작하도록 강제한다.
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
        }

        OpenCor(cts.Token).Forget();
    }

    public void Hide()
    {
        ResetCts();
        CloseCor(cts.Token).Forget();
    }

    private void ResetCts()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
    }

    private async UniTaskVoid OpenCor(CancellationToken token)
    {
        float t = Progress01(transform.localScale); // 현재 스케일에서 이어서 열기(연타 시 튐 방지)

        try
        {
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * scaleSpeed;
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        transform.localScale = Vector3.one;
    }

    private async UniTaskVoid CloseCor(CancellationToken token)
    {
        float t = 1f - Progress01(transform.localScale);

        try
        {
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * scaleSpeed;
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
    }

    // 현재 스케일이 0~1 열림 진행도의 어디쯤인지(연타/중간취소 시 이어서 애니메이션)
    private static float Progress01(Vector3 scale) => Mathf.Clamp01(scale.x);
}
