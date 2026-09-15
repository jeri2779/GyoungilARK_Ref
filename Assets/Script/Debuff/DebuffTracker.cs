using UnityEngine;

/// <summary>
/// 한 유닛에 걸린 디버프 장부. "무엇이 걸렸는가(DebuffType)"와 "얼마나 남았는가(초)"를 같이 들고 있다.
/// EnemyBase가 소유하고 쓴다(EnemyDebuffBar/EnemyCloak과 같은 구조 — MonoBehaviour가 아니다).
///
/// 만료 시각(Time.time + duration)을 저장하고 조회할 때마다 현재 시각과 비교한다.
/// EnemyBase의 _shieldExpiry와 같은 방식이며, 그래서 <b>Tick을 부를 필요가 없다</b>.
/// 스턴 만료 시각도 이 장부가 들고 있다(_stunExpiry는 여기로 흡수됐다).
/// 매 프레임 남은 시간을 깎는 구조로 만들면 Tick 한 번을 빼먹은 곳에서 디버프가 영구히 남아버린다.
/// Time.time은 timeScale의 영향을 받으므로 일시정지 중엔 남은 시간도 멈춘다(스턴과 동일한 거동).
///
/// 중요 — 이 장부는 <see cref="Apply"/>로 등록된 디버프만 안다.
/// 지금 영웅이 적에게 거는 둔화 등은 BuffManager를 지나 StatContainer에 Modifier로 꽂히고
/// 이쪽을 거치지 않으므로, 눈에 보이게 둔화된 적을 두고도 Has(Slow)는 false다.
/// (EnemyDebuffBar가 스탯 감소를 역추적하는 임시 구현을 쓰는 것과 같은 이유 — BuffManager에 조회 API가 없다.)
/// 디버프를 거는 쪽에서 Apply를 같이 호출하도록 바꿔야 UI/판정을 이 장부로 옮길 수 있다.
///
/// 스택(둔화 2중첩 등)은 다루지 않는다. 같은 종류가 다시 들어오면 "남은 시간이 더 긴 쪽"만 유지한다.
/// </summary>
public class DebuffTracker
{
    // DebuffEnum의 비트 하나가 슬롯 하나. 현재 최상위는 Exhaust(1 << 9)이므로 16칸이면 넉넉하다.
    // enum에 1 << 16 이상을 추가하게 되면 이 값만 올리면 된다(SlotOf가 범위를 벗어난 비트를 걸러낸다).
    private const int SlotCount = 16;

    private readonly float[] _expiry = new float[SlotCount];     // 만료 시각(Time.time 기준). 0 = 안 걸림
    private readonly float[] _duration = new float[SlotCount];   // 그 만료를 만든 총 지속시간 — 게이지 비율 계산용

    /// <summary>
    /// 현재 걸려 있는 디버프 전부를 합친 비트마스크. 안 걸렸으면 DebuffType.None.
    /// 캐시하지 않고 매번 만료 시각을 다시 본다 — 같은 프레임에 Apply가 끼어들어도 항상 최신값이다.
    /// </summary>
    public DebuffType ActiveMask
    {
        get
        {
            float now = Time.time;
            int mask = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (now < _expiry[i]) mask |= 1 << i;
            }
            return (DebuffType)mask;
        }
    }

    // 종류 하나 조회. 슬롯을 바로 짚어 O(1) — IsStunned처럼 매 프레임 여러 번 불리는 곳은 이걸 쓴다.
    public bool Has(DebuffType type)
    {
        int slot = SlotOf(type);
        return slot >= 0 && Time.time < _expiry[slot];
    }

    public bool HasAny(DebuffType mask)
    {
        int bits = (int)mask;
        if (bits == 0) return false;

        float now = Time.time;
        for (int i = 0; i < SlotCount; i++)
        {
            if ((bits & (1 << i)) != 0 && now < _expiry[i]) return true;
        }
        return false;
    }

    public bool HasAll(DebuffType mask)
    {
        int bits = (int)mask;
        if (bits == 0) return false;

        float now = Time.time;
        for (int i = 0; i < SlotCount; i++)
        {
            if ((bits & (1 << i)) != 0 && now >= _expiry[i]) return false;
        }
        return true;
    }


    public void Apply(DebuffType type, float duration)
    {
        if (duration <= 0f) return;

        int slot = SlotOf(type);
        if (slot < 0) return;  

        float expiry = Time.time + duration;

        if (expiry > _expiry[slot])
        {
            _expiry[slot] = expiry;
            _duration[slot] = duration;
        }
    }

    /// <summary>
    /// 남은 시간(초). 안 걸렸거나 이미 만료됐으면 0.
    /// <b>종류 하나만</b> 넘길 것 — 여러 비트를 묶은 마스크는 0을 돌려준다("안 걸림"과 구분되지 않으니
    /// 마스크로 물어볼 땐 <see cref="GetRemainingMax"/>를 쓴다).
    /// </summary>
    public float GetRemaining(DebuffType type)
    {
        int slot = SlotOf(type);
        if (slot < 0) return 0f;

        return Mathf.Max(0f, _expiry[slot] - Time.time);
    }

    /// <summary>mask에 든 종류들 중 가장 오래 남은 시간(초). 전부 안 걸렸으면 0.</summary>
    public float GetRemainingMax(DebuffType mask)
    {
        int bits = (int)mask;
        if (bits == 0) return 0f;

        float now = Time.time;
        float longest = 0f;
        for (int i = 0; i < SlotCount; i++)
        {
            if ((bits & (1 << i)) == 0) continue;
            float left = _expiry[i] - now;
            if (left > longest) longest = left;
        }
        return longest;
    }
    public float GetRemainingNormalized(DebuffType type)
    {
        int slot = SlotOf(type);
        if (slot < 0 || _duration[slot] <= 0f) return 0f;

        return Mathf.Clamp01((_expiry[slot] - Time.time) / _duration[slot]);
    }

    /// <summary>type을 즉시 해제(해제 스킬·면역 부여 등). 안 걸려 있으면 아무 일도 없다.</summary>
    public void Remove(DebuffType type)
    {
        int slot = SlotOf(type);
        if (slot < 0) return;

        _expiry[slot] = 0f;
        _duration[slot] = 0f;
    }

    public void Clear()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            _expiry[i] = 0f;
            _duration[i] = 0f;
        }
    }

    // 단일 비트인 DebuffType을 배열 인덱스로. 종류가 아닌 값은 -1.
    // None(0)과 여러 비트를 묶은 마스크(Slow|Stun 등)를 여기서 걸러내므로 호출부가 배열을 잘못 짚을 일이 없다.
    private static int SlotOf(DebuffType type)
    {
        int bits = (int)type;
        if (bits <= 0) return -1;
        if ((bits & (bits - 1)) != 0) return -1;   // 비트가 2개 이상 = 단일 종류가 아니다

        int slot = 0;
        while ((bits >>= 1) != 0) slot++;
        return slot < SlotCount ? slot : -1;
    }
}
