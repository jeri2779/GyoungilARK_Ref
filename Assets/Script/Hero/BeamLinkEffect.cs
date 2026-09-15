using System;
using UnityEngine;

// Hovl_Laser.cs 참고 — 다만 Physics.Raycast로 스스로 끝점을 찾는 대신, 호출자(ContinuousBeamStrategy /
// Hero.SpawnChainArc)가 이미 알고 있는 두 지점의 제공자(Func<Vector3>)를 등록해준다. Hovl_Laser가 자기
// Update()에서 매 프레임 레이캐스트로 끝점을 다시 구하는 것처럼, 여기서도 Track으로 등록만 해두면
// 매 프레임 스스로 다시 계산해 갱신한다 — 캐스터→타겟 빔과 체인 세그먼트(hits[i]→hits[i+1]) 양쪽에
// 동일하게 재사용된다.
// 바디 파티클 재생/정지는 여기서 다루지 않는다 — Hero.SpawnPersistentEffect가 스폰 시 모든 자식
// ParticleSystem을 Clear+Play하고, 풀 반납 시 SetActive(false)로 자동 정지되므로 별도 처리가 불필요.
[RequireComponent(typeof(LineRenderer))]
public class BeamLinkEffect : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [Tooltip("발사 지점(from)에 배치/정렬할 자식 — 머즐 플래시")]
    [SerializeField] private Transform flashEffect;
    [Tooltip("도착 지점(to)에 배치/정렬할 자식 — Hovl_Laser의 HitEffect에 해당")]
    [SerializeField] private Transform hitEffect;
    [SerializeField] private float mainTextureLength = 1f;
    [SerializeField] private float noiseTextureLength = 1f;
    [Tooltip("빔 두께 배율. 1이면 프리팹에 세팅된 원래 두께 그대로")]
    [SerializeField] private float beamSize = 1f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int NoiseId = Shader.PropertyToID("_Noise");

    // LineRenderer는 월드 좌표로 SetPosition하므로 transform.localScale은 두께에 영향이 없다 —
    // Hovl_Laser처럼 localScale까지 스케일할 필요 없이 widthMultiplier만 조절하면 충분하다.
    private float baseWidthMultiplier;

    // 매 프레임 다시 불러 두 끝점을 계산하는 제공자. Track으로 등록되면 Update가 계속 SetEndpoints를
    // 다시 호출해 캐스터/타겟이 움직여도 따라간다 — 등록 전이거나 StopTracking 후에는 null이라 멈춘다.
    private Func<Vector3> fromProvider;
    private Func<Vector3> toProvider;

    public float BeamSize
    {
        get => beamSize;
        set { beamSize = value; ApplyBeamSize(); }
    }

    private void Awake()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        baseWidthMultiplier = lineRenderer.widthMultiplier;
        ApplyBeamSize();
    }

    private void ApplyBeamSize()
    {
        lineRenderer.widthMultiplier = baseWidthMultiplier * Mathf.Max(0.0001f, beamSize);
    }

    // 매 프레임 스스로 갱신하도록 등록한다 — 이후 별도로 SetEndpoints를 다시 호출할 필요가 없다.
    public void Track(Func<Vector3> from, Func<Vector3> to)
    {
        fromProvider = from;
        toProvider = to;
        SetEndpoints(from(), to());   // 다음 프레임까지 기다리지 않고 등록 즉시 한 번 반영
    }

    public void StopTracking()
    {
        fromProvider = null;
        toProvider = null;
    }

    private void Update()
    {
        if (fromProvider == null || toProvider == null) return;
        SetEndpoints(fromProvider(), toProvider());
    }

    public void SetEndpoints(Vector3 from, Vector3 to)
    {
        lineRenderer.SetPosition(0, from);
        lineRenderer.SetPosition(1, to);

        float distance = Vector3.Distance(from, to);
        Material mat = lineRenderer.material;
        if (mat != null)
        {
            mat.SetTextureScale(MainTexId, new Vector2(mainTextureLength * distance, 1f));
            mat.SetTextureScale(NoiseId, new Vector2(noiseTextureLength * distance, 1f));
        }

        // flashEffect는 발사 지점(from)에서 빔 진행 방향(from→to)을 보고,
        // hitEffect는 도착 지점(to)에서 그 반대 방향(to→from, 날아온 쪽)을 본다.
        PlaceFacing(flashEffect, from, to - from);
        PlaceFacing(hitEffect, to, from - to);
    }

    private static void PlaceFacing(Transform t, Vector3 position, Vector3 forward)
    {
        if (t == null) return;
        t.position = position;
        if (forward.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(forward);
    }
}
