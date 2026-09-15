using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct StaticMoveArea
{
    public Transform center;
    public Vector2 halfSize;
}

// 해금된 모듈만 감싸는 카메라 이동 경계를 만든다.
public class CameraLimit
{
    public Bounds Area { get; private set; }

    // 해금된 모듈 경계를 계산한다.
    public bool Build(MapRegistry registry, IReadOnlyList<StaticMoveArea> staticAreas)
    {
        bool hasArea = false;
        Bounds next = default;
        foreach (ModuleLogic module in registry.AllModules.Values)
        {
            MapBoard board = module.GetComponent<MapBoard>();
            if (board.CellCount == 0)
            {
                board.Build();
            }
            if (!hasArea)
            {
                next = board.WorldBounds;
                hasArea = true;
                continue;
            }

            next.Encapsulate(board.WorldBounds);
        }

        AddStatic(staticAreas, ref next, ref hasArea);

        Area = next;
        return hasArea;
    }

    // 상시 이동 구역을 현재 카메라 경계에 합친다.
    private void AddStatic(IReadOnlyList<StaticMoveArea> staticAreas, ref Bounds next, ref bool hasArea)
    {
        for (int i = 0; i < staticAreas.Count; i++)
        {
            StaticMoveArea area = staticAreas[i];
            if (area.center == null)
            {
                continue;
            }

            Vector3 center = area.center.position;
            Vector3 size = new Vector3(area.halfSize.x * 2f, 0f, area.halfSize.y * 2f);
            Bounds staticArea = new Bounds(center, size);
            if (!hasArea)
            {
                next = staticArea;
                hasArea = true;
                continue;
            }

            next.Encapsulate(staticArea);
        }
    }
}
