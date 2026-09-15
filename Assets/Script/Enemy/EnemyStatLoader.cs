using System.Collections.Generic;
using UnityEngine;

public static class EnemyStatLoader
{
    public static EnemyTable.Data Load(string enemyKey)
    {
        var enemyTable = DataTableManager.Get<EnemyTable>(DataTableIds.Enemy);
        if (enemyTable == null) return null;

        var data = enemyTable.Get(enemyKey);
        if (data == null)
            Debug.LogWarning($"EnemyStatLoader: EnemyTable에서 '{enemyKey}' 데이터를 찾을 수 없음");
        return data;
    }
    public static void ResolveSkills(string skillIds, List<SkillDataSO> into)
    {
        if (string.IsNullOrEmpty(skillIds) || into == null) return;

        foreach (var raw in skillIds.Split(';'))
        {
            var id = raw.Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var so = Resources.Load<SkillDataSO>($"Skills/{id}");
            if (so == null)
            {
                Debug.LogWarning($"EnemyStatLoader: 스킬 '{id}' 로드 실패 (Resources/Skills/{id}). 임포터를 먼저 실행했는지 확인");
                continue;
            }
            if (!into.Contains(so)) into.Add(so);
        }
    }
}
