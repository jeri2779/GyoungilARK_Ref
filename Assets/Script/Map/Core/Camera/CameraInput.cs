using System;
using UnityEngine;
using UnityEngine.InputSystem;


// 마우스·키보드로 카메라를 조작한다.
// 우드래그·WASD = 평면 이동, QE = 높이, 휠 = 줌.
public class CameraInput : MonoBehaviour
{
    private CameraRig rig;   // 같은 오브젝트에 붙는 고정 관계라 Awake에서 잡는다(배선 불필요)

    // 사용자가 카메라를 건드린 프레임에 알린다. 자동 이동(ExpandFocus)이 이걸 듣고 손을 뗀다.
    // 이벤트라서 여기서는 듣는 쪽을 모른다 — ExpandFocus를 떼어내도 이 파일은 그대로다.
    public event Action UserMoved; // 사용자가 카메라를 건드린 프레임에 알린다. 자동 이동(ExpandFocus)이 이걸 듣고 손을 뗀다.

    [Header("Sensitivity")]
    [Tooltip("휠 한 칸당 거리 변화 비율.")]
    [Range(0.02f, 0.4f)] public float zoomStep = 0.1f;
    public float dragSpeed = 1.5f;
    public float keySpeed = 20f;

    // 시작할 때 같은 오브젝트에 붙은 CameraRig를 챙긴다.
    private void Awake()
    {
        rig = GetComponent<CameraRig>();
    }

    // 매 프레임 입력(이동·줌)을 읽어 카메라 값에 반영한다.
    private void Update()
    {
        bool moved = Pan();
        moved |= Zoom();

        // 입력이 있었던 프레임에만 필드를 되받아쓴다.
        // 매 프레임 클램프하면 Play 중 인스펙터 타이핑을 덮어써서 값이 튄다.
        if (moved)
        {
            rig.ClampState();
            UserMoved?.Invoke();   // 사용자가 조작하면 자동 이동을 놓아준다
        }
        rig.ApplyNow();
    }

    // 우클릭 드래그 = 화면 기준 팬. WASD = 수평 팬, QE = 높이.
    private bool Pan()
    {
        Transform view = rig.transform;
        bool moved = false;

        // 지면(XZ)에 투영한 축. 카메라 up을 그대로 쓰면 pitch만큼 월드 Y가 섞여
        // 위아래 드래그에 맵이 뜨거나 가라앉는다. 높이는 QE로만 바꾼다.
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta != Vector2.zero)
            {
                float scale = rig.distance * 0.002f * dragSpeed;
                rig.focus += (-right * delta.x - forward * delta.y) * scale;
                moved = true;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return moved;
        }

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) { move += forward; }
        if (keyboard.sKey.isPressed) { move -= forward; }
        if (keyboard.dKey.isPressed) { move += right; }
        if (keyboard.aKey.isPressed) { move -= right; }
        if (move != Vector3.zero)
        {
            rig.focus += move.normalized * (keySpeed * Time.unscaledDeltaTime);
            moved = true;
        }
        return moved;
    }

    // 휠 = 궤도 반경. 원근이므로 거리로 당긴다.
    private bool Zoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) <= 0.01f)
        {
            return false;
        }

        rig.distance -= Mathf.Sign(scroll) * rig.distance * zoomStep;
        return true;
    }
}
