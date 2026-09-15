using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 게임 화면에서 마우스를 따라다니는 커스텀 커서의 위치와 눌림 상태를 갱신한다.
public class CustomCursorFollow : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Animator cursorAnimator;

    private static readonly int PressedParameter = Animator.StringToHash("IsPressed");

    private RectTransform cursorRect;

#if UNITY_EDITOR
    private bool isGameView;
#endif

    // 커서 자신의 RectTransform을 캐시한다.
    private void Awake()
    {
        cursorRect = (RectTransform)transform;
    }

    // 현재 실행 환경에 맞는 커서 표시 상태를 적용한다.
    private void OnEnable()
    {
#if UNITY_EDITOR
        isGameView = IsGameView();
        ApplyView();
#else
        Cursor.visible = false;
#endif
    }

    // 비활성화되면 시스템 커서와 커서 이미지를 복구한다.
    private void OnDisable()
    {
        Cursor.visible = true;
        cursorAnimator.gameObject.SetActive(true);
    }

    // 게임 화면에서만 커서 위치와 눌림 상태를 갱신한다.
    private void Update()
    {
#if UNITY_EDITOR
        UpdateView();

        if (!isGameView)
        {
            return;
        }
#endif

        cursorRect.anchoredPosition = ScreenToCanvasPosition(Mouse.current.position.ReadValue());
        cursorAnimator.SetBool(PressedParameter, Mouse.current.leftButton.isPressed);
    }

#if UNITY_EDITOR
    // 마우스 아래 에디터 창이 Game 뷰인지 반환한다.
    private bool IsGameView()
    {
        EditorWindow window = EditorWindow.mouseOverWindow;

        if (window == null)
        {
            return false;
        }

        return window.GetType().FullName == "UnityEditor.GameView";
    }

    // Game 뷰 진입 상태가 달라졌을 때 표시 상태를 변경한다.
    private void UpdateView()
    {
        bool nextView = IsGameView();

        if (isGameView == nextView)
        {
            return;
        }

        isGameView = nextView;
        ApplyView();
    }

    // Game 뷰에서는 UI 커서를, 그 밖에서는 시스템 커서를 표시한다.
    private void ApplyView()
    {
        cursorAnimator.gameObject.SetActive(isGameView);
        Cursor.visible = !isGameView;
    }
#endif

    // 화면 좌표를 캔버스 기준 로컬 좌표로 계산한다.
    private Vector2 ScreenToCanvasPosition(Vector2 screenPosition)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out localPoint);
        return localPoint;
    }
}
