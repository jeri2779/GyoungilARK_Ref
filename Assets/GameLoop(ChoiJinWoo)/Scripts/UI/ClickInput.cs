using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 왼쪽 클릭이 발생한 순간 화면 좌표를 전달한다.
public class ClickInput : IDisposable
{
    private readonly InputAction clickAction;
    private bool isEnabled;

    public event Action<Vector2> Clicked;

    // 마우스 왼쪽 버튼 전용 입력을 만든다.
    public ClickInput()
    {
        clickAction = new InputAction(
            "Click",
            InputActionType.Button,
            "<Mouse>/leftButton");
    }

    // 클릭 이벤트 수신을 시작한다.
    public void Enable()
    {
        if (isEnabled)
        {
            return;
        }

        clickAction.performed += OnPerformed;
        clickAction.Enable();
        isEnabled = true;
    }

    // 클릭 이벤트 수신을 중지한다.
    public void Disable()
    {
        if (!isEnabled)
        {
            return;
        }

        clickAction.Disable();
        clickAction.performed -= OnPerformed;
        isEnabled = false;
    }

    // 입력 자원을 정리한다.
    public void Dispose()
    {
        Disable();
        clickAction.Dispose();
    }

    // 클릭한 마우스의 화면 좌표를 알린다.
    private void OnPerformed(InputAction.CallbackContext context)
    {
        Mouse mouse = (Mouse)context.control.device;
        Clicked?.Invoke(mouse.position.ReadValue());
    }
}
