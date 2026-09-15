using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using Random = UnityEngine.Random;

// 표시 시민 수 = round(clamp01((CurrentCitizen - HeroUsedCitizen) / estimatedMaxCitizenCeiling) * visibleCap)
// 밤에는 전원 hubPoint로 귀가 후 디스폰, 낮이 되면 다시 목표치만큼 스폰한다.
public class CitizenWanderManager : MonoBehaviour
{
    [SerializeField] private GameObject[] citizenPrefabs; // 여러 모델을 섞어 쓰면 다 똑같이 생긴 느낌이 줄어든다
    [SerializeField] private float wanderRadius = 6f;
    [SerializeField] private int visibleCap = 60;
    [SerializeField] private int estimatedMaxCitizenCeiling = 300; // 디자이너가 예상하는 최종 MaxCitizen 상한, 밸런스 튜닝용

    [Header("인사")]
    [SerializeField] private float greetCheckInterval = 1.5f; // 이 주기로 가까운 시민 쌍을 검사
    [SerializeField] private float greetRadius = 2.5f;
    [SerializeField] private float greetChance = 0.15f; // 사거리 안에 있어도 이 확률로만 실제로 인사
    [SerializeField] private float greetResponseDelay = 0.6f; // 상대방 인사 제스처가 이만큼 늦게 나옴
    [SerializeField] private float greetDuration = 2.5f; // 제스처 재생 후 서 있는 시간(각자 animDelay 이후부터)
    [SerializeField] private string greetAnimTrigger = "Greet";

    [Header("단체 대화")]
    [SerializeField] private float groupCheckInterval = 6f;
    [SerializeField] private float groupChance = 0.2f;
    [SerializeField] private int groupMinSize = 2;
    [SerializeField] private int groupMaxSize = 3;
    [SerializeField] private float groupGatherRadius = 6f; // 중심 시민 기준으로 이 안에 있는 후보만 그룹으로 묶음
    [SerializeField] private float groupSpotSpacing = 0.8f; // 모이는 지점 안에서 각자 벌어지는 정도
    [SerializeField] private float groupJoinTimeout = 8f;
    [SerializeField] private float groupChatDuration = 4f;
    [SerializeField] private string groupChatAnimTrigger = "Chat";

    private class GroupSession
    {
        public List<GameObject> Members;
        public int ArrivedCount;
        public bool ChatStarted;
    }

    // 프리팹 에셋이라 씬의 Transform을 직접 참조할 수 없다 — GameLifeTimeScope가 빌드 후 주입한다.
    private Transform hubPoint;
    private Transform[] homePoints; // 밤에 귀가할 목적지 후보들 — 시민마다 랜덤으로 하나씩 선택
    private Vector3 hubGroundPosition; // hubPoint를 NavMesh 위로 스냅한 좌표 — 스폰/목적지에 실제로 쓰는 값

    private CitizenManager citizenManager;
    private GameManager gameManager;
    private PoolManager poolManager;

    private readonly List<GameObject> activeCitizens = new();
    private bool isNight;
    private CancellationTokenSource lifetimeCts;

    [Inject]
    private void Construct(CitizenManager citizenManager, GameManager gameManager, PoolManager poolManager)
    {
        this.citizenManager = citizenManager;
        this.gameManager = gameManager;
        this.poolManager = poolManager;
    }

    private void Awake()
    {
        lifetimeCts = new CancellationTokenSource();
        citizenManager.CitizenChanged += OnCitizenChanged;
        gameManager.ChangeToNight += OnNight;
        gameManager.ChangeToDay += OnDay;
    }

    private void OnCitizenChanged() => SyncVisibleCountAsync(lifetimeCts.Token).Forget();

    private void Start()
    {
        if (hubPoint == null)
        {
            Debug.LogError("CitizenWanderManager: hubPoint가 설정되지 않았습니다. GameLifeTimeScope의 citizenHubPoint를 확인하세요.", this);
            return;
        }

        if (citizenPrefabs == null || citizenPrefabs.Length == 0)
        {
            Debug.LogError("CitizenWanderManager: citizenPrefabs가 비어 있습니다. 최소 1개 이상 등록하세요.", this);
            return;
        }

        hubGroundPosition = ResolveHubGroundPosition();
        InitializeAsync(lifetimeCts.Token).Forget();
        RunGreetCheckLoop(lifetimeCts.Token).Forget();
        RunGroupChatCheckLoop(lifetimeCts.Token).Forget();
    }

