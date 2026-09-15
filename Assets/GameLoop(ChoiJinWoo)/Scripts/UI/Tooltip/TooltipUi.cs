using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 씬에 하나만 두는 툴팁 창. TooltipTrigger들이 Instance로 직접 호출해서 띄우고 끈다.
public class TooltipUi : MonoBehaviour
{
    public static TooltipUi Instance { get; private set; }

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Canvas canvas; // 화면 좌표 -> 캔버스 로컬 좌표 변환에 필요
    [SerializeField] private Vector2 offset = new(16f, 16f);
    [SerializeField] private float fadeDuration = 0.12f;

    private RectTransform canvasRect;
    private CanvasGroup canvasGroup;
    private CancellationTokenSource fadeCts;

    private void Awake()
    {
        Instance = this;
        canvasRect = canvas.transform as RectTransform;

        canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;

        panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message, Vector2 screenPosition)
    {
        if (string.IsNullOrEmpty(message)) return;

        text.text = DataTableManager.StringTable.Get(message);
        panel.gameObject.SetActive(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        SetPosition(screenPosition);

        FadeTo(1f).Forget();
    }
    public void JustShow(string message, Vector2 screenPosition)
    {
        if (string.IsNullOrEmpty(message)) return;

        text.text = message;

        panel.gameObject.SetActive(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        SetPosition(screenPosition);

        FadeTo(1f).Forget();
    }

    public void Hide()
    {
        if (!panel.gameObject.activeSelf) return;
        FadeTo(0f).Forget();
    }

    // 페이드 도중 다시 Show/Hide가 불리면 진행 중이던 페이드를 취소하고 새 목표값으로 다시 시작한다.
    private async UniTaskVoid FadeTo(float target)
    {
        fadeCts?.Cancel();
        fadeCts = new CancellationTokenSource();
        var token = fadeCts.Token;

        float start = canvasGroup.alpha;
        float elapsed = 0f;

        try
        {
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        canvasGroup.alpha = target;
        if (target <= 0f) panel.gameObject.SetActive(false);
    }

    private void SetPosition(Vector2 screenPosition)
    {
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out var localPoint);

        bool isRightHalf = localPoint.x > canvasRect.rect.center.x;
        panel.pivot = new Vector2(isRightHalf ? 1f : 0f, 0f);
        float offsetX = isRightHalf ? -offset.x : offset.x;

        panel.anchoredPosition = ClampToCanvas(localPoint + new Vector2(offsetX, offset.y));
    }

    // 패널이 화면(캔버스) 밖으로 잘려나가지 않도록 anchoredPosition을 캔버스 경계 안으로 눌러 담는다.
    // panel의 anchorMin == anchorMax(늘어나지 않는 고정 앵커)인 일반적인 툴팁 설정을 가정한다.
    private Vector2 ClampToCanvas(Vector2 desired)
    {
        var bounds = canvasRect.rect;
        var anchorPoint = bounds.min + Vector2.Scale(bounds.size, panel.anchorMin);
        var pivotOffset = Vector2.Scale(panel.rect.size, panel.pivot);

        var minX = bounds.xMin - anchorPoint.x + pivotOffset.x;
        var maxX = bounds.xMax - anchorPoint.x - (panel.rect.width - pivotOffset.x);
        var minY = bounds.yMin - anchorPoint.y + pivotOffset.y;
        var maxY = bounds.yMax - anchorPoint.y - (panel.rect.height - pivotOffset.y);

        // 패널이 캔버스보다 크면 min > max가 되어버리니, 그런 경우엔 최소값에 고정한다.
        float x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : minX;
        float y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : minY;
        return new Vector2(x, y);
    }
}
