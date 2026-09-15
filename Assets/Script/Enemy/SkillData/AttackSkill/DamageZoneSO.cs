using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class DamageZoneSO : AttackSkillDataSO
{
    // 배경 오라 — 죽을 때까지 지속되므로 일반 공격 루프를 막지 않는다(안 그러면 평생 공격 불가).
    public override bool BlocksBasicAttack => false;
    public GameObject damageZoneEffectPrefab;

    [Header("이펙트 크기")]
    [Tooltip("range가 1일 때 이펙트에 줄 스케일. 눈으로 보면서 맞추면 된다. " +
             "range가 늘어나면 여기에 비례해 커진다. 0이면 크기를 안 건드린다(프리팹 그대로).")]
    [SerializeField] private float scaleAtRange1 = 0f;

    private bool loggedScale;

    // 이펙트를 range(타일 수)에 비례해 키운다.
    // 파티클 이펙트의 '실제로 보이는 반지름'은 코드로 재봐야 못 맞춘다
    // (텍스처의 투명 여백, Size over Lifetime 커브, 서브 이미터가 다 섞인다).
    // 그래서 추정하지 않고, range 1에서의 스케일을 인스펙터에서 눈으로 맞추게 한다.
    private void FitEffectToRange(GameObject effect, EnemyBase owner, int cellRange)
    {
        if (effect == null || scaleAtRange1 <= 0f) return;   // 0이면 프리팹 원래 크기 유지

        float factor = scaleAtRange1 * cellRange;
        // 프리팹이 비균일 스케일로 만들어졌을 수 있으니 비율을 유지한 채 곱한다.
        effect.transform.localScale = damageZoneEffectPrefab.transform.localScale * factor;

        if (!loggedScale)
        {
            loggedScale = true;
            // Debug.Log($"DamageZoneSO({name}): range {cellRange} → localScale {factor:0.###}");
        }
    }

    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.IsDead) return;
        // 이 SO는 Resources.Load로 모든 적이 공유하는 애셋이다(EnemyStatLoader.ResolveSkills).
        // 이펙트 핸들을 필드에 두면 나중에 시전한 적이 앞선 적의 핸들을 덮어써서
        // 남의 살아있는 이펙트를 Despawn하고 자기 것은 풀에 못 돌려주는 누수가 난다. 반드시 지역 변수로.
        GameObject go = null;
        try
        {
            int cellRange = range > 0f ? Mathf.RoundToInt(range) : 1;
            Vector3 center = owner.transform.position; // 나중에 장판 이펙트/파티클 스폰 위치로 사용 예정
            float t = 0f;
            if(damageZoneEffectPrefab !=null)
            {
                // Spawn은 넘긴 회전을 '월드 회전'으로 덮어쓴다(PoolManager의 SetPositionAndRotation).
                // Quaternion.identity를 넘기면 프리팹에 구워둔 눕힌 회전이 지워져 장판이 세워진다.
                go = PoolManager.Instance.Spawn(damageZoneEffectPrefab,center,damageZoneEffectPrefab.transform.rotation,owner.transform);
                FitEffectToRange(go, owner, cellRange);
            }
            while(!owner.IsDead)
            {
                t +=Time.deltaTime;
                if(t>tickInterval)
                {
                    if (owner.Board == null) return;
                    Vector2Int origin = owner.Board.WorldToCell(owner.transform.position);
                    List<GameObject> target = new();
                    foreach (Tile tile in owner.Board.GetTiles(origin, cellRange))
                    {
                        if (tile.OccupantObject == null) continue;
                        target.Add(tile.OccupantObject);
                    }
                    int d = Mathf.RoundToInt(damage);
                    foreach(var g in target)
                    {
                        if (g.GetComponentInParent<IDamageAble>() is IDamageAble dmg)
                            dmg.TakeDamage(d);
                    }
                    t = 0;
                }
                await UniTask.Yield(token);
            }
        }
        catch (OperationCanceledException)
        {
            
        }
        finally
        {
            if(go!=null)PoolManager.Instance.Despawn(go);   
        }

    }
}