    // 이전엔 Update()에서 Time.time과 비교해가며 매 프레임 폴링했다 - 이미 파일 전체가 UniTask 루프
    // 스타일이라 그와 동일하게 대기-후-실행 루프로 바꿔 Update() 자체를 없앤다.
    private async UniTask RunGreetCheckLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(greetCheckInterval), cancellationToken: token);
            if (!isNight) TryTriggerGreetings();
        }
    }

    private async UniTask RunGroupChatCheckLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(groupCheckInterval), cancellationToken: token);
            if (!isNight) TryStartGroupChat();
        }
    }

    // 워밍업 스폰과 초기 표시 스폰을 한 프레임에 몰아서 하면 씬 로딩 직후 그 프레임에 스파이크가 생긴다
    // (MainScene 활성화 렉의 주범이었음) — 여러 프레임에 나눠 처리해 스파이크를 없앤다.
    private async UniTaskVoid InitializeAsync(CancellationToken token)
    {
        await WarmPoolAsync(token);
        await SyncVisibleCountAsync(token);
    }


    private Vector3 ResolveHubGroundPosition()
    {
        if (NavMesh.SamplePosition(hubPoint.position, out var hit, wanderRadius, NavMesh.AllAreas))
            return hit.position;

        Debug.LogError("CitizenWanderManager: hubPoint 주변에서 NavMesh를 찾지 못했습니다. " +
            "NavMeshSurface 베이크 범위가 Hub 위치를 덮고 있는지 확인하세요.", this);
        return hubPoint.position;
    }

    // GameLifeTimeScope가 씬의 Hub Transform을 빌드 콜백에서 주입한다.
    public void SetHubPoint(Transform point)
    {
        hubPoint = point;
    }

    // GameLifeTimeScope가 씬의 귀가 목적지 후보들을 빌드 콜백에서 주입한다.
    public void SetHomePoints(Transform[] points)
    {
        homePoints = points;
    }

    private void OnDestroy()
    {
        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        citizenManager.CitizenChanged -= OnCitizenChanged;
        gameManager.ChangeToNight -= OnNight;
        gameManager.ChangeToDay -= OnDay;
    }

    // 가까운 시민 쌍마다 확률을 굴려 인사를 트리거한다. 같은 체크에서 한 시민이 여러 상대와
    // 동시에 인사하지 않도록, 트리거된 시민은 곧바로 IsAvailableForSocial이 false가 되어 제외된다.
    private void TryTriggerGreetings()
    {
        float radiusSqr = greetRadius * greetRadius;

        for (int i = 0; i < activeCitizens.Count; i++)
        {
            var a = activeCitizens[i];
            var npcA = a.GetComponent<CitizenNPC>();
            if (!npcA.IsAvailableForSocial) continue;

            for (int j = i + 1; j < activeCitizens.Count; j++)
            {
                var b = activeCitizens[j];
                var npcB = b.GetComponent<CitizenNPC>();
                if (!npcB.IsAvailableForSocial) continue;

                if ((a.transform.position - b.transform.position).sqrMagnitude > radiusSqr) continue;
                if (Random.value > greetChance) continue;

                npcA.TriggerGreet(b.transform.position, 0f, greetDuration, greetAnimTrigger);
                npcB.TriggerGreet(a.transform.position, greetResponseDelay, greetDuration, greetAnimTrigger);
                break; // a는 이번 체크에서 이미 인사를 시작했으니 다음 시민(i+1)으로
            }
        }
    }

    // 확률에 걸리면, 배회 중인 시민 중 하나를 중심으로 그 주변(groupGatherRadius) 후보들을 모아
    // 작은 그룹을 만든다. 근처에 사람이 충분하지 않으면 그냥 넘어간다(억지로 멀리서 끌어오지 않음).
    private void TryStartGroupChat()
    {
        if (Random.value > groupChance) return;

        var available = new List<GameObject>();
        foreach (var go in activeCitizens)
        {
            if (go.GetComponent<CitizenNPC>().IsAvailableForSocial)
                available.Add(go);
        }

        int groupSize = Random.Range(groupMinSize, groupMaxSize + 1);
        if (available.Count < groupSize) return;

        var center = available[Random.Range(0, available.Count)];
        Vector3 centerPos = center.transform.position;

        var candidates = new List<GameObject>(available);
        candidates.Remove(center);
        float radiusSqr = groupGatherRadius * groupGatherRadius;
        candidates.RemoveAll(go => (go.transform.position - centerPos).sqrMagnitude > radiusSqr);

        var members = new List<GameObject> { center };
        while (members.Count < groupSize && candidates.Count > 0)
        {
            int idx = Random.Range(0, candidates.Count);
            members.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }

        if (members.Count < groupMinSize) return; // 근처에 사람이 부족하면 포기

        StartGroupSession(members, centerPos);
    }

    // 모일 지점을 NavMesh 위로 스냅하고, 그 주위에 인원수만큼 지점을 나눠 각자 걸어가게 한다.
    // 전원이 도착(또는 timeout)하면 다 같이 대화를 시작하고, groupChatDuration 뒤 다 같이 끝낸다.
    private void StartGroupSession(List<GameObject> members, Vector3 centerPos)
    {
        if (!NavMesh.SamplePosition(centerPos, out var hit, wanderRadius, NavMesh.AllAreas))
            return;

        Vector3 gatherPoint = hit.position;
        var session = new GroupSession { Members = members };

        for (int i = 0; i < members.Count; i++)
        {
            float angle = i * Mathf.PI * 2f / members.Count;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * groupSpotSpacing;
            Vector3 spot = NavMesh.SamplePosition(gatherPoint + offset, out var spotHit, groupSpotSpacing + 0.5f, NavMesh.AllAreas)
                ? spotHit.position
                : gatherPoint;

            members[i].GetComponent<CitizenNPC>()
                .JoinGroup(spot, gatherPoint, groupJoinTimeout, () => OnGroupMemberArrived(session));
        }
    }

    private void OnGroupMemberArrived(GroupSession session)
    {
        session.ArrivedCount++;
        if (session.ChatStarted || session.ArrivedCount < session.Members.Count) return;

        session.ChatStarted = true;
        foreach (var member in session.Members)
        {
            if (member != null && member.activeInHierarchy)
                member.GetComponent<CitizenNPC>().StartGroupChat(groupChatAnimTrigger);
        }

        StartCoroutine(EndGroupSessionAfter(session, groupChatDuration));
    }

    private IEnumerator EndGroupSessionAfter(GroupSession session, float delay)
    {
        yield return new WaitForSeconds(delay);

        foreach (var member in session.Members)
        {
            // 풀링된 오브젝트라 그 사이 디스폰→다른 시민으로 재사용됐을 수 있다 —
            // 비활성 상태면 건너뛰고, 활성 상태면 그 시점에 실제로 이 컴포넌트를 쓰는 시민이 맞다.
            if (member != null && member.activeInHierarchy)
                member.GetComponent<CitizenNPC>().EndGroupChat();
        }
    }


    private const int WarmPoolBatchSize = 5;

    private async UniTask WarmPoolAsync(CancellationToken token)
    {
        int perPrefab = Mathf.CeilToInt((float)visibleCap / citizenPrefabs.Length);
        int sinceYield = 0;

        foreach (var prefab in citizenPrefabs)
        {
            var warm = new GameObject[perPrefab];
            for (int i = 0; i < perPrefab; i++)
            {
                warm[i] = poolManager.Spawn(prefab, hubGroundPosition, Quaternion.identity);
                if (++sinceYield >= WarmPoolBatchSize)
                {
                    sinceYield = 0;
                    await UniTask.Yield(token);
                }
            }

            for (int i = 0; i < perPrefab; i++)
            {
                poolManager.Despawn(warm[i]);
                if (++sinceYield >= WarmPoolBatchSize)
                {
                    sinceYield = 0;
                    await UniTask.Yield(token);
                }
            }
        }
    }

    private int CalculateTargetCount()
    {
        int available = Mathf.Max(0, citizenManager.CurrentCitizen - citizenManager.HeroUsedCitizen);
        float ratio = estimatedMaxCitizenCeiling > 0 ? (float)available / estimatedMaxCitizenCeiling : 0f;
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ratio) * visibleCap), 0, visibleCap);
    }

    private async UniTask SyncVisibleCountAsync(CancellationToken token)
    {
        if (isNight) return; // 밤에는 전부 집에 들어가 있어야 하므로 낮에만 동기화

        int target = CalculateTargetCount();
        int sinceYield = 0;

        while (activeCitizens.Count < target)
        {
            SpawnOne();
            if (++sinceYield >= WarmPoolBatchSize)
            {
                sinceYield = 0;
                await UniTask.Yield(token);
            }
        }

        while (activeCitizens.Count > target)
        {
            DespawnOne(activeCitizens[^1]);
            if (++sinceYield >= WarmPoolBatchSize)
            {
                sinceYield = 0;
                await UniTask.Yield(token);
            }
        }
    }

    private void SpawnOne()
    {
        Vector3 pos = RandomPointAroundHub();
        GameObject prefab = citizenPrefabs[Random.Range(0, citizenPrefabs.Length)];
        var go = poolManager.Spawn(prefab, pos, Quaternion.identity);
        go.GetComponent<CitizenNPC>().StartWandering(hubGroundPosition, wanderRadius);
        activeCitizens.Add(go);
    }

    private void DespawnOne(GameObject go)
    {
        activeCitizens.Remove(go);
        poolManager.Despawn(go);
    }

    private Vector3 RandomPointAroundHub()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = hubGroundPosition + new Vector3(offset.x, 0f, offset.y);
        return NavMesh.SamplePosition(candidate, out var hit, wanderRadius, NavMesh.AllAreas)
            ? hit.position
            : hubGroundPosition;
    }

    private void OnNight()
    {
        isNight = true;
        foreach (var go in activeCitizens)
        {
            Vector3 destination = PickHomeDestination();
            go.GetComponent<CitizenNPC>().GoHome(destination, () => poolManager.Despawn(go));
        }

        activeCitizens.Clear();
    }

    private Vector3 PickHomeDestination()
    {
        if (homePoints != null && homePoints.Length > 0)
            return homePoints[Random.Range(0, homePoints.Length)].position;
        return hubGroundPosition; // 후보가 없으면 기존처럼 Hub로 귀가
    }

    private void OnDay()
    {
        isNight = false;
        SyncVisibleCountAsync(lifetimeCts.Token).Forget();
    }
}
