using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장부(DebuffTracker)가 없는 대상 — 지금은 영웅 — 의 디버프 이펙트를 그린다.
///
/// 적은 EnemyDebuffEffects가 프리팹 앵커(머리·몸통)를 보고 그리지만 영웅 프리팹엔 아직 그 앵커가 없다.
/// 그래서 여기선 <b>앵커 종류를 무시하고 대상(target) 원점 기준</b>으로 띄운다.
/// 영웅에 Head/Body 앵커가 생기는 날 EnemyDebuffEffects와 같은 규칙으로 옮기면 된다.
///
/// 이펙트 프리팹 목록은 적과 같은 공용 에셋(DebuffEffectSetSO)을 그대로 돌려쓴다 — 종류를 추가할 때
/// 에셋 하나만 고치면 적·영웅에 같이 반영된다.
///
/// "걸린 순간"이 아니라 "지금 걸려 있는가"로 굴린다(EnemyDebuffEffects와 같은 방식).
/// DotRegistry.Tick이 매 프레임 살아있는 상태를 그대로 넘겨 주므로, 같은 디버프가 겹쳐 들어와도
/// 이펙트는 늘 하나이고 만료 시각이 밀리면 이펙트도 그만큼 더 유지된다.
///
/// Hero.cs는 건드리지 않는다(팀원 소유) — 영웅에 컴포넌트를 붙이지 않고 DotRegistry가 밀어 주는 구조다.
/// </summary>
public static class DebuffEffectView
{
    // EnemyBase.DebuffEffectSetPath와 같은 에셋. 그쪽이 private const라 문자열을 공유하지 못한다 —
    // 경로를 옮길 때 두 곳을 같이 고쳐야 한다.
    private const string SetPath = "EnemyEffectPrefab/DebuffEffectSet";

    private static DebuffEffectSetSO set;
    private static bool setLoaded;

    // 대상 → 칸별 소환된 이펙트(칸 인덱스는 set.effects와 같다). null = 그 칸은 안 걸린 상태.
    private static readonly Dictionary<Component, GameObject[]> spawned = new();
    // 순회 중 제거하면 인덱스가 깨지므로 지울 키를 모아 뒀다가 지운다(매 프레임 재사용해 할당을 피한다).
    private static readonly List<Component> stale = new();

    /// <summary>
    /// 지속 피해가 아닌 디버프(스탯·상태)가 장부 없는 대상에 걸렸을 때의 만료 시각.
    /// DotRegistry가 지속 피해를 들고 굴리듯, 이쪽은 나머지를 든다 — 빙결·둔화는 BuffManager로 가는데
    /// 거긴 이펙트 뷰와 연결이 없어서 여기 기록해두지 않으면 영웅에게 아무것도 안 뜬다.
    /// </summary>
    private struct Entry
    {
        public Component Host;
        public DebuffType Type;
        public float Expiry;
        public object Source;
    }

    private static readonly List<Entry> tracked = new();
    // 지속 피해 마스크와 위 장부를 합친 이번 프레임 결과. 매 프레임 재사용해 할당을 피한다.
    private static readonly Dictionary<Component, DebuffType> merged = new();

