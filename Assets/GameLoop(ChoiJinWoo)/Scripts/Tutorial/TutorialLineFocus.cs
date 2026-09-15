using UnityEngine;

public class TutorialLineFocus : MonoBehaviour
{
    [SerializeField] private RectTransform topLine;
    [SerializeField] private RectTransform bottomLine;
    [SerializeField] private RectTransform leftLine;
    [SerializeField] private RectTransform rightLine;

    [SerializeField, Min(0.01f)] private float moveTime = 0.6f;
    [SerializeField, Min(0f)] private float holdTime = 0.2f;
    [SerializeField, Min(1f)] private float startScale = 1.5f;

    private float elapsed;
    private bool isMoving;

    // 강조선이 보이는 동안 이동 사각형을 갱신합니다.
    private void LateUpdate()
    {
        if (!CanMove())
        {
            StopMove();
            return;
        }

        if (!isMoving)
        {
            BeginMove();
        }

        Rect target = ReadRect();
        AdvanceTime();
        Rect current = GetRect(target);
        ApplyRect(current);
    }

    // 컴포넌트가 꺼질 때 이동 상태를 초기화합니다.
    private void OnDisable()
    {
        StopMove();
    }

    // 강조선 이동을 실행할 수 있는지 반환합니다.
    private bool CanMove()
    {
        if (topLine == null || bottomLine == null)
        {
            return false;
        }

        if (leftLine == null || rightLine == null)
        {
            return false;
        }

        if (!topLine.gameObject.activeInHierarchy)
        {
            return false;
        }

        return bottomLine.gameObject.activeInHierarchy
            && leftLine.gameObject.activeInHierarchy
            && rightLine.gameObject.activeInHierarchy;
    }

    // 기존 강조선이 만든 최종 사각형을 반환합니다.
    private Rect ReadRect()
    {
        Vector2 topSize = topLine.sizeDelta;
        Vector2 leftSize = leftLine.sizeDelta;
        Vector2 topPos = topLine.anchoredPosition;
        Vector2 leftPos = leftLine.anchoredPosition;

        float xMin = topPos.x - topSize.x * 0.5f;
        float xMax = topPos.x + topSize.x * 0.5f;
        float yMin = leftPos.y - leftSize.y * 0.5f;
        float yMax = leftPos.y + leftSize.y * 0.5f;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // 강조선 반복 이동을 처음부터 시작합니다.
    private void BeginMove()
    {
        elapsed = 0f;
        isMoving = true;
    }

    // 일시 정지와 무관한 시간으로 이동 시간을 갱신합니다.
    private void AdvanceTime()
    {
        float totalTime = moveTime + holdTime;
        elapsed += Time.unscaledDeltaTime;
        elapsed = Mathf.Repeat(elapsed, totalTime);
    }

    // 현재 이동 진행도를 반환합니다.
    private float GetProgress()
    {
        if (elapsed >= moveTime)
        {
            return 1f;
        }

        float ratio = elapsed / moveTime;
        float remain = 1f - ratio;
        return 1f - remain * remain * remain;
    }

    // 최종 사각형을 기준으로 현재 이동 사각형을 반환합니다.
    private Rect GetRect(Rect target)
    {
        float progress = GetProgress();
        float scale = Mathf.Lerp(startScale, 1f, progress);
        Vector2 size = target.size * scale;
        Vector2 center = target.center;
        Vector2 half = size * 0.5f;

        return Rect.MinMaxRect(
            center.x - half.x,
            center.y - half.y,
            center.x + half.x,
            center.y + half.y);
    }

    // 현재 사각형을 강조선 네 방향에 적용합니다.
    private void ApplyRect(Rect current)
    {
        float thickness = topLine.sizeDelta.y;

        SetRect(topLine, current.xMin, current.yMax - thickness, current.xMax, current.yMax);
        SetRect(bottomLine, current.xMin, current.yMin, current.xMax, current.yMin + thickness);
        SetRect(leftLine, current.xMin, current.yMin, current.xMin + thickness, current.yMax);
        SetRect(rightLine, current.xMax - thickness, current.yMin, current.xMax, current.yMax);
    }

    // 전달받은 경계로 강조선의 크기와 위치를 설정합니다.
    private void SetRect(RectTransform line, float xMin, float yMin, float xMax, float yMax)
    {
        float width = Mathf.Max(0f, xMax - xMin);
        float height = Mathf.Max(0f, yMax - yMin);

        line.sizeDelta = new Vector2(width, height);
        line.anchoredPosition = new Vector2(
            (xMin + xMax) * 0.5f,
            (yMin + yMax) * 0.5f);
    }

    // 강조선 이동 시간과 실행 상태를 초기화합니다.
    private void StopMove()
    {
        elapsed = 0f;
        isMoving = false;
    }
}
