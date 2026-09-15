using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력바 아래 디버프 아이콘 줄. EnemyBase가 소유하고 Tick으로 굴린다(EnemyCloak/EnemyHealthBar와 같은 구조).
/// 디버프가 걸리면 아이콘을 풀에서 소환해 root에 붙이고, 풀리면 풀에 반납한다.
/// 배치는 root에 달린 GridLayoutGroup이 전담 — 소환 순서대로 옆에 쌓인다.
/// root나 프리팹을 안 꽂은 프리팹이면 Setup/Tick/Reset이 전부 조용히 no-op(일반 적 전부 해당).
///
/// "지속시간이 끝나면 사라지는" 동작은 폴링으로 감지한다 — 실제 만료 시각과 일치하되 최대 RefreshInterval만큼 늦다.
///
/// 판정은 두 단계다.
/// (1) DebuffTracker에 그 종류가 있으면 켠다. 종류를 직접 알고 있으므로 정확하다 —
///     탈진처럼 스탯 셋을 한꺼번에 깎는 디버프도 아이콘 하나로 뜬다.
/// (2) 장부에 없는 종류는 "현재 스탯이 원본보다 낮은가"로 추론한다(예전 방식).
///     영웅이 거는 둔화는 아직 BuffManager로 직행해 장부를 지나지 않으므로 이 보완이 필요하다.
///     그쪽이 DebuffSO/ApplyDebuff를 타게 되면 (2)는 통째로 지워도 된다.
///
/// (2)의 한계는 그대로다: 같은 스탯에 버프와 디버프가 겹치면 구분하지 못하고
/// (폭주가 SPD에 Flat +3을 넣으므로, 폭주한 보스가 둔화되면 순 이속이 원본보다 높아 아이콘이 안 뜬다),
/// 남은 시간을 알 수 없다. (1)로 들어온 디버프는 tracker.GetRemainingNormalized로 게이지까지 붙일 수 있다.
/// </summary>
public class EnemyDebuffBar
{
    // 매 프레임 갱신할 이유가 없다. GridLayoutGroup은 자식이 드나들 때마다 레이아웃을 다시 계산한다.
    private const float RefreshInterval = 0.1f;

    private RectTransform _root;
    private GameObject _iconPrefab;
    private EnemyDebuffIcon[] _icons;
    private GameObject[] _spawned;   // 종류별 현재 소환된 아이콘. null = 안 걸린 상태
    private float _timer;

    // _spawned를 함께 보는 이유는 Setup 전에 Reset/Tick이 불려도 안전하게 no-op이 되게 하기 위함이다.
    public bool IsSetup => _icons != null && _spawned != null;

    /// <summary>
    /// EnemyBase가 Awake에서 1회 호출. root나 아이콘 설정이 비면 이후 모든 호출이 no-op가 된다.
    /// root만 프리팹별이고, 프리팹·스프라이트는 set(공용 에셋)에서 온다.
    /// </summary>
    public void Setup(RectTransform root, DebuffIconSetSO set)
    {
        if (root == null || set == null || set.iconPrefab == null) return;
        if (set.icons == null || set.icons.Length == 0) return;

        _root = root;
        _iconPrefab = set.iconPrefab;
        _icons = set.icons;
        _spawned = new GameObject[_icons.Length];
        _timer = float.MaxValue;   // 첫 Tick에서 간격을 기다리지 않고 즉시 판정
    }

    /// <summary>
    /// 걸린 디버프에 맞춰 아이콘을 소환/반납한다. baseStats는 모디파이어가 붙기 전 원본값 — EnemyBase가 ApplyData에서 기록한다.
    /// tracker는 EnemyBase가 들고 있는 장부(스턴 포함). null이면 스탯 역추적만으로 판정한다.
    /// </summary>
    public void Tick(StatContainer sc, IReadOnlyDictionary<StatType, float> baseStats, DebuffTracker tracker)
    {
        if (!IsSetup || sc == null || baseStats == null) return;

        _timer += Time.deltaTime;
        if (_timer < RefreshInterval) return;
        _timer = 0f;

        DebuffType tracked = tracker != null ? tracker.ActiveMask : DebuffType.None;
        int explained = ExplainedStats(tracked);

        for (int i = 0; i < _icons.Length; i++)
        {
            if (_icons[i].sprite == null) continue;   // 종류만 골라두고 스프라이트를 안 넣은 칸

            DebuffType kind = _icons[i].kind;
            bool on;
            if ((tracked & kind) != 0) on = true;
            // 스탯으로 판정할 수 없는 종류(독·점화 등)는 절대 켜지 않는다.
            // 기본값을 아무 StatType으로 두면 그 스탯이 깎일 때 엉뚱한 아이콘이 같이 떠버린다.
            else if (TryStatOf(kind, out StatType stat))
            {
                // 그 스탯 감소를 이미 장부의 다른 디버프가 설명하고 있으면 켜지 않는다 —
                // 탈진(AS·SPD·ATK)이 걸렸을 때 둔화·공속감소 아이콘까지 같이 뜨는 중복을 막는다.
                on = (explained & (1 << (int)stat)) == 0 && IsLowered(sc, baseStats, stat);
            }
            else on = false;

            // 파괴된 오브젝트도 == null이 true라, 밖에서 사라졌으면 저절로 다시 소환된다.
            bool spawned = _spawned[i] != null;
            if (on == spawned) continue;

            if (on) Spawn(i);
            else Despawn(i);
        }
    }