    // 도메인 리로드를 끈 플레이 모드에서 지난 세션의 이펙트 기록이 남는 것을 막는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        spawned.Clear();
        stale.Clear();
        tracked.Clear();
        merged.Clear();
        set = null;
        setLoaded = false;
    }

    /// <summary>
    /// 장부 없는 대상에 지속 피해가 아닌 디버프가 걸렸음을 알린다(DebuffSO.Apply가 부른다).
    /// 같은 대상·같은 종류가 이미 있으면 더 늦은 만료 시각이 이긴다 — 겹쳐 들어와도 이펙트는 하나다.
    /// </summary>
    public static void Track(Component host, DebuffType type, float duration, object source = null)
    {
        if (host == null || type == DebuffType.None || duration <= 0f) return;
        if (!EnsureSet()) return;   // 이펙트 에셋이 없으면 기록해봐야 그릴 것이 없다

        // 만료를 굴려 줄 곳이 DotRegistry.Tick(DotDriver)뿐이다. 지속 피해가 한 번도 안 걸린 판에서는
        // 구동체가 아예 없으므로 여기서 만들어 둔다 — 안 하면 걸리기만 하고 영영 안 사라진다.
        DotDriver.Ensure();

        float expiry = Time.time + duration;
        for (int i = 0; i < tracked.Count; i++)
        {
            if (tracked[i].Host != host || tracked[i].Type != type || tracked[i].Source != source) continue;
            if (tracked[i].Expiry >= expiry) return;   // 이미 더 늦게 끝난다
            Entry longer = tracked[i];
            longer.Expiry = expiry;
            tracked[i] = longer;
            return;
        }
        tracked.Add(new Entry { Host = host, Type = type, Expiry = expiry, Source = source });
    }

    // 대상에게 특정 출처가 표시한 디버프 효과를 제거한다.
    public static void Remove(Component host, DebuffType type, object source)
    {
        for (int entryIndex = tracked.Count - 1; entryIndex >= 0; entryIndex--)
        {
            Entry entry = tracked[entryIndex];
            if (entry.Host == host && entry.Type == type && entry.Source == source)
            {
                tracked.RemoveAt(entryIndex);
            }
        }
    }

    /// <summary>
    /// 이번 프레임 지속 피해 상태를 그대로 넘긴다(대상 → 걸린 종류 마스크). DotRegistry.Tick이 매 프레임 부른다.
    /// 여기에 Track으로 쌓인 스탯·상태 디버프를 합쳐 그린다.
    /// 목록에서 빠진 대상의 이펙트는 여기서 반납된다 — 호출부가 해제를 따로 알려 줄 필요가 없다.
    /// </summary>
    public static void Sync(Dictionary<Component, DebuffType> dots)
    {
        if (!EnsureSet()) return;

        // 0) 지속 피해 마스크 + 이 클래스가 든 장부를 합친다.
        merged.Clear();
        if (dots != null)
        {
            foreach (KeyValuePair<Component, DebuffType> pair in dots)
                if (pair.Key != null) merged[pair.Key] = pair.Value;
        }

        // 만료·파괴·사망을 여기서 걷어낸다. 지속 피해가 아닌 디버프는 이 장부가 유일한 시계다.
        // 사망 판정을 DotRegistry와 같은 규칙(Hp <= 0)으로 두어, 죽은 영웅 위에 서리가 남지 않게 한다.
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            Entry e = tracked[i];
            if (e.Host == null || Time.time >= e.Expiry
                || (e.Host is IDamageAble dmg && dmg.Hp <= 0f))
            {
                tracked.RemoveAt(i);
                continue;
            }

            merged.TryGetValue(e.Host, out DebuffType mask);
            merged[e.Host] = mask | e.Type;
        }

        // 1) 이번 프레임 목록에 없는 대상(해제·사망·풀 반납·파괴)은 전부 반납.
        stale.Clear();
        foreach (KeyValuePair<Component, GameObject[]> pair in spawned)
        {
            if (pair.Key == null || !merged.ContainsKey(pair.Key))
                stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++) Clear(stale[i]);

        // 2) 걸려 있는 대상은 마스크대로 칸을 켜고 끈다.
        foreach (KeyValuePair<Component, DebuffType> pair in merged)
        {
            if (pair.Key == null) continue;
            Apply(pair.Key, pair.Value);
        }
    }

    /// <summary>대상의 이펙트를 전부 풀에 되돌린다. 안 되돌리면 영웅이 사라져도 이펙트가 떠 있는 채로 남는다.</summary>
    public static void Clear(Component host)
    {
        if (host == null)
        {
            // 파괴된 대상은 키로 다시 찾을 수 없으므로 null 키 항목을 통째로 걷어낸다.
            RemoveDestroyed();
            for (int i = tracked.Count - 1; i >= 0; i--)
                if (tracked[i].Host == null) tracked.RemoveAt(i);
            return;
        }

        // 장부도 같이 끊는다 — 안 끊으면 다음 프레임 Sync가 만료 전인 항목을 보고 이펙트를 다시 소환한다.
        for (int i = tracked.Count - 1; i >= 0; i--)
            if (tracked[i].Host == host) tracked.RemoveAt(i);

        if (!spawned.TryGetValue(host, out GameObject[] slots)) return;

        for (int i = 0; i < slots.Length; i++) Despawn(slots, i);
        spawned.Remove(host);
    }

    private static void Apply(Component host, DebuffType mask)
    {
        DebuffEffect[] effects = set.effects;
        if (!spawned.TryGetValue(host, out GameObject[] slots))
        {
            slots = new GameObject[effects.Length];
            spawned[host] = slots;
        }

        // 앵커 종류(Head/Body/Foot)는 보지 않는다 — 영웅엔 앵커가 없으므로 대상 원점이 유일한 기준이다.
        Vector3 origin = host.transform.position;

        for (int i = 0; i < effects.Length && i < slots.Length; i++)
        {
            if (effects[i].prefab == null) continue;   // 종류만 골라두고 프리팹을 안 넣은 칸

            if ((mask & effects[i].type) == 0)
            {
                Despawn(slots, i);
                continue;
            }

            Vector3 pos = origin + effects[i].offset;

            // 파괴된 오브젝트도 == null이 true라, 밖에서 사라졌으면 저절로 다시 소환된다.
            if (slots[i] == null)
            {
                slots[i] = PoolManager.Instance.Spawn(effects[i].prefab, pos, effects[i].prefab.transform.rotation);

                // 효과음도 이펙트와 같은 자리에서 낸다(EnemyDebuffEffects와 같은 규칙 — 뜨는 순간 한 번).
                // 지금은 이쪽과 EnemyDebuffEffects가 장부(ledger) 유무의 반대편을 보고 있어 같은 대상에 둘이 겹치지 않는다
                // (여기 오는 건 DebuffSO가 ledger == null일 때만 Track한 대상, 저쪽은 tracker가 있어야 그린다).
                // 그 갈림이 바뀌면 같은 디버프에 소리가 두 번 나므로 그때 한쪽으로 몰아야 한다.
                if (!string.IsNullOrEmpty(effects[i].soundKey)) EnemySoundManager.Play(effects[i].soundKey, at: pos);
            }
            else
                slots[i].transform.position = pos;   // 영웅이 움직여도(넉백 등) 따라붙게
        }
    }

    // 키가 파괴된(Destroy된) 항목들을 걷어낸다. Dictionary는 파괴된 키로 조회가 안 되므로 순회로 찾는다.
    private static void RemoveDestroyed()
    {
        stale.Clear();
        foreach (KeyValuePair<Component, GameObject[]> pair in spawned)
        {
            if (pair.Key != null) continue;
            for (int i = 0; i < pair.Value.Length; i++) Despawn(pair.Value, i);
            stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++) spawned.Remove(stale[i]);
    }

    private static void Despawn(GameObject[] slots, int index)
    {
        if (slots[index] == null) { slots[index] = null; return; }

        PoolManager.Instance.Despawn(slots[index]);
        slots[index] = null;
    }

    // 공용 에셋을 1회 로드. 없으면(에셋을 안 만들었으면) 이후 모든 호출이 조용히 no-op이 된다.
    private static bool EnsureSet()
    {
        if (!setLoaded)
        {
            setLoaded = true;
            set = Resources.Load<DebuffEffectSetSO>(SetPath);
        }
        return set != null && set.effects != null && set.effects.Length > 0;
    }
}
