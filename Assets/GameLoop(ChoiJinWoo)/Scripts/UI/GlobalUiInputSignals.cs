using System;
using UnityEngine.InputSystem;

// 마우스 클릭/ESC를 매 프레임 폴링하던 각 패널의 Update()를 대체하는 공용 입력 신호.
// TutorialInputGate/ExclusiveUiCoordinator와 같은 정적 서비스 패턴을 따른다 - Title 씬은
// VContainer 컨테이너가 없어 DI로 배선할 수 없기 때문이다.
// Main 씬은 UiManager.Awake/OnDestroy가, Title 씬은 TitleUI.OnEnable/OnDisable이 Enable/Disable을 호출한다.
public static class GlobalUiInputSignals
{
    private static InputAction clickAction;
    private static InputAction escapeAction;
    private static bool enabled;

    public static event Action ClickPerformed;
    public static event Action EscapePerformed;

    public static void Enable()
    {
        if (enabled) return;
        enabled = true;

        clickAction = new InputAction("GlobalClick", InputActionType.Button, "<Mouse>/leftButton");
        clickAction.performed += OnClickPerformed;
        clickAction.Enable();

        escapeAction = new InputAction("GlobalEscape", binding: "<Keyboard>/escape");
        escapeAction.performed += OnEscapePerformed;
        escapeAction.Enable();
    }

    public static void Disable()
    {
        if (!enabled) return;
        enabled = false;

        clickAction.performed -= OnClickPerformed;
        clickAction.Disable();
        clickAction.Dispose();
        clickAction = null;

        escapeAction.performed -= OnEscapePerformed;
        escapeAction.Disable();
        escapeAction.Dispose();
        escapeAction = null;
    }

    private static void OnClickPerformed(InputAction.CallbackContext context) => ClickPerformed?.Invoke();
    private static void OnEscapePerformed(InputAction.CallbackContext context) => EscapePerformed?.Invoke();
}
