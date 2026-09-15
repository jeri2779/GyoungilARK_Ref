using UnityEngine;

/// <summary>
/// 걸린 디버프에 맞춰 월드 이펙트를 소환/반납한다. EnemyBase가 소유하고 매 프레임 Tick으로 굴린다
/// (EnemyCloak/EnemyBurrow와 같은 구조). 이전 EnemyStunEffect를 종류별로 넓힌 것이다.
///
/// 소환/반납을 "걸린 순간"이 아니라 "지금 걸려 있는가"로 굴리는 게 핵심이다(EnemyDebuffBar와 같은 방식).
/// 그래서 같은 디버프가 겹쳐 들어와도 이펙트는 늘 하나이고, 만료 시각이 뒤로 밀리면 이펙트도 그만큼 더 유지된다 —
/// Despawn(go, duration)으로 미리 예약해두면 나중에 들어온 디버프와 시각이 어긋난다.
///
/// 위치는 부모로 붙이지 않고 매 프레임 앵커를 따라간다(EnemyBurrow의 지면 마커와 같은 방식).
/// 자식으로 붙이면 유닛 스케일이 이펙트에 곱해져서, 보스나 분열체(SetScaleMul)에서 크기가 달라진다.
///
/// 아이콘(EnemyDebuffBar)과 달리 매 프레임 돈다 — 앵커를 따라가야 하므로 폴링 간격을 둘 수 없다.
/// </summary>
public class EnemyDebuffEffects
{
    private DebuffEffect[] _entries;
    private GameObject[] _spawned;     // 칸별 현재 소환된 이펙트. null = 안 걸린 상태
    private Transform _head;
    private Transform _body;
    private Transform _self;           // Body 앵커를 안 꽂은 프리팹의 대체 위치

    // _spawned를 함께 보는 이유는 Setup 전에 Reset/Tick이 불려도 안전하게 no-op이 되게 하기 위함이다.
    public bool IsSetup => _entries != null && _spawned != null;

    /// <summary>
    /// EnemyBase가 Awake에서 1회 호출. head/body는 프리팹별 앵커고, set은 공용 에셋이다.
    /// stunFallbackPrefab은 에셋을 아직 안 만든 프리팹에서도 기존 기절 이펙트가 계속 뜨게 하는 대비책 —
    /// set에 Stun 칸이 있으면 그쪽이 이긴다.
    /// </summary>
    public void Setup(DebuffEffectSetSO set, Transform head, Transform body, Transform self,
        GameObject stunFallbackPrefab = null)
    {
        _head = head;
        _body = body;
        _self = self;

        DebuffEffect[] entries = set != null ? set.effects : null;

        // 에셋에 Stun 칸이 없고 대비책 프리팹이 있으면 머리 위 Stun 칸을 하나 만들어 붙인다.
        // prefab이 비었는지는 보지 않는다 — 칸을 선언한 것만으로 대비책을 끈다.
        // (프리팹을 아직 안 꽂은 Stun 칸이 있는데 대비책까지 붙이면, 나중에 그 칸을 채운 순간 별이 두 개 겹친다.)
        bool hasStun = false;
        if (entries != null)
            for (int i = 0; i < entries.Length; i++)
                if ((entries[i].type & DebuffType.Stun) != 0) hasStun = true;

        if (!hasStun && stunFallbackPrefab != null)
        {
            var fallback = new DebuffEffect
            {
                type = DebuffType.Stun,
                prefab = stunFallbackPrefab,
                anchor = DebuffEffectAnchor.Head,
            };

            if (entries == null || entries.Length == 0)
            {
                entries = new[] { fallback };
            }
            else
            {
                var merged = new DebuffEffect[entries.Length + 1];
                entries.CopyTo(merged, 0);
                merged[entries.Length] = fallback;
                entries = merged;
            }
        }

        if (entries == null || entries.Length == 0) return;   // 이후 모든 호출이 no-op

        _entries = entries;
        _spawned = new GameObject[_entries.Length];
    }

    public void Tick(DebuffTracker tracker, bool suppress)
    {
        if (!IsSetup) return;

        if (suppress || tracker == null) { Reset(); return; }

        DebuffType active = tracker.ActiveMask;

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i].prefab == null) continue;   // 종류만 골라두고 프리팹을 안 넣은 칸

            bool on = (active & _entries[i].type) != 0;

            // 파괴된 오브젝트도 == null이 true라, 밖에서 사라졌으면 저절로 다시 소환된다.
            if (!on)
            {
                Despawn(i);
                continue;
            }

            Transform anchor = AnchorOf(_entries[i].anchor);
            if (anchor == null) continue;   // Head 앵커를 안 꽂은 프리팹 — 그 칸만 조용히 건너뛴다

            Vector3 pos = anchor.position + _entries[i].offset;

            if (_spawned[i] == null)
            {
                _spawned[i] = PoolManager.Instance.Spawn(_entries[i].prefab, pos, _entries[i].prefab.transform.rotation);

                // 효과음은 이펙트가 "새로 뜨는 순간"에만 낸다 — 이펙트와 소리가 한 자리에서 갈리지 않게.
                // 여기 두면 이펙트가 유지되는 동안(겹쳐 걸려 만료가 밀려도) 다시 울리지 않는다.
                // 여러 적이 같은 프레임에 같이 걸려도 EnemySoundManager의 같은 키 스로틀이 한 번으로 묶는다.
                // pos를 같이 넘겨 화면 밖 유닛의 디버프 소리는 컷되게 한다 — 무리로 걸릴 때 혼잡의 큰 몫이다.
                if (!string.IsNullOrEmpty(_entries[i].soundKey)) EnemySoundManager.Play(_entries[i].soundKey, at: pos);
            }
            else
                _spawned[i].transform.position = pos;   // 넉백·잠행 등으로 유닛이 움직여도 따라붙게
        }
    }

    /// <summary>
    /// 디버프 해제·사망·풀 반납에서 호출. 이펙트를 전부 풀에 되돌린다.
    /// 안 되돌리면 적이 풀로 돌아갈 때 이펙트가 떠 있는 채로 남는다(EnemyBurrow 지면 마커와 같은 함정).
    /// </summary>
    public void Reset()
    {
        if (!IsSetup) return;

        for (int i = 0; i < _spawned.Length; i++) Despawn(i);
    }

    // 머리·몸통 높이는 유닛마다 달라 앵커 없이는 알 수 없다 — 없으면 그 칸을 포기한다.
    // Body에 원점 대체를 두지 않는 이유: 그러면 Foot과 구분이 안 되면서 "몸통인데 발밑에 뜨는" 상태가 조용히 생긴다.
    // 발밑은 크기와 무관하게 항상 원점이라 Foot만 앵커가 필요 없다.
    private Transform AnchorOf(DebuffEffectAnchor anchor)
    {
        return anchor switch
        {
            DebuffEffectAnchor.Head => _head,
            DebuffEffectAnchor.Body => _body,
            DebuffEffectAnchor.Foot => _self,
            _ => null,
        };
    }

    private void Despawn(int index)
    {
        if (_spawned[index] == null) { _spawned[index] = null; return; }

        PoolManager.Instance.Despawn(_spawned[index]);
        _spawned[index] = null;
    }
}
