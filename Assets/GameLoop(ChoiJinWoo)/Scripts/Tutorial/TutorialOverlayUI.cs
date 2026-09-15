using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면은 그대로 두고 스포트라이트 대상(target) 둘레에 색상 테두리만 그려서 강조하는 튜토리얼
// 오버레이. dimTop/Bottom/Left/Right/fullscreenBlocker는 더 이상 화면을 어둡게 칠하지 않는다(색
// 알파를 0으로 만들어 완전히 투명하게 둔다) - 대신 타겟 둘레를 뺀 나머지 화면 전체를 계속 덮어
// raycastTarget으로 클릭만 차단하는 "투명 차단막" 역할만 한다. 실제로 눈에 보이는 강조는
// highlightTop/Bottom/Left/Right 4장이 타겟 사각형 테두리를 얇게 두르는 것으로 대신한다(raycastTarget
// 꺼짐 - 시각 전용이라 클릭을 가로채면 안 된다).
//
// 인스펙터 요구사항: dimmerRoot는 부모(overlayCanvas)에 풀스트레치 + pivot(0.5,0.5)로 두고,
// dimTop/Bottom/Left/Right/fullscreenBlocker/highlightTop/Bottom/Left/Right/messageBox는 전부
// dimmerRoot의 자식으로 anchorMin=anchorMax=(0.5,0.5)로 둔다 - 그래야 dimmerRoot.rect의 로컬
// 좌표가 곧 anchoredPosition이 된다.
public class TutorialOverlayUI : MonoBehaviour
{
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private RectTransform dimmerRoot;
    [SerializeField] private RectTransform dimTop;
    [SerializeField] private RectTransform dimBottom;
    [SerializeField] private RectTransform dimLeft;
    [SerializeField] private RectTransform dimRight;
    [SerializeField] private RectTransform fullscreenBlocker; // target이 하나도 안 잡힐 때(패널 전환 도중 등) 전체 차단 폴백
    [SerializeField] private RectTransform messageBox;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button acknowledgeButton; // "다음" - completesOnAcknowledge 단계에서만 보인다
    [SerializeField] private Vector2 messageOffset = new(24f, 24f);

    [Tooltip("타겟 사각형 둘레를 두르는 강조 테두리 4장(상하좌우). dimTop/Bottom/Left/Right와 같은 " +
        "구조(anchorMin=anchorMax=(0.5,0.5))로 dimmerRoot 밑에 두되, raycastTarget은 꺼서 타겟 클릭을 " +
        "가로채지 않게 한다 - 실제 클릭 차단은 여전히 투명해진 dim 4장이 담당한다.")]
    [SerializeField] private RectTransform highlightTop;
    [SerializeField] private RectTransform highlightBottom;
    [SerializeField] private RectTransform highlightLeft;
    [SerializeField] private RectTransform highlightRight;
    [SerializeField] private float highlightBorderThickness = 4f;
    [SerializeField] private Color highlightBorderColor = new(1f, 0.82f, 0.2f, 1f);

    [Tooltip("테두리가 숨쉬듯 밝아졌다 옅어지는 펄스 속도/최저 밝기(0~1, highlightBorderColor의 알파에 곱해짐). " +
        "Time.unscaledTime 기준이라 튜토리얼이 Time.timeScale을 0으로 멈추는 구간(pauseTimeWhileActive)에도 " +
        "계속 애니메이션된다.")]
    [SerializeField] private float highlightPulseSpeed = 2.5f;
    [SerializeField, Range(0f, 1f)] private float highlightPulseMinAlpha = 0.35f;

    [Tooltip("한 줄에 허용할 최대 글자 수(공백 포함). messageBox가 ContentSizeFitter(PreferredSize)로 " +
        "텍스트 폭에 맞춰 늘어나는 구조라 TMP 자동 줄바꿈이 걸리지 않으므로, 표시 직전에 이 길이를 " +
        "넘는 줄을 직접 잘라 넣는다. StringTable에 저작해둔 \\n(문단 구분)은 그대로 존중한다.")]
    [SerializeField] private int maxLineLength = 20;

