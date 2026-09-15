using Cysharp.Threading.Tasks;
using UnityEngine;

public class Bat : EnemyBase
{
    public float hp;
    public float speed;
    public int attack;
    public int def; //테스트용 인스펙터 확인용 스탯들
    private GameObject attackPrefab;
    // _pool/Construct/Pool은 EnemyBase가 이미 제공 — 여기서 다시 선언하면 필드명 중복 직렬화 경고가 난다.
    private void Start()
    {
        hp = Hp;
        speed = MoveSpeed;
        attack = AttackPower;
        def = Defense; //테스트용
        attackPrefab = Resources.Load<GameObject>($"EnemyEffectPrefab/BatAttack");
    }

    public override void AnimEvent_AttackHit()
    {
        // base.AnimEvent_AttackHit();
        GameObject go = FindAttackTarget();
        if (go == null || attackPrefab == null) return; // 히트 프레임에 대상 이탈/사망 or 프리팹 로드 실패 시 헛발사 방지
        GameObject attack = Pool.Spawn(attackPrefab,transform.position,Quaternion.identity);
        EnemySoundAttack();
        AttackTarget(go,attack).Forget();
    }
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("DashSkill", at: transform.position);

    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("FlyDie", at: transform.position);
    }

    private async UniTask AttackTarget(GameObject target,GameObject attackprefab)
    {
        float wavespeed = 10f;
        float t = 0f;
        Vector3 start = transform.position;
        Vector3 end = target.transform.position; // 발사 시점 위치로 직선 발사
        try
        {
            while(t<1f)
            {
                t += wavespeed*Time.deltaTime;
                attackprefab.transform.position = Vector3.Lerp(start,end,Mathf.Clamp01(t));
                await UniTask.Yield();
            }
            attackprefab.transform.position = end;
            // 도착 시점에 대상이 살아있으면 데미지. 영웅은 IDamageAble이 부모에 있을 수 있어 InParent로 탐색.
            if (target != null && target.GetComponentInParent<IDamageAble>() is IDamageAble dmg)
            {
                dmg.TakeDamage(Mathf.FloorToInt(Stats[StatType.ATK]));
                EnemySoundManager.Play("SpiderAttack", at: transform.position);
            }
        }
        catch(System.OperationCanceledException)
        {
            
        }
        finally
        {
            Pool.Despawn(attackprefab);
        }
    }
}
