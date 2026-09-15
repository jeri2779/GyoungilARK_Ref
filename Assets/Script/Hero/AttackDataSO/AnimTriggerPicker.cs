using System.Collections.Generic;
using UnityEngine;

// MeleeAttackExecutor / RangedAttackExecutor / HealAttackExecutor에 거의 동일하게 복제돼 있던
// PickTrigger를 모은 것. static 헬퍼가 아니라 executor가 필드로 소유하는 인스턴스 클래스인 이유:
// executor는 {SwordMan,Archer,Mage,Healer}AttackState.Enter()에서 Hero마다 새로 생성되므로
// sequentialIndices는 지금 Hero별로 독립적이다. static으로 만들면 같은 AttackDataSO 에셋을 쓰는
// 모든 Hero가 인덱스를 공유해 Sequential 콤보 순서가 섞인다(동작 변경).
public sealed class AnimTriggerPicker
{
    private readonly Dictionary<AttackDataSO, int> sequentialIndices = new();
    private readonly string ownerLabel;

    public AnimTriggerPicker(string ownerLabel) => this.ownerLabel = ownerLabel;

    public string Pick(AttackDataSO data)
    {
        string[] triggers = data.animTriggers;
        if (triggers == null || triggers.Length == 0)
        {
            Debug.LogError($"[{ownerLabel}] '{data.name}' 의 animTriggers가 비어 있습니다.");
            return string.Empty;
        }
        if (triggers.Length == 1) return triggers[0];

        if (data.selectMode == AnimSelectMode.Random)
            return triggers[Random.Range(0, triggers.Length)];

        sequentialIndices.TryGetValue(data, out int idx);
        sequentialIndices[data] = (idx + 1) % triggers.Length;
        return triggers[idx];
    }
}