    public event Action AcknowledgeClicked;

    // "다음" 버튼으로 넘어가는 단계는 진행에 타겟 클릭이 필요 없으니, 안내창이 도저히 안 들어가는
    // 상황에서는 화면 밖으로 나가는 것보다 타겟 쪽으로 살짝 겹치는 걸 감수한다(ClampPreferring 참고).
    // 반대로 실제 target 클릭으로 완료되는 단계는 겹치면 클릭이 막히니 되도록 겹치지 않으려 하되,
    // 어느 쪽이든 화면 경계를 벗어나는 것보다는 우선순위가 낮다 - 안내창이 아예 안 보이는 것보다
    // 타겟과 살짝 겹치는 채로라도 화면 안에 보이는 쪽이 낫다.
    private bool targetClickRequired;

    private void Awake()
    {
        acknowledgeButton.onClick.AddListener(() => AcknowledgeClicked?.Invoke());

        HideDimVisually(dimTop);
        HideDimVisually(dimBottom);
        HideDimVisually(dimLeft);
        HideDimVisually(dimRight);
        HideDimVisually(fullscreenBlocker);

        SetupHighlightBorder(highlightTop);
        SetupHighlightBorder(highlightBottom);
        SetupHighlightBorder(highlightLeft);
        SetupHighlightBorder(highlightRight);

        gameObject.SetActive(false);
    }

    // 딤은 더 이상 화면을 어둡게 칠하지 않는다 - Image 색 알파만 0으로 만들어 완전히 투명하게 두되,
    // raycastTarget은 인스펙터 설정 그대로 둬서(보통 true) 클릭 차단 역할은 그대로 유지한다.
    private static void HideDimVisually(RectTransform rt)
    {
        if (rt != null && rt.TryGetComponent(out Image image))
        {
            Color c = image.color;
            c.a = 0f;
            image.color = c;
        }
    }

    // 하이라이트 테두리는 순수 시각 요소다 - 타겟 클릭을 가로채면 안 되므로 raycastTarget을 끈다.
    private void SetupHighlightBorder(RectTransform rt)
    {
        if (rt != null && rt.TryGetComponent(out Image image))
        {
            image.color = highlightBorderColor;
            image.raycastTarget = false;
        }
    }

    public void Show(bool showAcknowledgeButton)
    {
        gameObject.SetActive(true);
        acknowledgeButton.gameObject.SetActive(showAcknowledgeButton);
        targetClickRequired = !showAcknowledgeButton;
    }

