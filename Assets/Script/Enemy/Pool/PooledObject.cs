using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    private GameObject sourcePrefab;
    private ParticleSystem[] particles;
    private CancellationTokenSource dcts;
    private bool released;
    private PoolManager pool; // 자기를 만든 풀(createFunc에서 주입) — 지연 회수에 사용
    public GameObject SourcePrefab => sourcePrefab;
    public bool IsReleased => released;

    // createFunc에서 1회 호출. 캐싱은 여기서 한 번만.
    public void Init(GameObject prefab, PoolManager owner)
    {
        sourcePrefab = prefab;
        pool = owner;
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }
    void OnEnable()
    {
        dcts = new CancellationTokenSource();
    }

    // Spawn 직후(활성화된 뒤) 호출: 상태 초기화 + 파티클 재생.
    public void OnSpawned()
    {
        released = false;
        dcts.Cancel();
        dcts.Dispose();
        dcts = new CancellationTokenSource();
        if (particles != null)
        {
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
            }
        }
    }
    public void MarkReleased()
    {
        released = true;
        dcts.Cancel();
        dcts.Dispose();
        dcts = new CancellationTokenSource();
    }

    // delay초 뒤 자기 자신을 풀로 회수 (기존 Destroy(go, t) 대체).
    public void ScheduleDespawn(float delay,bool scaleCheck=true)
    {
        dcts.Cancel();
        dcts.Dispose();
        dcts = new CancellationTokenSource();
        DespawnAfter(delay,scaleCheck).Forget();
    }

    private async UniTask DespawnAfter(float delay,bool scaleCheck = true)
    {
        var token = dcts.Token;
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay),ignoreTimeScale:!scaleCheck,cancellationToken : token);   
        }
        catch(OperationCanceledException)
        {
            return;
        }
        pool.Despawn(gameObject);
    }
}
