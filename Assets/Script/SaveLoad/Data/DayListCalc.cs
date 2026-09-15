using System;

// 기존 일차 목록에 오늘 일차를 더한 새 목록을 계산한다 (상태 없음, 디스크 접근 없음).
public static class DayListCalc
{
    // 저장된 일차 목록 끝에 오늘 일차를 더한 새 배열을 만든다. 마지막 값이 이미 오늘이면 그대로 돌려준다(중복 방지).
    public static int[] AppendDay(int[] savedDays, int todayDay)
    {
        if (savedDays.Length > 0 && savedDays[savedDays.Length - 1] == todayDay)
        {
            return savedDays;
        }

        int[] result = new int[savedDays.Length + 1];
        Array.Copy(savedDays, result, savedDays.Length);
        result[savedDays.Length] = todayDay;
        return result;
    }
}
