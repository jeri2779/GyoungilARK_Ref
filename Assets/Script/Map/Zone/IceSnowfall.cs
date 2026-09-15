using System.Collections.Generic;
using UnityEngine;

// 얼음 보드 전체를 덮는 상시 눈발 효과. 시작 시 한 번만 만들고, 모닥불 구역에서는 눈송이를 지운다.
public class IceSnowfall
{
    private const float SnowHeightAboveBoard = 2.5f;
    private const float SnowSlabHeight = 1f;
    private const int MaxMeltZoneCount = 6;

    // 보드 전체를 덮도록 크기를 맞춰 얼음 보드 윗면 위에 만들고 모닥불 구역을 등록한다.
    public IceSnowfall(MapBoard board, GameObject prefab, IReadOnlyList<Collider> meltZones)
    {
        Bounds bounds = board.WorldBounds;
        Vector3 spawnCenter = new Vector3(bounds.center.x, bounds.max.y + SnowHeightAboveBoard, bounds.center.z);
        GameObject instance = Object.Instantiate(prefab, spawnCenter, Quaternion.identity, board.transform);

        ParticleSystem particles = instance.GetComponent<ParticleSystem>();
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.scale = new Vector3(bounds.size.x, SnowSlabHeight, bounds.size.z);

        SetMeltZones(particles, meltZones);
    }

    // 모닥불 구역에 들어온 눈송이를 지우도록 파티클에 등록한다.
    private static void SetMeltZones(ParticleSystem particles, IReadOnlyList<Collider> meltZones)
    {
        ParticleSystem.TriggerModule trigger = particles.trigger;
        trigger.enabled = true;
        trigger.inside = ParticleSystemOverlapAction.Kill;

        int zoneCount = Mathf.Min(meltZones.Count, MaxMeltZoneCount);
        ReportZoneOverflow(meltZones.Count, zoneCount);
        for (int index = 0; index < zoneCount; index++)
        {
            trigger.SetCollider(index, meltZones[index]);
        }
    }

    // 등록 한도를 넘은 모닥불이 있으면 조용히 넘기지 않고 알린다.
    private static void ReportZoneOverflow(int requestCount, int usedCount)
    {
        if (requestCount <= usedCount)
        {
            return;
        }

        Debug.LogError($"[IceSnowfall] 모닥불 {requestCount}개 중 {usedCount}개만 눈 제거 구역으로 등록됐다 — 나머지 자리에는 눈이 계속 내린다.");
    }
}
