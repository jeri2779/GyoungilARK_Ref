using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 맵 모듈이 해금되면 입력을 막고 새 지역으로 카메라를 강제 이동시킨다.
// 카메라 이동과 안개 제거가 모두 끝나면 입력을 다시 활성화한다.
[RequireComponent(typeof(CameraRig), typeof(CameraInput))]
public class ExpandFocus : MonoBehaviour
{
    [SerializeField] private MapRegistry registry;
    [Tooltip("새로 열린 지역으로 부드럽게 이동하는 시간(초).")]
    [FormerlySerializedAs("panTime")]
    [SerializeField, Min(0.05f)] private float moveTime = 0.4f;
    [Tooltip("확장 완료 시 모듈이 화면을 채우는 비율(1=모서리가 화면 끝, 낮추면 여백 추가).")]
    [SerializeField, Range(0.5f, 1f)] private float fitFill = 0.95f;
    [Header("Static Move Areas")]
    [SerializeField] private List<StaticMoveArea> staticMoveAreas = new();

    private CameraRig rig;
    private CameraInput input;
    private readonly CameraLimit limit = new();

    private bool moving;
    private Vector3 moveTarget;
    private Vector3 moveVel;
    private float moveDistance;
    private float moveDistVel;
    private int moveId = -1;
    private bool moveDone;
    private bool fogDone;

    private void Awake()
    {
        rig = GetComponent<CameraRig>();
        input = GetComponent<CameraInput>();
    }

    private void Start()
    {
        rig.SuspendClamp(); // 사용자가 직접 조작하기 전까지는 울타리를 걸지 않는다.
        RebuildLimit();
        BindModules();
        input.UserMoved += OnUserMoved;
        FogController.RevealDone += OnFogDone;
    }

    private void OnDestroy()
    {
        UnbindModules();
        input.UserMoved -= OnUserMoved;
        FogController.RevealDone -= OnFogDone;
    }

    private void Update()
    {
        if (!CanMove()) // 이동 중이고 시간이 흐를 때만 실행한다.
        {
            return;
        }

        MoveCamera();
    }

    private bool CanMove()
    {
        if (!moving) // 자동 이동 중이 아니면 실행하지 않는다.
        {
            return false;
        }

        return Time.deltaTime > 0f; // UI 일시정지가 끝날 때까지 이동을 대기한다.
    }

    private void MoveCamera()
    {
        rig.focus = Vector3.SmoothDamp(rig.focus, moveTarget, ref moveVel, moveTime); // 새 지역 중앙으로 부드럽게 이동한다.
        rig.distance = Mathf.SmoothDamp(rig.distance, moveDistance, ref moveDistVel, moveTime); // 화면을 채우는 거리로 함께 줌한다.
        rig.ApplyNow();   // 울타리는 꺼진 상태라 렌즈·위치만 갱신된다
        if (StillMoving())
        {
            return;
        }

        FinishMove(); // 초점·거리 둘 다 목표 지점에 도달했으면 이동 완료로 처리한다.
    }

    // 초점 또는 거리 중 하나라도 목표 지점(moveTarget, moveDistance)에 도달하지 않았으면 아직 이동 중이다.
    private bool StillMoving()
    {
        bool focusRemain = (rig.focus - moveTarget).sqrMagnitude >= 1e-3f;
        bool distanceRemain = Mathf.Abs(rig.distance - moveDistance) >= 1e-2f;
        return focusRemain || distanceRemain;
    }

    // 개발용 자유 카메라가 자동 이동을 중단할 때 사용한다.
    public void CancelMove()
    {
        moving = false;
        moveVel = Vector3.zero;
        moveDistVel = 0f;
        rig.ResumeClamp(); // 가로챌 때도 잠시 꺼둔 울타리를 되돌린다.
    }

    private void FinishMove()
    {
        moving = false; // 카메라 이동을 정지한다.
        moveVel = Vector3.zero; // 이동 속도를 초기화한다.
        moveDone = true; // 카메라 이동 완료를 기록한다.
        TryUnlock();
    }