    public void SetMessage(string messageKey)
    {
        string message = DataTableManager.StringTable.Get(messageKey);
        messageText.text = WrapLongLines(message, maxLineLength);
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageBox);
    }

    // 문단(\n)은 그대로 두고, 문단 각각을 maxLineLength 기준으로 다시 줄바꿈한다.
    private static string WrapLongLines(string text, int maxLineLength)
    {
        string[] paragraphs = text.Split('\n');
        for (int i = 0; i < paragraphs.Length; i++)
        {
            paragraphs[i] = WrapParagraph(paragraphs[i], maxLineLength);
        }
        return string.Join("\n", paragraphs);
    }

    // 공백 단위로 단어를 누적하다가, 다음 단어를 더하면 maxLineLength를 넘는 지점에서 줄바꿈한다.
    // 일본어처럼 애초에 공백이 없는 언어는 문단 전체가 "단어" 하나로 들어와 maxLineLength를 훨씬
    // 넘어도 공백 기준으로는 못 끊이므로, maxLineLength보다 긴 단어는 글자 수 기준으로 강제로 끊는다.
    private static string WrapParagraph(string paragraph, int maxLineLength)
    {
        if (paragraph.Length <= maxLineLength) return paragraph;

        var sb = new StringBuilder(paragraph.Length + 4);
        int lineLength = 0;
        string[] words = paragraph.Split(' ');
        foreach (string word in words)
        {
            if (lineLength > 0 && lineLength + 1 + word.Length > maxLineLength)
            {
                sb.Append('\n');
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                sb.Append(' ');
                lineLength += 1;
            }

            int index = 0;
            while (word.Length - index > maxLineLength - lineLength)
            {
                int take = Mathf.Max(1, maxLineLength - lineLength);
                sb.Append(word, index, take);
                sb.Append('\n');
                index += take;
                lineLength = 0;
            }
            sb.Append(word, index, word.Length - index);
            lineLength += word.Length - index;
        }
        return sb.ToString();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void ShowUnblocked()
    {
        dimTop.gameObject.SetActive(false);
        dimBottom.gameObject.SetActive(false);
        dimLeft.gameObject.SetActive(false);
        dimRight.gameObject.SetActive(false);
        fullscreenBlocker.gameObject.SetActive(false);
        SetHighlightActive(false);
    }

    // 매 프레임 TutorialManager가 호출한다 - target이 패널 토글로 나타났다 사라졌다 하므로 매번 다시 계산한다.
    public void SetSpotlight(RectTransform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            fullscreenBlocker.gameObject.SetActive(true);
            dimTop.gameObject.SetActive(false);
            dimBottom.gameObject.SetActive(false);
            dimLeft.gameObject.SetActive(false);
            dimRight.gameObject.SetActive(false);
            SetHighlightActive(false);

            PositionMessageBoxCenter();
            return;
        }

        fullscreenBlocker.gameObject.SetActive(false);
        dimTop.gameObject.SetActive(true);
        dimBottom.gameObject.SetActive(true);
        dimLeft.gameObject.SetActive(true);
        dimRight.gameObject.SetActive(true);
        SetHighlightActive(true);

        // 타겟 RectTransform이 화면 경계 밖으로 삐져나와 있으면(가로/세로로 크게 늘린 버튼 등),
        // 뚫어줄 구멍 사각형을 화면 경계로 클램프하지 않고 그대로 쓸 경우 dimLeft/dimRight(또는
        // dimTop/dimBottom)의 xMax(또는 yMax) < xMin(또는 yMin)이 되어 SetRect가 폭/높이를 0으로
        // 잘라버린다 - 그러면 그 방향의 딤이 완전히 사라져 화면 가장자리 클릭이 그대로 통과한다.
        Rect targetLocal = ClampRectToBounds(ComputeLocalRect(target), dimmerRoot.rect);
        LayoutDimmers(targetLocal);
        LayoutHighlightBorder(targetLocal);
        PositionMessageBox(targetLocal);
    }

    private void SetHighlightActive(bool active)
    {
        highlightTop.gameObject.SetActive(active);
        highlightBottom.gameObject.SetActive(active);
        highlightLeft.gameObject.SetActive(active);
        highlightRight.gameObject.SetActive(active);
    }

    // 테두리가 켜져 있는 동안만 밝기를 사인파로 진동시킨다 - GameSpeedMention 등 pauseTimeWhileActive
    // 스텝에서 Time.timeScale이 0이 돼도 계속 움직여야 하므로 Time.time이 아니라 unscaledTime을 쓴다.
    private void Update()
    {
        if (!highlightTop.gameObject.activeInHierarchy) return;

        float wave = (Mathf.Sin(Time.unscaledTime * highlightPulseSpeed) + 1f) * 0.5f; // 0..1
        float alpha = Mathf.Lerp(highlightPulseMinAlpha, 1f, wave) * highlightBorderColor.a;
        SetHighlightAlpha(alpha);
    }

    private void SetHighlightAlpha(float alpha)
    {
        SetImageAlpha(highlightTop, alpha);
        SetImageAlpha(highlightBottom, alpha);
        SetImageAlpha(highlightLeft, alpha);
        SetImageAlpha(highlightRight, alpha);
    }

    private void SetImageAlpha(RectTransform rt, float alpha)
    {
        if (rt != null && rt.TryGetComponent(out Image image))
        {
            Color c = highlightBorderColor;
            c.a = alpha;
            image.color = c;
        }
    }

    // target의 월드 코너 -> 스크린 좌표 -> dimmerRoot 로컬 좌표. 두 캔버스의 렌더 모드가 달라도 안전하다.
    private Rect ComputeLocalRect(RectTransform target)
    {
        // target이 막 활성화됐거나 Layout Group/ContentSizeFitter의 자식이면, 실제 리빌드는 이번 프레임
        // 렌더 직전에야 일어나서 GetWorldCorners가 아직 옛/기본 위치를 반환한다 - 그래서 첫 프레임엔
        // 스포트라이트가 엉뚱한 자리에 잡혔다가 다음 프레임에 제자리로 "튀는" 것처럼 보인다. 좌표를
        // 읽기 전에 대기 중인 리빌드를 강제로 끝내 같은 프레임 안에서 최종 위치가 나오게 한다.
        Canvas.ForceUpdateCanvases();

        var corners = new Vector3[4];
        target.GetWorldCorners(corners); // [0] bottom-left, [2] top-right

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCam = targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : targetCanvas.worldCamera;
        Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(targetCam, corners[0]);
        Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(targetCam, corners[2]);

        Camera overlayCam = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlayCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(dimmerRoot, screenMin, overlayCam, out var localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(dimmerRoot, screenMax, overlayCam, out var localMax);

        return Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);
    }

    // 타겟 사각형을 화면 경계 안으로 잘라낸다 - 타겟이 화면 밖으로 나가 있어도 각 변을 독립적으로
    // 클램프하므로 xMin<=xMax, yMin<=yMax가 항상 보장된다(뚫어줄 구멍이 경계를 넘어도 딤 폭이 0이나
    // 음수가 되지 않는다). 화면 밖 영역은 어차피 클릭할 수 없으니 그 부분까지 뚫어줄 필요도 없다.
    private static Rect ClampRectToBounds(Rect r, Rect bounds)
    {
        float xMin = Mathf.Clamp(r.xMin, bounds.xMin, bounds.xMax);
        float xMax = Mathf.Clamp(r.xMax, bounds.xMin, bounds.xMax);
        float yMin = Mathf.Clamp(r.yMin, bounds.yMin, bounds.yMax);
        float yMax = Mathf.Clamp(r.yMax, bounds.yMin, bounds.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // 타겟 사각형 둘레를 4장으로 감싼다 - 겹침도 빈틈도 없고, 타겟 자리에는 아무것도 그리지 않는다.
    private void LayoutDimmers(Rect t)
    {
        Rect full = dimmerRoot.rect;

        SetRect(dimTop, full.xMin, t.yMax, full.xMax, full.yMax);
        SetRect(dimBottom, full.xMin, full.yMin, full.xMax, t.yMin);
        SetRect(dimLeft, full.xMin, t.yMin, t.xMin, t.yMax);
        SetRect(dimRight, t.xMax, t.yMin, full.xMax, t.yMax);
    }

    // 타겟 사각형 테두리를 얇은 띠 4장으로 두른다 - dim은 이제 투명해서 클릭 차단 용도로만 남으니,
    // 실제로 뭘 봐야 하는지는 이 테두리가 알려준다. 모서리가 겹치도록 top/bottom은 폭 전체(코너 포함),
    // left/right는 그 사이 높이만 채운다.
    private void LayoutHighlightBorder(Rect t)
    {
        float half = highlightBorderThickness * 0.5f;
        SetRect(highlightTop, t.xMin - half, t.yMax - half, t.xMax + half, t.yMax + half);
        SetRect(highlightBottom, t.xMin - half, t.yMin - half, t.xMax + half, t.yMin + half);
        SetRect(highlightLeft, t.xMin - half, t.yMin - half, t.xMin + half, t.yMax + half);
        SetRect(highlightRight, t.xMax - half, t.yMin - half, t.xMax + half, t.yMax + half);
    }

    private static void SetRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        float width = Mathf.Max(0f, xMax - xMin);
        float height = Mathf.Max(0f, yMax - yMin);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
    }

    // 스포트라이트 대상이 없을 때(완료 메시지 등) 화면 정중앙에 고정한다.
    private void PositionMessageBoxCenter()
    {
        messageBox.pivot = new Vector2(0.5f, 0.5f);
        messageBox.anchoredPosition = dimmerRoot.rect.center;
    }

    // 배치/재배치 대기 중(맵 타일 클릭을 기다리는 동안, TutorialManager.ShowUnblockedMessage)에 쓴다 -
    // 그동안엔 스포트라이트가 없어 SetSpotlight가 아예 안 불리므로, 이전 프레임에 짚어주던 위치에
    // 안내창이 그대로 남아있게 된다. 맵 중앙을 가리지 않도록 화면 중앙 최하단으로 고정해서 보여준다.
    public void PositionMessageBoxBottomCenter()
    {
        Rect bounds = dimmerRoot.rect;
        messageBox.pivot = new Vector2(0.5f, 0f);
        messageBox.anchoredPosition = new Vector2(bounds.center.x, bounds.yMin + messageOffset.y);
    }

    private void PositionMessageBox(Rect targetLocal)
    {
        Rect bounds = dimmerRoot.rect;
        Vector2 size = messageBox.rect.size;

        float spaceTop = bounds.yMax - targetLocal.yMax;
        float spaceBottom = targetLocal.yMin - bounds.yMin;
        float spaceLeft = targetLocal.xMin - bounds.xMin;
        float spaceRight = bounds.xMax - targetLocal.xMax;

        // 남는 공간이 가장 넓은 방향이 아니라, 안내창이 실제로 그 공간에 다 들어가고도 얼마나
        // 남는지(공간 - 박스 크기 - 여백)로 방향을 고른다. HeroInventory처럼 폭이 넓은 타겟은
        // 좌우 여유가 넓어 보여도 안내창 폭보다 작아서 못 들어가면, 상/하처럼 실제로 들어가는
        // 방향을 우선해야 화면 밖으로 밀려나거나 타겟과 겹치지 않는다.
        float fitTop = spaceTop - size.y - messageOffset.y;
        float fitBottom = spaceBottom - size.y - messageOffset.y;
        float fitLeft = spaceLeft - size.x - messageOffset.x;
        float fitRight = spaceRight - size.x - messageOffset.x;
        float best = Mathf.Max(Mathf.Max(fitTop, fitBottom), Mathf.Max(fitLeft, fitRight));

        Vector2 anchor;
        Vector2 offset;
        if (best == fitTop)
        {
            anchor = new Vector2(targetLocal.center.x, targetLocal.yMax);
            messageBox.pivot = new Vector2(0.5f, 0f);
            offset = new Vector2(0f, messageOffset.y);
        }
        else if (best == fitBottom)
        {
            anchor = new Vector2(targetLocal.center.x, targetLocal.yMin);
            messageBox.pivot = new Vector2(0.5f, 1f);
            offset = new Vector2(0f, -messageOffset.y);
        }
        else if (best == fitLeft)
        {
            anchor = new Vector2(targetLocal.xMin, targetLocal.center.y);
            messageBox.pivot = new Vector2(1f, 0.5f);
            offset = new Vector2(-messageOffset.x, 0f);
        }
        else
        {
            anchor = new Vector2(targetLocal.xMax, targetLocal.center.y);
            messageBox.pivot = new Vector2(0f, 0.5f);
            offset = new Vector2(messageOffset.x, 0f);
        }

        messageBox.anchoredPosition = ClampToBounds(anchor + offset, targetLocal);
    }

    // 화면 경계를 항상 최우선으로 지킨다 - 안내창이 화면 밖으로 나가는 일은 없어야 한다. 타겟과
    // 안 겹치는 것(target을 클릭해야 진행되는 단계에서 클릭을 막지 않기 위함)은 "가능하면" 지키는
    // 2순위 선호일 뿐이라, 화면 안에 두면서 동시에 타겟도 피할 공간이 없을 때는 화면 안에 두는
    // 쪽을 선택하고 타겟과의 겹침은 감수한다 - 완전히 화면 밖으로 사라지는 것보다 타겟과 살짝
    // 겹친 채로라도 보이는 편이 사용자가 진행 상황을 파악하기에 낫다.
    private Vector2 ClampToBounds(Vector2 desired, Rect targetLocal)
    {
        Rect bounds = dimmerRoot.rect;
        Vector2 anchorPoint = bounds.min + Vector2.Scale(bounds.size, messageBox.anchorMin);
        Vector2 pivotOffset = Vector2.Scale(messageBox.rect.size, messageBox.pivot);

        float minX = bounds.xMin - anchorPoint.x + pivotOffset.x;
        float maxX = bounds.xMax - anchorPoint.x - (messageBox.rect.width - pivotOffset.x);
        float minY = bounds.yMin - anchorPoint.y + pivotOffset.y;
        float maxY = bounds.yMax - anchorPoint.y - (messageBox.rect.height - pivotOffset.y);

        float x;
        if (messageBox.pivot.x == 1f) // 타겟 좌측에 배치 - 가능하면 오른쪽 경계(=박스 우측)가 타겟 좌측을 안 넘게.
        {
            float? preferredMax = targetClickRequired ? Mathf.Min(maxX, targetLocal.xMin) : (float?)null;
            x = ClampPreferring(desired.x, minX, maxX, null, preferredMax);
        }
        else if (messageBox.pivot.x == 0f) // 타겟 우측에 배치 - 가능하면 왼쪽 경계(=박스 좌측)가 타겟 우측을 안 넘게.
        {
            float? preferredMin = targetClickRequired ? Mathf.Max(minX, targetLocal.xMax) : (float?)null;
            x = ClampPreferring(desired.x, minX, maxX, preferredMin, null);
        }
        else
        {
            x = ClampPreferring(desired.x, minX, maxX, null, null);
        }

        float y;
        if (messageBox.pivot.y == 0f) // 타겟 상단에 배치 - 가능하면 아래쪽 경계(=박스 하단)가 타겟 상단을 안 넘게.
        {
            float? preferredMin = targetClickRequired ? Mathf.Max(minY, targetLocal.yMax) : (float?)null;
            y = ClampPreferring(desired.y, minY, maxY, preferredMin, null);
        }
        else if (messageBox.pivot.y == 1f) // 타겟 하단에 배치 - 가능하면 위쪽 경계(=박스 상단)가 타겟 하단을 안 넘게.
        {
            float? preferredMax = targetClickRequired ? Mathf.Min(maxY, targetLocal.yMin) : (float?)null;
            y = ClampPreferring(desired.y, minY, maxY, null, preferredMax);
        }
        else
        {
            y = ClampPreferring(desired.y, minY, maxY, null, null);
        }

        return new Vector2(x, y);
    }

    // screenMin/screenMax(화면 경계, 항상 지켜야 함) 안에서, preferredMin/preferredMax(타겟 회피,
    // 되면 좋은 것)까지 같이 만족하는 값이 있으면 그걸 쓰고, 없으면 화면 경계만 지키는 값으로
    // 물러난다. screenMin > screenMax(안내창 자체가 화면보다 큼)인 극단적인 경우에만 최소한으로 넘친다.
    private static float ClampPreferring(float desired, float screenMin, float screenMax, float? preferredMin, float? preferredMax)
    {
        float rMin = preferredMin ?? screenMin;
        float rMax = preferredMax ?? screenMax;
        if (rMin <= rMax) return Mathf.Clamp(desired, rMin, rMax);
        if (screenMin <= screenMax) return Mathf.Clamp(desired, screenMin, screenMax);
        return screenMin;
    }
}
