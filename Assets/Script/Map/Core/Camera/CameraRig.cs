using UnityEngine;

// 카메라의 궤도 값(초점·좌우각·상하각·거리)  
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    [Header("각도")]
    public Vector3 focus;
    public float yaw = 45f;
    public float pitch = 45f;
    public float distance = 40f;

    [Header("렌즈")]
    public bool perspective = true;
    [Range(10f, 70f)] public float fieldOfView = 30f;

    [Header("한계")]
    public float minPitch = 5f;
    public float maxPitch = 89f;
    public float minDistance = 5f;
    public float maxDistance = 120f;
    [Tooltip("가로 이동 한계: 맵 좌우 끝이 화면에서 닿는 위치(1=화면 끝, 낮출수록 밖이 더 보임).")]
    [SerializeField, Range(0.2f, 1f)] private float fillH = 1f;
    [Tooltip("세로 이동 한계: 낮출수록 위·아래로 더 이동(여백↑). 위쪽 타일 윗면 여유가 필요하면 낮춘다.")]
    [SerializeField, Range(0.4f, 1f)] private float fillV = 0.8f;

    [Header("부드러움")]
    [Tooltip("카메라가 목표를 따라가는 시간(초). 클수록 더 부드럽고 느긋하게 붙는다. 0=즉시(끔).")]
    [SerializeField, Range(0f, 0.4f)] private float smoothTime = 0.12f;

    private Camera cam;
    private readonly CameraClamp clamp = new();
    private Bounds area;
    private bool hasArea;
    private bool clampSuspended;

    // 화면에 실제로 그리는 표시 상태. focus/yaw/pitch/distance는 '목표'이고 이 값이 매 프레임 목표로
    private Vector3 showFocus;
    private float showYaw;
    private float showPitch;
    private float showDist;
    private Vector3 focusVel;
    private float yawVel;
    private float pitchVel;
    private float distVel;
    private bool showReady;

    private Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);
    private Quaternion ShowRotation => Quaternion.Euler(showPitch, showYaw, 0f);

    // 카메라가 켜질 때 한 번: 카메라 부품을 챙기고 화면에 반영한다.
    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        ApplyNow();
    }

    // 인스펙터에서 값을 고치면 Play 없이 바로 보여준다. 여기서 값을 되돌리지는 않는다 —
    // 타이핑 도중의 중간 값(150을 치려고 누른 1)에 반응하면 다른 값까지 덮어써 버린다.
    private void OnValidate()
    {
        cam = GetComponent<Camera>();
        ApplyNow();
    }

    // 이동 가능 영역. ExpandFocus가 모듈 해금 때마다 갱신해 넣는다. 넣기 전엔 클램프가 놀고 있다.
    public void SetArea(Bounds next)
    {
        area = next;
        hasArea = true;
        ApplyNow();
    }

    // 상자가 화면 채움 비율(fill)만큼 차지하는 거리. focus를 상자 중심에 둔 상태를 가정한다(확장 완료 프레이밍용).
    public float FitDistance(Vector3 focusPoint, Bounds bounds, float fill)
    {
        float raw = clamp.FitDistance(focusPoint, bounds, Rotation, fieldOfView, cam.aspect, fill, fill);
        return Mathf.Clamp(raw, minDistance, maxDistance);
    }

    // 확장 자동 이동처럼 목표 지점이 이미 정해진 동안 울타리가 끼어들지 않게 잠시 끈다.
    public void SuspendClamp()
    {
        clampSuspended = true;
    }

    // 자동 이동이 끝나거나 가로채졌을 때 울타리를 다시 켠다.
    public void ResumeClamp()
    {
        clampSuspended = false;
    }

    // 지금 값들을 화면(카메라 위치·각도·줌)에 한 번에 반영한다.
    public void ApplyNow()
    {
        ApplyLimit();
        Damp();
        ApplyLens();
        ApplyOrbit();
    }

    // 화면에 보이는 값을 목표값 쪽으로 조금씩 부드럽게 옮긴다(움직임 딱딱함 방지).
    private void Damp()
    {
        if (!Application.isPlaying || smoothTime <= 0f || !showReady)
        {
            showFocus = focus;
            showYaw = yaw;
            showPitch = pitch;
            showDist = distance;
            showReady = true;
            return;
        }

        float dt = Time.unscaledDeltaTime;
        showFocus = Vector3.SmoothDamp(showFocus, focus, ref focusVel, smoothTime, Mathf.Infinity, dt);
        showYaw = Mathf.SmoothDampAngle(showYaw, yaw, ref yawVel, smoothTime, Mathf.Infinity, dt);
        showPitch = Mathf.SmoothDamp(showPitch, pitch, ref pitchVel, smoothTime, Mathf.Infinity, dt);
        showDist = Mathf.SmoothDamp(showDist, distance, ref distVel, smoothTime, Mathf.Infinity, dt);
    }

    // 범위를 벗어난 값이 들어왔을 때 되돌린다.
    public void ClampState()
    {
        maxPitch = Mathf.Max(maxPitch, minPitch);
        maxDistance = Mathf.Max(maxDistance, minDistance);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    // 초점이 맵 밖으로 나가지 않게 경계 안으로 되돌린다.
    private void ApplyLimit()
    {
        if (!CanClamp())
        {
            return;
        }
        focus = clamp.FitFocus(focus, area, Rotation, distance, fieldOfView, cam.aspect, fillH, fillV);
    }

    private bool CanClamp()
    {
        return hasArea && !clampSuspended;
    }

    // 원근/직교 여부와 줌 크기를 카메라 렌즈에 반영한다.
    private void ApplyLens()
    {
        cam.orthographic = !perspective;
        cam.fieldOfView = fieldOfView;
        // 원근·직교를 오가도 화면상 크기가 유지되도록 distance에서 직교 크기를 유도한다.
        cam.orthographicSize = showDist * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = Mathf.Max(1000f, showDist * 4f);
    }

    // 표시값으로 카메라의 실제 위치와 바라보는 방향을 정한다.
    private void ApplyOrbit()
    {
        Quaternion rot = ShowRotation;
        transform.rotation = rot;
        transform.position = showFocus - rot * Vector3.forward * showDist;
    }
}
