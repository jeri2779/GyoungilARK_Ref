using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Skill/HealSkill")]
public class HealSkillDataSO : UtilitySkillDataSO
{
    public float valueScale;   // 스테이지당 힐량 증가
    public GameObject AoEField;
    // public GameObject HealEffect; //나중에 힐될때 팡 터지는 이펙트용

    // Cooldown 을 틱 간격으로 사용한다(예: 0.5초). EnemyBase 스킬 루프가 쿨다운마다 호출 = 매 틱 1회 힐.
    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        // fire-and-forget: 힐 장판은 Execute보다 오래 산다. 스킬 루프가 넘겨준 token은 이 Execute 스코프에서
        // 만료(dispose)되므로, 유닛 수명 토큰(LifetimeToken)으로 돌려야 디스폰/사망 시 정상 취소된다.
        TickRateInterval(owner, owner.LifetimeToken).Forget();
        await UniTask.CompletedTask;
    }
    private async UniTask TickRateInterval(EnemyBase owner,CancellationToken token)
    {
        float tickTime =0f;
        float durationTime = 0f;
        GameObject go = PoolManager.Instance.Spawn(AoEField,owner.transform.position,Quaternion.identity);
        while(durationTime<duration)
        {
            durationTime+=Time.deltaTime;
            tickTime += Time.deltaTime;
            if(tickTime>=tickInterval)
            {
                tickTime = 0f;
                float heal = value + valueScale * (CurrentStage() - 1);   // value = 기본 힐량
                if (heal <= 0f) return;

                int cellRange = range > 0f ? Mathf.RoundToInt(range) : 1;
                Vector3 center = owner.transform.position;

                foreach (var ally in EnemyRegistry.Alive)
                {
                    if (ally == null || ally.IsDead) continue;
                    if (!EnemyTargeting.InRange(center, ally.transform.position, cellRange)) continue;
                    ally.Heal(heal);
                }
            }
            await UniTask.Yield(token);
        }
        PoolManager.Instance.Despawn(go);
    }
    // TODO: 스테이지 시스템 생기면 현재 스테이지 반환하도록 연결 (EnemyTable.UpHealthScale 도 여기 연동 예정)
    private static int CurrentStage() => 1;
}
