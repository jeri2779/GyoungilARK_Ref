using UnityEngine;
using UnityEngine.InputSystem;

// 누른 지점에서 충분히 움직였으면 드래그로 판정(제자리 클릭과 구분).
public class DragDetect
{
    private Vector2 _pressPos;
    private readonly float _threshold;

    public DragDetect(float thresholdPixels)
    {
        _threshold = thresholdPixels;
    }

    public void MarkPress()
    {
        _pressPos = Mouse.current.position.ReadValue();
    }

    public bool MovedEnough()
    {
        Vector2 moved = Mouse.current.position.ReadValue() - _pressPos;
        return moved.magnitude > _threshold;
    }
}
