using System;
using UnityEngine;
using UnityEngine.AI;

// 풀링되어 재사용되므로 Awake는 최초 생성 시 1번만 돈다 — 상태 리셋은 OnDisable에서.
[RequireComponent(typeof(NavMeshAgent))]
public class CitizenNPC : MonoBehaviour
{
    private const string MovingBool = "IsMoving";
    // 인사/모임 애니메이션에서 빠져나오는 트랜지션 조건으로 씀 — SetTrigger는 "들어가라"만 알릴 뿐
    // 언제 나가는지는 모르니, 이 bool이 false로 내려가는 시점을 트랜지션 조건(Has Exit Time 끄고)으로 걸면 된다.
    private const string SocializingBool = "IsSocializing";
    private const float HomeTimeout = 15f; // 경로 탐색 실패로 영원히 안 사라지는 상황 방지
    private const int MaxDestinationAttempts = 5; // 너무 가까운 지점만 계속 뽑히는 걸 줄이기 위한 재시도 횟수

    [SerializeField] private float minWanderInterval = 2f;
    [SerializeField] private float maxWanderInterval = 5f;
    [SerializeField] private float arriveDistance = 0.3f;
    [SerializeField] private float minWanderDistance = 1.5f; // 이보다 가까운 지점은 재시도(걷는 티가 안 나서 제외)
    [SerializeField] private float rotationSpeed = 6f; // 회전 보간 속도 — 낮을수록 느긋하게 돈다
    [SerializeField] private float statVariance = 0.2f; // 개체별 편차 폭(예: 0.2 = ±20%) — 다 같은 속도로 움직이면 기계적으로 보임
    [SerializeField] private float homeSpeedMultiplier = 1.6f; // 밤에 귀가할 때는 이 배수만큼 빠르게 걷는다

    private NavMeshAgent agent;
    private Animator animator;
    private float baseSpeed; // 개체별 편차가 적용된 "평소(배회) 속도" — 귀가 배속의 기준값

    private Vector3 wanderCenter;
    private float wanderRadius;
    private float nextWanderTime;

    private bool goingHome;
    private float homeDeadline;
    private Action onArrivedHome;

    private bool waitingAtDestination;
    private NavMeshPath scratchPath;

    public bool IsInteracting { get; private set; }
    public bool IsInGroup => isJoiningGroup || isInGroupChat;
    public bool IsAvailableForSocial => !goingHome && !IsInteracting && !IsInGroup;

    private Vector3 greetFacePoint;
    private string greetAnimTrigger;
    private float greetAnimTime; // 이 시점에 실제 인사 제스처를 재생 — 초대자/응답자가 서로 다르게 줘서 순서대로 나오게 함
    private float greetEndTime;
    private bool greetAnimPlayed;

    private bool isJoiningGroup; // 모임 장소로 걸어가는 중
    private float joinDeadline;
    private Vector3 groupFacePoint; // 도착 후 바라볼 모임 중심점
    private Action onJoinArrived;

    private bool isInGroupChat; // 도착해서 대화 중 — 시작/종료는 매니저가 그룹 전체를 맞춰서 호출
    private string groupChatAnimTrigger;
    private bool groupChatAnimPlayed;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        scratchPath = new NavMeshPath();

        // 배회하는 시민끼리는 서로 피하지 않고 겹쳐서 지나가게 함(정적 장애물 회피는 그대로 유지).
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        // NavMeshAgent 기본 회전은 즉각적으로 꺾여 뚝뚝 끊기므로, 직접 Slerp로 부드럽게 돈다.
        agent.updateRotation = false;

        // 개체마다 속도/회전/배회 간격에 편차를 줘서 전부 똑같이 움직이는 기계적인 느낌을 없앤다.
        // 풀링된 오브젝트라 Awake는 최초 생성 시 1번만 돌기 때문에, 한 번 정해진 개성이 계속 유지된다.
        agent.speed *= RandomMultiplier();
        rotationSpeed *= RandomMultiplier();
        minWanderInterval *= RandomMultiplier();
        maxWanderInterval *= RandomMultiplier();

