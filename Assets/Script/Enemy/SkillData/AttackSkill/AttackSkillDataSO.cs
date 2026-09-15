using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

[CreateAssetMenu(menuName = "Data/Skill/AttackSkill")]
public class AttackSkillDataSO : SkillDataSO
{
    public float damage;

    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        // 아직 타겟 시스템같은게 없어서 일단 보류
        owner.Attack();
        await UniTask.CompletedTask;
    }
}
