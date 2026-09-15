using System.Collections.Generic;
using UnityEngine;

// 체인 라이트닝: start에게 즉시 피해 적용 후, 이전 타겟 위치 기준 chainRange 내의 아직 맞지 않은 적 중
// 가장 가까운 하나로 전이. chainFalloff를 매 점프마다 곱해 감쇄시키며 chainCount(최초 타격 포함)에
// 도달하거나 후보가 없으면 멈춘다.
public static class ChainResolver
{
    public static List<GameObject> Resolve(
        GameObject start, float baseDamage, int chainRange, int chainCount, float chainFalloff,
        System.Func<Vector3, int, RangeShape, List<GameObject>> getEnemiesNear,
        System.Action<GameObject, int, bool> onHit = null)
    {
        var hitOrder = new List<GameObject>();
        if (start == null) return hitOrder;

        var visited = new HashSet<GameObject> { start };
        hitOrder.Add(start);
        ApplyDamage(start, baseDamage, onHit);
        GameObject current = start;
        float damage = baseDamage;
        for (int jump = 1; jump < chainCount; jump++)
        {
            List<GameObject> candidates = getEnemiesNear(current.transform.position, chainRange, RangeShape.Diamond);
            GameObject next = NearestUnvisited(current.transform.position, candidates, visited);
            if (next == null) break;

            damage *= chainFalloff;
            ApplyDamage(next, damage, onHit);
            visited.Add(next);
            hitOrder.Add(next);
            current = next;
        }
        return hitOrder;
    }

    private static void ApplyDamage(GameObject go, float damage, System.Action<GameObject, int, bool> onHit)
    {
        if (go == null || go.GetComponentInParent<IDamageAble>() is not IDamageAble d) return;
        int amount = (int)damage;
        d.TakeDamage(amount);
        onHit?.Invoke(go, amount, false);
    }

    private static GameObject NearestUnvisited(Vector3 from, List<GameObject> candidates, HashSet<GameObject> visited)
    {
        GameObject best = null;
        float bestSqr = float.MaxValue;
        foreach (GameObject go in candidates)
        {
            if (go == null || visited.Contains(go)) continue;
            float sqr = (go.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = go; }
        }
        return best;
    }
}
