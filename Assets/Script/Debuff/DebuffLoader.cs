using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DebuffId로 임포터가 만든 SO를 꺼낸다. EnemyStatLoader.ResolveSkills와 같은 규약 —
/// CSV 한 칸에 ';'로 여러 개를 적을 수 있다("Slow_Basic;Bleed_Basic").
/// </summary>
public static class DebuffLoader
{
    public static DebuffSO Get(string debuffId)
    {
        if (string.IsNullOrEmpty(debuffId)) return null;

        var so = Resources.Load<DebuffSO>($"DebuffSO/{debuffId.Trim()}");
        if (so == null)
            Debug.LogWarning($"DebuffLoader: 디버프 '{debuffId}' 로드 실패 (Resources/DebuffSO/{debuffId}). 임포터를 먼저 실행했는지 확인");
        return so;
    }

    public static void Resolve(string debuffIds, List<DebuffSO> into)
    {
        if (string.IsNullOrEmpty(debuffIds) || into == null) return;

        foreach (var raw in debuffIds.Split(';'))
        {
            var so = Get(raw);
            if (so != null && !into.Contains(so)) into.Add(so);
        }
    }
}
