using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 마우스 왼쪽 버튼의 눌림/뗌을 매 프레임 감지해 알린다.
public class MapInput : MonoBehaviour
{
    public event Action Pressed;
    public event Action Released;
    public event Action RightPressed;
    public event Action RightReleased;

    [SerializeField] private bool _blocked;
    private bool uiHolding;

    public bool Blocked
    {
        get { return _blocked; }
    }

    public bool OverUI
    {
        get
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
        }
    }

    public bool WorldBlocked
    {
        get { return _blocked || OverUI || uiHolding; }
    }

    public bool LeftHolding => Mouse.current.leftButton.isPressed;

    private void Update()
    {
        TrackUI();
        if (WorldBlocked)
        {
            ClearUI();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame) Pressed?.Invoke();
        if (Mouse.current.leftButton.wasReleasedThisFrame) Released?.Invoke();
        if (Mouse.current.rightButton.wasPressedThisFrame) RightPressed?.Invoke();
        if (Mouse.current.rightButton.wasReleasedThisFrame) RightReleased?.Invoke();
        ClearUI();
    }

    // UI에서 시작한 마우스 조작을 기록합니다.
    private void TrackUI()
    {
        bool pressed = Mouse.current.leftButton.wasPressedThisFrame
            || Mouse.current.rightButton.wasPressedThisFrame;

        if (pressed && OverUI)
        {
            uiHolding = true;
        }
    }

    // UI에서 시작한 마우스 조작이 끝났으면 기록을 지웁니다.
    private void ClearUI()
    {
        bool released = !Mouse.current.leftButton.isPressed
            && !Mouse.current.rightButton.isPressed;

        if (released)
        {
            uiHolding = false;
        }
    }

    public void SetBlock(bool value)
    {
        _blocked = value;
    }

    
  
}
