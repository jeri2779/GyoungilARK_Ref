using UnityEngine;

/// <summary>
/// WARNING 연출의 좌/우 화살표를 각자 시작점 → 끝점으로 이동시킨다.
///
/// AnimationClip이 아니라 코드로 도는 이유: 화살표가 붙어 있는 Directing 루트의 클립
/// (BossEnterDirecting.anim)은 Warning Builder가 구울 때마다 ClearCurves로 전부 지우기 때문에,
/// 거기에 손으로 찍은 커브는 다음 굽기에 날아간다. 모션을 클립 밖에 두면 그 문제가 없다.
///
/// 좌우를 따로 잡는 이유: 두 화살표는 Arrows(부모)의 자식이라 부모를 움직이면 같이 끌려간다.
/// 각자 자기 anchoredPosition으로 움직여야 서로 반대 방향(대각선 맞물림)이 나온다.
///
/// 보스 연출 중엔 Time.timeScale=0이므로 시간 소스는 unscaled 고정이다.
/// 켜고 끄는 건 여기서 하지 않는다 — Directing 루트의 CanvasGroup 알파 커브가 자식 전체에 걸리므로
/// 페이드인/아웃은 저절로 WARNING과 같은 타이밍에 맞는다.
/// </summary>
[DisallowMultipleComponent]
public class WarningArrowSlide : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private RectTransform rightArrow;
    [SerializeField] private RectTransform leftArrow;

    [Header("오른쪽 화살표 (anchoredPosition)")]
    [SerializeField] private Vector2 rightStart = new Vector2(310f, 547f);
    [SerializeField] private Vector2 rightEnd = new Vector2(1190f, 734f); 

    [Header("왼쪽 화살표 (anchoredPosition)")]
    [SerializeField] private Vector2 leftStart = new Vector2(-1056f, 248f);
    [SerializeField] private Vector2 leftEnd = new Vector2(-1936f, 61f);

    [Header("타이밍 (초) — 연출 중 timeScale=0이라 항상 unscaled")]
    [Tooltip("Play() 후 이만큼 기다렸다 움직이기 시작한다. 그동안은 시작점에 멈춰 있다.")]
    [SerializeField] private float delay = 0f;
    [SerializeField] private float duration = 0.5f;
    [Tooltip("0→1 구간의 가속. 기본은 시작·끝이 부드러운 EaseInOut.")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float _elapsed;
    private bool _playing;

    /// <summary>연출 시작. 시작점으로 되돌린 뒤 delay를 기다렸다 끝점까지 이동한다. 재호출하면 처음부터 다시.</summary>
    public void Play()
    {
        _elapsed = 0f;
        _playing = true;
        Apply(0f);
    }

    /// <summary>
    /// 진행 중인 이동만 끊는다. 위치는 건드리지 않는다 —
    /// 연출이 정상적으로 끝났을 때 화살표를 시작점으로 되돌리면, 아직 안 사라진 상태에서
    /// 순간이동하는 게 보인다. 다음 연출의 시작 위치는 Play()가 알아서 맞춘다.
    /// 취소로 중간에 끊겼을 때 Update가 계속 도는 걸 막는 게 목적이다.
    /// </summary>
    public void Stop() => _playing = false;

    /// <summary>이동을 멈추고 시작점으로 되돌린다(수동 리셋용).</summary>
    public void ResetToStart()
    {
        _playing = false;
        _elapsed = 0f;
        Apply(0f);
    }

    private void Update()
    {
        if (!_playing) return;

        // 스케일드 시간은 연출 중(timeScale=0) delta가 0이라 영원히 안 끝난다.
        _elapsed += Time.unscaledDeltaTime;

        float dur = Mathf.Max(0.0001f, duration);
        if (_elapsed >= delay + dur)
        {
            Apply(1f);
            _playing = false;
            return;
        }
        Apply(Mathf.Clamp01((_elapsed - delay) / dur));   // delay 동안은 음수 → 0으로 잘려 시작점 유지
    }

    // t=0 시작점 / t=1 끝점. ease가 1을 넘거나 0 밑으로 내려가는 커브(오버슈트)여도 쓸 수 있게 Unclamped.
    private void Apply(float t)
    {
        float k = ease != null ? ease.Evaluate(t) : t;
        if (rightArrow != null) rightArrow.anchoredPosition = Vector2.LerpUnclamped(rightStart, rightEnd, k);
        if (leftArrow != null) leftArrow.anchoredPosition = Vector2.LerpUnclamped(leftStart, leftEnd, k);
    }

#if UNITY_EDITOR
    // ── 수치 잡기용 (컴포넌트 우클릭 메뉴) ──────────────────────────
    // 씬 뷰에서 화살표를 원하는 자리로 끌어다 놓고 아래 항목을 눌러 그 좌표를 그대로 받아 적는다.

    [ContextMenu("시작점 ← 지금 화살표 위치")]
    private void CaptureStart()
    {
        UnityEditor.Undo.RecordObject(this, "Capture Arrow Start");
        if (rightArrow != null) rightStart = rightArrow.anchoredPosition;
        if (leftArrow != null) leftStart = leftArrow.anchoredPosition;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("끝점 ← 지금 화살표 위치")]
    private void CaptureEnd()
    {
        UnityEditor.Undo.RecordObject(this, "Capture Arrow End");
        if (rightArrow != null) rightEnd = rightArrow.anchoredPosition;
        if (leftArrow != null) leftEnd = leftArrow.anchoredPosition;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("미리보기 — 시작점")]
    private void PreviewStart() => PreviewAt(0f);

    [ContextMenu("미리보기 — 끝점")]
    private void PreviewEnd() => PreviewAt(1f);

    // 플레이 중이면 Play()가 자연스럽고, 에디터 정지 상태에선 Update가 안 돌아 즉시 배치만 한다.
    [ContextMenu("▶ 재생 테스트")]
    private void PreviewPlay()
    {
        if (Application.isPlaying) Play();
        else PreviewAt(0f);
    }

    private void PreviewAt(float t)
    {
        if (rightArrow != null) UnityEditor.Undo.RecordObject(rightArrow, "Preview Arrow");
        if (leftArrow != null) UnityEditor.Undo.RecordObject(leftArrow, "Preview Arrow");
        Apply(t);
    }
#endif
}
