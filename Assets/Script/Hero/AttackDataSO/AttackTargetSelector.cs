using System.Collections.Generic;
using UnityEngine;

public static class AttackTargetSelector
{
    // pool: 후보 목록
    // shotCount: 총 발사/타격 수 (attackCount)
    // distinctCap: 서로 다른 대상 최대 수 (targetCount)
    // 반환: 길이 shotCount 의 타겟 목록. 서로 다른 대상을 distinctCap개까지 우선 배치하고,
    //       대상이 부족하면 라운드로빈으로 중복시킨다.
    public static List<T> SelectTargets<T>(List<T> pool, int shotCount, int distinctCap)
    {
        var result = new List<T>(shotCount);
        if (pool == null || pool.Count == 0 || shotCount <= 0)
            return result;

        int poolSize = Mathf.Min(distinctCap, pool.Count);
        for (int i = 0; i < shotCount; i++)
            result.Add(pool[i % poolSize]);

        return result;
    }
}