    private void OnFogDone(int moduleId)
    {
        if (moduleId != moveId) // 현재 확장 지역의 완료 신호만 처리한다.
        {
            return;
        }

        fogDone = true; // 안개 제거 완료를 기록한다.
        TryUnlock();
    }

    // 사용자가 카메라를 직접 조작하면 그때부터 울타리를 다시 건다.
    private void OnUserMoved()
    {
        rig.ResumeClamp();
    }

    private void TryUnlock()
    {
        if (!CanUnlock()) // 두 작업 중 하나라도 남아 있으면 입력을 유지한다.
        {
            return;
        }

        input.enabled = true; // 카메라 입력을 다시 활성화한다.
        moveId = -1; // 현재 확장 지역 기록을 초기화한다.
    }

    private bool CanUnlock()
    {
        return moveDone && fogDone;
    }

    // 해금 모듈이 하나도 없으면 경계가 없다 → 클램프를 걸지 않는다(0 크기 박스면 카메라가 원점에 박힌다).
    private void RebuildLimit()
    {
        if (limit.Build(registry, staticMoveAreas))
        {
            rig.SetArea(limit.Area);
        }
    }
    // 모듈의 상태 변경과 신규 해금을 각각 다른 창구로 구독한다.
    private void BindModules()
    {
        foreach (ModuleLogic module in registry.AllModules.Values)
        {
            module.OnStateChanged += OnModuleState;
            module.OnUnlocked += ModuleUnlocked;
        }
    }
    // 해금 모듈이 하나도 없으면 경계가 없다 → 클램프를 걸지 않는다(0 크기 박스면 카메라가 원점에 박힌다).
    private void UnbindModules()
    {
        foreach (ModuleLogic module in registry.AllModules.Values)
        {
            module.OnStateChanged -= OnModuleState;
            module.OnUnlocked -= ModuleUnlocked;
        }
    }
    // 모듈 상태가 바뀌면(세이브 복원 포함) 카메라를 옮기지 않고 이동 경계만 다시 잡는다.
    private void OnModuleState(ModuleState state)
    {
        rig.SuspendClamp();   // 경계를 바꾸는 순간 지금 화면이 즉시 밀리지 않게 먼저 끈다
        RebuildLimit();   // 경계 먼저 확장(새 모듈 포함)
    }

    // 게임 중 새로 해금된 모듈로만 카메라를 부드럽게 이동시킨다.
    private void ModuleUnlocked(ModuleLogic module)
    {
        StartMove(module);
    }

    // focus·거리 목표를 해당 모듈 중앙·화면 꽉 채움으로. 울타리는 OnModuleState에서 이미 꺼둔 상태다.
    private void StartMove(ModuleLogic module)
    {
        Bounds bounds = module.GetComponent<MapBoard>().WorldBounds; // 새로 열린 지역의 경계를 구한다.
        moveTarget = bounds.center; // 이동 목표는 그 경계의 중심이다.
        moveTarget.y = rig.focus.y; // 줌 기준 높이는 유지하고 수평 위치만 새 지역으로 옮긴다.
        moveDistance = rig.FitDistance(moveTarget, bounds, fitFill); // 그 경계가 화면을 채우는 거리를 구한다.
        moveVel = Vector3.zero; // 이전 이동 속도를 초기화한다.
        moveDistVel = 0f; // 이전 거리 속도를 초기화한다.
        moveId = module.ModuleId; // 완료 신호를 비교할 확장 지역을 기록한다.
        moveDone = false; // 카메라 이동 완료 상태를 초기화한다.
        fogDone = false; // 안개 제거 완료 상태를 초기화한다.
        input.enabled = false; // 확장 연출 중 카메라 입력을 차단한다.
        moving = true; // 다음 Update부터 자동 이동을 시작한다.
    }
}
