using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

// 화면 정중앙에 크게 떴다가 위로 떠오르면서 옅어져 사라지는 1회성 피드백 메시지(메이플스토리 데미지
// 표시 느낌). 연타해도 이전 표시를 취소하지 않고 각자 독립적으로 떠오르되, 동시에 뜰 수 있는 개수는
// maxActive로 제한한다 - 그 한도에 도달하면 가장 먼저 떴던 인스턴스의 연출을 취소하고 그 자리에서
// 바로 새 메시지로 재사용한다(선입선출).
public class CenterFeedbackUi : MonoBehaviour
{
    public static CenterFeedbackUi Instance { get; private set; }

    [SerializeField] private RectTransform template;
    [SerializeField] private int maxActive = 6;
    [SerializeField] private float riseDistance = 60f;
    [SerializeField] private float holdDuration = 0.4f; // 다 뜬 채로 가만히 있는 시간
    [SerializeField] private float fadeDuration = 0.6f; // 떠오르며 옅어지는 시간

    private Vector2 basePosition;
    private readonly Stack<RectTransform> pool = new();
    private readonly Queue<RectTransform> active = new(); // 뜬 순서대로 - 맨 앞이 가장 오래된 것
    private readonly Dictionary<RectTransform, CancellationTokenSource> playing = new();

    private void Awake()
    {
        Instance = this;

        basePosition = template.anchoredPosition;
        template.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string messageKey)
    {
        if (string.IsNullOrEmpty(messageKey)) return;

        if (TooltipUi.Instance != null) TooltipUi.Instance.Hide(); // 클릭 지점 문구와 겹쳐 보이지 않도록 떠 있던 호버 툴팁을 닫는다.

        RectTransform instance = Rent();
        active.Enqueue(instance);

        instance.anchoredPosition = basePosition;

        var text = instance.GetComponent<TextMeshProUGUI>();
        text.text = DataTableManager.StringTable.Get(messageKey);

        var canvasGroup = instance.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        instance.gameObject.SetActive(true);

        var cts = new CancellationTokenSource();
        playing[instance] = cts;

        Play(instance, canvasGroup, cts.Token).Forget();
    }

    // 풀에 반납된 여분이 있으면 그걸 쓰고, 없으면 maxActive 안에서는 새로 Instantiate한다.
    // 이미 maxActive만큼 다 떠 있으면 가장 먼저 뜬 것(active 맨 앞)을 취소하고 그 인스턴스를 그대로 재사용한다.
    private RectTransform Rent()
    {
        if (pool.Count > 0) return pool.Pop();

        if (active.Count < maxActive)
        {
            RectTransform created = Instantiate(template, template.parent);

            CanvasGroup canvasGroup = created.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = created.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            return created;
        }

        RectTransform oldest = active.Dequeue();
        if (playing.TryGetValue(oldest, out var oldestCts))
        {
            oldestCts.Cancel();
            oldestCts.Dispose();
            playing.Remove(oldest);
        }

        return oldest;
    }

    // 캔슬되면(재사용으로 가로채인 경우) 여기서 곧장 반환하고, 뒤이어 시작한 새 Show()의 Play()가
    // 이 인스턴스를 대신 이어받는다 - active 목록/풀 반납은 자연 종료된 쪽만 건드린다.
    private async UniTaskVoid Play(RectTransform instance, CanvasGroup canvasGroup, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(holdDuration), ignoreTimeScale: true, cancellationToken: token);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeDuration;
                instance.anchoredPosition = basePosition + new Vector2(0f, riseDistance * t);
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        playing.Remove(instance);
        active.Dequeue(); // 지속 시간이 전부 동일해 선입선출로 끝나므로, 자연 종료 시점엔 항상 자기 자신이 맨 앞이다.
        instance.gameObject.SetActive(false);
        pool.Push(instance);
    }
}
