using UnityEngine;
using UnityEngine.InputSystem;

// 개발용 자유 카메라. 맵을 아무 각도에서나 둘러보기 위한 도구다.
// F = 비행 모드 토글(마우스 = 시선, WASD = 이동, QE = 높이, 휠 = 속도), Home = 원래 시점으로 복귀.
// 비행 중엔 평소 조작과 이동 범위 제한을 잠시 끈다. 게임 플레이에는 쓰이지 않는다.
public class CameraFreeLook : MonoBehaviour
{
    [SerializeField] private CameraRig rig;

    private CameraInput input;   // 셋 다 같은 카메라 오브젝트에 붙는 고정 관계라 Start에서 잡는다
    private ExpandFocus expand;

    [Header("Keys")]
    [SerializeField] private Key freeLookKey = Key.F;
    [SerializeField] private Key resetKey = Key.Home;

    [Header("자유시점(비행)")]
    [Tooltip("마우스 감도(픽셀당 회전 각).")]
    [SerializeField, Range(0.02f, 1f)] private float lookSens = 0.15f;
    [Tooltip("비행 이동 속도. 자유시점 중 스크롤로 조절된다.")]
    [SerializeField] private float moveSpeed = 20f;
    [Tooltip("상하각(pitch) 제한(min,max). 비행 중에만 적용.")]
    [SerializeField] private Vector2 freePitch = new(-85f, 85f);
    [Tooltip("비행 중 화각. 탑뷰의 좁은 FOV 대신 넓게 본다(나갈 때 원래 렌즈로 복귀).")]
    [SerializeField, Range(30f, 90f)] private float flyFov = 60f;

    [Header("복귀")]
    [Tooltip("홈으로 부드럽게 돌아오는 시간(초).")]
    [SerializeField, Min(0.05f)] private float returnTime = 0.4f;

    private Vector3 homeFocus;
    private float homeYaw;
    private float homePitch;
    private float homeDistance;
    private Vector2 basePitch;   // 리그 원래 상하각 제한(복원용)

    private bool freeLook;
    private bool returning;

    private Camera cam;          // 비행 중 렌즈를 직접 바꾸기 위한 캐시(나갈 때 rig가 원복)
    private Vector3 flyPos;      // 비행 중 카메라 위치(트랜스폼 직접 구동)
    private float flyYaw;
    private float flyPitch;

    private Vector3 focusVel;
    private float yawVel;
    private float pitchVel;
    private float distVel;

    private void Start()
    {
        input = rig.GetComponent<CameraInput>();
        expand = rig.GetComponent<ExpandFocus>();
        cam = rig.GetComponent<Camera>();
        SaveHome();
    }

    private void Update()
    {
        ReadKeys();
        if (freeLook)
        {
            FlyStep();
        }
        else if (returning)
        {
            StepReturn();
        }
    }

    private void OnDisable()
    {
        if (freeLook)
        {
            ExitFree(); // 컴포넌트가 꺼져도 커서·입력·제한을 되돌린다
        }
    }

    // 시작 순간의 궤도와 상하각 제한을 홈으로 저장.
    private void SaveHome()
    {
        homeFocus = rig.focus;
        homeYaw = rig.yaw;
        homePitch = rig.pitch;
        homeDistance = rig.distance;
        basePitch = new Vector2(rig.minPitch, rig.maxPitch);
    }