    /// <summary>
    /// 풀 반납 등에서 호출. 소환된 아이콘을 전부 풀에 되돌려, 다음 스폰이 이전 개체의 아이콘을 물고 나오지 않게 한다.
    /// (EnemyBurrow의 지면 마커가 겪던 것과 같은 함정 — 풀링에선 되돌리는 경로가 생명선이다.)
    /// </summary>
    public void Reset()
    {
        if (!IsSetup) return;

        _timer = float.MaxValue;
        for (int i = 0; i < _spawned.Length; i++) Despawn(i);
    }

    private void Spawn(int index)
    {
        // 스프라이트 없는 칸은 Tick이 미리 걸러낸다 — 여기까지 오면 항상 유효하다.
        GameObject go = PoolManager.Instance.Spawn(_iconPrefab, Vector3.zero, Quaternion.identity);
        if (go == null) return;

        // PoolManager.Spawn은 월드 이펙트 기준이라 worldPositionStays=true로 붙인다.
        // UI에 그대로 쓰면 캔버스 스케일이 1이 아닌 해상도(CanvasScaler ScaleWithScreenSize)에서
        // 크기 보정이 localScale에 들어가 아이콘이 커지거나 작아진다 — 여기서 false로 다시 붙인다.
        go.transform.SetParent(_root, false);
        go.transform.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = _icons[index].sprite;
            img.raycastTarget = false;   // 화면 위 UI가 포탈 클릭 등을 삼키지 않게
        }

        _spawned[index] = go;
    }

    private void Despawn(int index)
    {
        if (_spawned[index] == null) { _spawned[index] = null; return; }

        PoolManager.Instance.Despawn(_spawned[index]);
        _spawned[index] = null;
    }

    // 원본보다 낮아졌으면 그 스탯이 깎인 것으로 본다.
    // baseStats에 없는 스탯은 이 적의 StatContainer에도 없다 — sc[t]가 KeyNotFoundException을 던지므로 반드시 먼저 막는다.
    private static bool IsLowered(StatContainer sc, IReadOnlyDictionary<StatType, float> baseStats, StatType type)
    {
        if (!baseStats.TryGetValue(type, out float baseValue)) return false;
        // 절대 오차만 쓰면 체력(수천 단위)에서 너무 민감하고, 비율만 쓰면 이속(1~6)에서 너무 둔하다.
        float epsilon = Mathf.Max(0.01f, Mathf.Abs(baseValue) * 0.001f);
        return sc[type] < baseValue - epsilon;
    }

    // 이 종류가 "단일 스탯 감소"로 나타나는지. false면 스탯 역추적으로는 알 수 없는 종류다
    // (스턴·독·점화·출혈은 스탯을 건드리지 않고, 탈진은 여러 스탯을 깎아 하나로 특정할 수 없다 — 장부로만 뜬다).
    private static bool TryStatOf(DebuffType kind, out StatType stat)
    {
        switch (kind)
        {
            case DebuffType.Slow:       stat = StatType.SPD; return true;
            case DebuffType.ATKDown:    stat = StatType.ATK; return true;
            case DebuffType.ASDown:     stat = StatType.AS;  return true;
            case DebuffType.ArmorBreak: stat = StatType.DEF; return true;
            default:                    stat = default;      return false;
        }
    }

    // 장부에 걸려 있는 디버프들이 설명하는 스탯 집합(StatType을 비트로). 역추적 중복 점등을 막는 데만 쓴다.
    // DebuffTracker는 종류가 어떤 스탯을 건드리는지 저장하지 않으므로 여기서 손으로 대응시킨다 —
    // StatDebuffSO 에셋의 실제 구성과 어긋나지 않게, 종류를 늘리면 이 표도 같이 늘려야 한다.
    private static int ExplainedStats(DebuffType tracked)
    {
        int mask = 0;
        if ((tracked & DebuffType.Slow) != 0)       mask |= 1 << (int)StatType.SPD;
        if ((tracked & DebuffType.ATKDown) != 0)    mask |= 1 << (int)StatType.ATK;
        if ((tracked & DebuffType.ASDown) != 0)     mask |= 1 << (int)StatType.AS;
        if ((tracked & DebuffType.ArmorBreak) != 0) mask |= 1 << (int)StatType.DEF;
        if ((tracked & DebuffType.Exhaust) != 0)
            mask |= (1 << (int)StatType.AS) | (1 << (int)StatType.SPD) | (1 << (int)StatType.ATK);
        // 빙결은 공속·이속·방어력을 함께 깎는다 — 등록해두지 않으면 빙결 하나에
        // 둔화·공속감소·방깎 아이콘까지 덩달아 뜬다(탈진과 같은 이유).
        if ((tracked & DebuffType.Frost) != 0)
            mask |= (1 << (int)StatType.AS) | (1 << (int)StatType.SPD) | (1 << (int)StatType.DEF);
        return mask;
    }
}

/// <summary>
/// 디버프 종류 하나와 그때 소환할 아이콘 스프라이트 짝. EnemyBase의 인스펙터에서 프리팹별로 채운다.
/// 여기 등록되고 스프라이트가 들어 있는 종류만 표시된다.
/// </summary>
[System.Serializable]
public struct EnemyDebuffIcon
{
    [Tooltip("이 아이콘이 나타낼 디버프 종류")]
    public DebuffType kind;
    [Tooltip("해당 디버프가 걸린 동안 소환될 아이콘 이미지")]
    public Sprite sprite;
}
