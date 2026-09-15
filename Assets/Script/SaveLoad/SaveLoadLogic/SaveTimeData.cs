using System;
using UnityEngine;
using VContainer.Unity;

// 저장·로드 사이에 이어져야 하는 값을 보관한다 (플레이 시간, 지금까지 저장된 일차 목록).
public class SaveTimeData : ITickable
{
    public float PlayTime { get; private set; }
    public int[] DayList { get; private set; } = Array.Empty<int>();

    // 매 프레임 플레이 시간을 늘린다
    public void Tick()
    {
        PlayTime += Time.deltaTime;
    }

    // 로드된 값으로 플레이 시간을 교체한다
    public void SetPlayTime(float playTime)
    {
        PlayTime = playTime;
    }

    // 계산된 새 일차 목록으로 교체한다
    public void SetDayList(int[] dayList)
    {
        DayList = dayList;
    }
}