    private void ReadKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }
        if (keyboard[freeLookKey].wasPressedThisFrame)
        {
            ToggleFree();
        }
        if (keyboard[resetKey].wasPressedThisFrame)
        {
            StartReturn();
        }
    }

    private void ToggleFree()
    {
        if (freeLook)
        {
            ExitFree();
        }
        else
        {
            EnterFree();
        }
    }

    // 자유시점 진입: CameraInput을 끄고 현재 시점을 비행 상태로 씨앗 삼는다.
    private void EnterFree()
    {
        freeLook = true;
        returning = false;
        input.enabled = false;
        expand.CancelMove();
        flyYaw = rig.yaw;
        flyPitch = rig.pitch;
        flyPos = rig.transform.position;
        if (cam != null)
        {
            cam.orthographic = false; // 비행은 원근이라야 자연스럽다
            cam.fieldOfView = flyFov;
        }
        HideCursor();
    }

    // 자유시점 종료(F 토글): 현재 비행 위치에서 rig 값을 역산해 제약 모드로 즉시 복귀.
    private void ExitFree()
    {
        freeLook = false;
        ShowCursor();
        input.enabled = true;
        SyncRig();
        RestoreLimit();
        rig.pitch = Mathf.Clamp(rig.pitch, basePitch.x, basePitch.y);
        rig.ApplyNow(); // 클램프가 focus를 맵 위로 재프레이밍
    }

    // 비행 한 프레임: 마우스로 시선, WASD/QE로 이동, 스크롤로 속도. 트랜스폼을 직접 세팅(클램프 우회).
    private void FlyStep()
    {
        expand.CancelMove(); // 비행 중 확장 자동 이동 억제
        Look();
        Move();
        rig.transform.SetPositionAndRotation(flyPos, Quaternion.Euler(flyPitch, flyYaw, 0f));
    }

    private void Look()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }
        Vector2 delta = mouse.delta.ReadValue();
        flyYaw += delta.x * lookSens;
        flyPitch -= delta.y * lookSens;
        flyPitch = Mathf.Clamp(flyPitch, freePitch.x, freePitch.y);

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            moveSpeed = Mathf.Clamp(moveSpeed + Mathf.Sign(scroll) * moveSpeed * 0.1f, 2f, 300f);
        }
    }

    private void Move()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }
        Quaternion rot = Quaternion.Euler(flyPitch, flyYaw, 0f);
        Vector3 dir = Vector3.zero;
        if (keyboard.wKey.isPressed) { dir += rot * Vector3.forward; }
        if (keyboard.sKey.isPressed) { dir -= rot * Vector3.forward; }
        if (keyboard.dKey.isPressed) { dir += rot * Vector3.right; }
        if (keyboard.aKey.isPressed) { dir -= rot * Vector3.right; }
        if (keyboard.eKey.isPressed) { dir += Vector3.up; }
        if (keyboard.qKey.isPressed) { dir -= Vector3.up; }

        if (dir != Vector3.zero)
        {
            flyPos += dir.normalized * (moveSpeed * Time.unscaledDeltaTime);
        }
    }

    // 홈 복귀 시작. 자유시점이면 부드럽게 이어지도록 제한만 넓게 유지한 채 rig로 값을 넘긴다.
    private void StartReturn()
    {
        if (freeLook)
        {
            freeLook = false;
            ShowCursor();
            input.enabled = true;
            SyncRig();
            rig.minPitch = freePitch.x; // 넓은 pitch에서 부드럽게 내려오도록 복귀 끝까지 유지
            rig.maxPitch = freePitch.y;
        }
        expand.CancelMove();
        focusVel = Vector3.zero;
        yawVel = 0f;
        pitchVel = 0f;
        distVel = 0f;
        returning = true;
    }

    private void StepReturn()
    {
        if (HasInput())
        {
            returning = false; // 사용자가 끼어들면 복귀 포기
            RestoreLimit();
            return;
        }
        rig.focus = Vector3.SmoothDamp(rig.focus, homeFocus, ref focusVel, returnTime);
        rig.yaw = Mathf.SmoothDampAngle(rig.yaw, homeYaw, ref yawVel, returnTime);
        rig.pitch = Mathf.SmoothDamp(rig.pitch, homePitch, ref pitchVel, returnTime);
        rig.distance = Mathf.SmoothDamp(rig.distance, homeDistance, ref distVel, returnTime);
        rig.ApplyNow();
        if (ReachedHome())
        {
            returning = false;
            RestoreLimit();
        }
    }

    // 목표 근접 판정. SmoothDamp은 점근만 하므로 남은 거리로 끝을 확정한다(프레임레이트 무관).
    private bool ReachedHome()
    {
        bool nearFocus = (rig.focus - homeFocus).sqrMagnitude < 1e-4f;
        bool nearYaw = Mathf.Abs(Mathf.DeltaAngle(rig.yaw, homeYaw)) < 0.05f;
        bool nearPitch = Mathf.Abs(rig.pitch - homePitch) < 0.05f;
        bool nearDistance = Mathf.Abs(rig.distance - homeDistance) < 0.05f;
        return nearFocus && nearYaw && nearPitch && nearDistance;
    }

    // 비행 위치에서 rig 궤도 값을 역산. 카메라위치 = focus - 시선*거리 → focus = 카메라위치 + 시선*거리.
    private void SyncRig()
    {
        rig.yaw = flyYaw;
        rig.pitch = flyPitch;
        Quaternion rot = Quaternion.Euler(flyPitch, flyYaw, 0f);
        rig.focus = flyPos + rot * Vector3.forward * rig.distance;
    }

    private void RestoreLimit()
    {
        rig.minPitch = basePitch.x;
        rig.maxPitch = basePitch.y;
    }

    private void HideCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 카메라 조작 입력이 있으면 사용자가 개입 중으로 본다(복귀 취소용).
    private bool HasInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                return true;
            }
            if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
            {
                return true;
            }
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed
                || keyboard.dKey.isPressed || keyboard.qKey.isPressed || keyboard.eKey.isPressed)
            {
                return true;
            }
        }
        return false;
    }
}
