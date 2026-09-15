using System.Collections.Generic;
using UnityEngine;

// 얼음 보드에 놓인 모닥불 불빛을 모아 밤/낮에 맞춰 한꺼번에 켜고 끈다.
public class CampfireLightController : IZoneEffect
{
    private readonly List<CampfireLight> lights = new();
    private readonly List<MapBoard> lightBoards = new();

    // 이 얼음 보드의 장식 자리를 훑어 모닥불 불빛을 모으고, 보호 반경(월드 단위)으로 범위를 맞춘다.
    public void Collect(MapBoard iceBoard, int campfireRange)
    {
        Transform holder = iceBoard.transform.parent != null ? iceBoard.transform.parent : iceBoard.transform;
        CampfireLight[] found = holder.GetComponentsInChildren<CampfireLight>(true);
        // +1.5칸: 정확한 마름모 경계보다 넉넉하게 넘치도록 잡는다 — 감쇠(falloff) 탓에 Range를 딱 맞추면 오히려 좁아 보인다.
        float worldRange = (campfireRange + 2.0f) * iceBoard.CellSize;

        for (int index = 0; index < found.Length; index++)
        {
            found[index].SetRange(worldRange);
            lights.Add(found[index]);
            lightBoards.Add(iceBoard);
        }
    }

    // 밤이 시작되면 소속 모듈이 해금된 불빛만 켠다. 잠긴 모듈의 불빛은 건너뛴다.
    public void OnNightChanged()
    {
        for (int index = 0; index < lights.Count; index++)
        {
            if (!lightBoards[index].IsUnlocked)
            {
                continue;
            }

            lights[index].SetOn(true);
        }
    }

    // 낮이 시작되면 모아둔 불빛을 전부 끈다.
    public void OnDayChanged()
    {
        for (int index = 0; index < lights.Count; index++)
        {
            lights[index].SetOn(false);
        }
    }
}
