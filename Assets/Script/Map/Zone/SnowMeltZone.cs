using System.Collections.Generic;
using UnityEngine;

// 모닥불 자리마다 눈송이가 사라질 구역을 만들어 보관한다.
public class SnowMeltZone
{
    private const int IgnoreRaycastLayer = 2;
    private const string ZoneObjectName = "SnowMeltZone";

    private readonly List<Collider> zones = new();

    public IReadOnlyList<Collider> Zones => zones;

    // 이 얼음 보드의 모닥불 자리마다 보호 반경만 한 구역을 만들어 담는다.
    public SnowMeltZone(MapBoard iceBoard, int campfireRange)
    {
        Transform holder = ReadHolder(iceBoard);
        CampfireLight[] fires = holder.GetComponentsInChildren<CampfireLight>(true);
        float meltRadius = campfireRange * iceBoard.CellSize;

        for (int index = 0; index < fires.Length; index++)
        {
            zones.Add(BuildZone(fires[index].transform.position, meltRadius, iceBoard.transform));
        }
    }

    // 모닥불 불빛이 매달린 상위 자리를 돌려준다.
    private static Transform ReadHolder(MapBoard iceBoard)
    {
        if (iceBoard.transform.parent == null)
        {
            return iceBoard.transform;
        }

        return iceBoard.transform.parent;
    }

    // 지정 위치에 눈송이만 반응하는 구 모양 구역 하나를 만든다.
    private static Collider BuildZone(Vector3 center, float radius, Transform parent)
    {
        GameObject zoneObject = new GameObject(ZoneObjectName);
        zoneObject.layer = IgnoreRaycastLayer;
        zoneObject.transform.SetParent(parent, false);
        zoneObject.transform.position = center;

        SphereCollider sphere = zoneObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;
        return sphere;
    }
}