        baseSpeed = agent.speed;
    }

    private float RandomMultiplier() => 1f + UnityEngine.Random.Range(-statVariance, statVariance);

    private void SetSocializing(bool value)
    {
        if (animator != null) animator.SetBool(SocializingBool, value);
    }

    private void OnDisable()
    {
        goingHome = false;
        onArrivedHome = null;
        IsInteracting = false;
        isJoiningGroup = false;
        isInGroupChat = false;
        onJoinArrived = null;
        SetSocializing(false);
        if (agent != null)
        {
            agent.speed = baseSpeed; // 귀가 배속이 걸린 채로 풀에 반납되지 않게
            if (agent.isOnNavMesh)
                agent.ResetPath();
        }
    }

    public void StartWandering(Vector3 center, float radius)
    {
        wanderCenter = center;
        wanderRadius = radius;
        agent.speed = baseSpeed; // 귀가 배속이 남아있었을 수 있으니 평소 속도로 복원
        goingHome = false;
        onArrivedHome = null;
        IsInteracting = false;
        isJoiningGroup = false;
        isInGroupChat = false;
        onJoinArrived = null;
        SetSocializing(false);
        waitingAtDestination = false;
        PickNewWanderDestination();
    }

    public void GoHome(Vector3 homePosition, Action onArrived)
    {
        goingHome = true;
        onArrivedHome = onArrived;
        homeDeadline = Time.time + HomeTimeout;
        // 인사/모임 중이었어도 밤이 되면 바로 귀가하도록 취소
        IsInteracting = false;
        isJoiningGroup = false;
        isInGroupChat = false;
        onJoinArrived = null;
        SetSocializing(false);
        agent.speed = baseSpeed * homeSpeedMultiplier; // 귀가할 땐 서둘러 걷게
        agent.SetDestination(homePosition);
    }

    // 모임 장소로 걸어간다. 도착하거나 timeout이 지나면(둘 중 먼저) onArrived를 호출 —
    // 매니저는 이 콜백으로 그룹 전원이 모였는지 판단하므로, 도달 실패로 영원히 안 부르는 일이 없어야 한다.
    public void JoinGroup(Vector3 spotPosition, Vector3 facePoint, float timeout, Action onArrived)
    {
        isJoiningGroup = true;
        groupFacePoint = facePoint;
        joinDeadline = Time.time + timeout;
        onJoinArrived = onArrived;
        agent.SetDestination(spotPosition);
    }

    // 그룹 전원이 모였을 때 매니저가 동시에 호출 — 다 같이 대화를 시작한다.
    public void StartGroupChat(string animTrigger)
    {
        isJoiningGroup = false;
        isInGroupChat = true;
        groupChatAnimTrigger = animTrigger;
        groupChatAnimPlayed = false;
        SetSocializing(true);
        agent.SetDestination(transform.position); // 제자리에 멈춤
    }

    // 매니저가 그룹 전체를 같은 시점에 끝내려고 호출.
    public void EndGroupChat()
    {
        isInGroupChat = false;
        SetSocializing(false);
        waitingAtDestination = false; // 배회 상태머신이 다시 새 목적지를 고르게 함
    }

    // animDelay만큼 늦게 실제 인사 제스처(Animator Trigger)를 재생한다 — 두 시민에게 서로 다른
    // animDelay를 줘서 동시에 인사하는 대신 한쪽이 먼저, 다른 쪽이 반응하듯 순서대로 나오게 한다.
    // 마주 보는 동작 자체는 지연 없이 바로 시작한다(멀리서부터 눈이 마주친 것처럼 자연스럽게).
    public void TriggerGreet(Vector3 facePoint, float animDelay, float duration, string animTrigger)
    {
        IsInteracting = true;
        SetSocializing(true);
        agent.SetDestination(transform.position); // 제자리에 멈춤

        greetFacePoint = facePoint;
        greetAnimTrigger = animTrigger;
        greetAnimTime = Time.time + animDelay;
        greetEndTime = Time.time + animDelay + duration;
        greetAnimPlayed = false;
    }

    private void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (IsInteracting)
        {
            UpdateGreet();
            return;
        }

        if (isInGroupChat)
        {
            UpdateGroupChat();
            return;
        }

        if (isJoiningGroup)
        {
            UpdateJoinGroup();
            return;
        }

        bool pathReady = !agent.pathPending;
        bool arrived = pathReady && agent.remainingDistance <= arriveDistance;

        // 도착한 뒤 잔여 거리를 좁히려는 미세 조향 때문에 방향이 흔들리며 제자리에서 도는 것처럼
        // 보이므로, agent.isStopped로 실제 이동을 건드리는 대신 "도착 상태에서는 회전/애니메이션을
        // 갱신하지 않는다"로 시각적으로만 처리한다(NavMeshAgent의 실제 경로 상태는 그대로 둔다).
        bool moving = pathReady && !arrived && agent.velocity.sqrMagnitude > 0.01f;
        if (animator != null) animator.SetBool(MovingBool, moving);
        if (moving) FaceMovementDirection();

        if (goingHome)
        {
            if (arrived || Time.time >= homeDeadline)
            {
                goingHome = false;
                var callback = onArrivedHome;
                onArrivedHome = null;
                callback?.Invoke();
            }
            return;
        }

        if (!pathReady) return;

        if (!arrived)
        {
            waitingAtDestination = false; // 아직 이동 중 — 도착하면 그때부터 고민 시간을 잰다
            return;
        }

        if (!waitingAtDestination)
        {
            waitingAtDestination = true;
            nextWanderTime = Time.time + UnityEngine.Random.Range(minWanderInterval, maxWanderInterval);
            return;
        }

        if (Time.time >= nextWanderTime)
            PickNewWanderDestination();
    }

    private void UpdateGreet()
    {
        if (animator != null) animator.SetBool(MovingBool, false);
        FaceTowardDirection(greetFacePoint - transform.position);

        if (!greetAnimPlayed && Time.time >= greetAnimTime)
        {
            greetAnimPlayed = true;
            if (animator != null && !string.IsNullOrEmpty(greetAnimTrigger))
                animator.SetTrigger(greetAnimTrigger);
        }

        if (Time.time >= greetEndTime)
        {
            IsInteracting = false;
            SetSocializing(false);
            waitingAtDestination = false; // 인사 끝났으니 배회 상태머신이 다시 새 목적지를 고르게 함
        }
    }

    private void UpdateJoinGroup()
    {
        bool pathReady = !agent.pathPending;
        bool arrived = pathReady && agent.remainingDistance <= arriveDistance;

        bool moving = pathReady && !arrived && agent.velocity.sqrMagnitude > 0.01f;
        if (animator != null) animator.SetBool(MovingBool, moving);
        if (moving) FaceMovementDirection();
        else if (arrived) FaceTowardDirection(groupFacePoint - transform.position);

        if (arrived || Time.time >= joinDeadline)
        {
            isJoiningGroup = false;
            var callback = onJoinArrived;
            onJoinArrived = null;
            callback?.Invoke();
        }
    }

    private void UpdateGroupChat()
    {
        if (animator != null) animator.SetBool(MovingBool, false);
        FaceTowardDirection(groupFacePoint - transform.position);

        if (!groupChatAnimPlayed)
        {
            groupChatAnimPlayed = true;
            if (animator != null && !string.IsNullOrEmpty(groupChatAnimTrigger))
                animator.SetTrigger(groupChatAnimTrigger);
        }
        // 종료는 매니저가 그룹 전체를 맞춰서 EndGroupChat()으로 직접 호출한다.
    }

    private void FaceMovementDirection() => FaceTowardDirection(agent.velocity);

    private void FaceTowardDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    private void PickNewWanderDestination()
    {
        waitingAtDestination = false;
        agent.SetDestination(FindWanderDestination());
    }

    // NavMesh.SamplePosition은 "근처에 NavMesh가 있는지"만 볼 뿐 "거기까지 실제로 갈 수 있는지"는
    // 보장하지 않는다 — 바위·나무 등 장애물로 메시가 조각나 있으면 도달 불가능한 지점을 목적지로
    // 잡아 경로가 끊긴 채 멈출 수 있다. 그래서 CalculatePath로 완전한 경로인지까지 검증한다.
    // 그래도 마땅한 곳이 없으면(전부 실패) wanderCenter로 폴백한다.
    private Vector3 FindWanderDestination()
    {
        float minSqr = minWanderDistance * minWanderDistance;
        Vector3 fallback = wanderCenter;

        for (int attempt = 0; attempt < MaxDestinationAttempts; attempt++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = wanderCenter + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(candidate, out var hit, wanderRadius, NavMesh.AllAreas))
                continue;

            // agent.CalculatePath는 에이전트 자신의 경로 계산 슬롯을 써서, 바로 뒤이은 SetDestination의
            // 경로 요청과 겹쳐 pathPending이 꼬일 수 있다(멀쩡히 SetDestination해도 안 움직이는 원인).
            // 그래서 에이전트 상태와 무관한 정적 NavMesh.CalculatePath로 검증만 하고 실제 이동은 SetDestination에 맡긴다.
            if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, scratchPath)
                || scratchPath.status != NavMeshPathStatus.PathComplete)
                continue; // 도달 불가능하거나 부분 경로만 나오는 지점 — 다시 뽑는다

            fallback = hit.position; // 거리 조건을 못 채워도 최소한 도달 가능한 점으로는 폴백
            if ((hit.position - transform.position).sqrMagnitude >= minSqr)
                return hit.position;
        }

        return fallback;
    }
}
