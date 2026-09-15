using UnityEngine;

/// <summary>
/// 디버프 한 종류의 정의. "무엇을 하는가"만 담고 지속시간 관리는 하지 않는다 —
/// 시간은 BuffManager(스탯)·DotRegistry(지속피해)·DebuffTracker(조회) 중 하나가 이미 굴린다.
///
/// Apply가 유일한 입구다. 게임플레이 코드가 BuffManager와 DebuffTracker를 따로 부르면
/// 같은 디버프에 만료 시각이 둘 생겨 어긋나므로, 반드시 이 SO를 통해서만 건다.
/// </summary>
public abstract class DebuffSO : ScriptableObject
{
    [Tooltip("이 디버프의 종류. 장부 조회·아이콘·면역 판정이 이 값으로 이뤄진다.")]
    public DebuffType type = DebuffType.Slow;

    [Tooltip("지속시간(초). 실제 효과와 장부가 같은 이 값을 쓴다.")]
    [Min(0f)] public float duration = 3f;

    public void Apply(in DebuffContext ctx) => Apply(ctx, 0f, 1f);

    /// <summary>
    /// 에셋 값을 호출부에서 조정해 건다. 같은 디버프를 세기만 달리 쓸 때 에셋을 여러 개 만들지 않아도 된다
    /// (에셋을 나누면 BuffManager의 Source가 달라져 서로 독립 중첩되므로 maxStacks가 무의미해진다).
    ///
    /// durationOverride: 0 이하면 에셋의 duration을 쓴다.
    /// scale: 효과 크기 배율. 스탯 감소는 value에, 지속 피해는 틱 피해에 곱한다. 스턴은 무시(지속시간이 세기다).
    /// </summary>
    public void Apply(in DebuffContext ctx, float durationOverride, float scale = 1f)
    {
        float dur = durationOverride > 0f ? durationOverride : duration;
        if (dur <= 0f || type == DebuffType.None)
        {
            DebuffDebug.Log($"{name} 무시 — duration={dur}, type={type}", this);
            return;
        }
        if (scale <= 0f)
        {
            DebuffDebug.Log($"{name} 무시 — scale={scale} (0 이하)", this);
            return;
        }
        // 면역이면 효과도 장부도 건너뛴다 — 여기서 막으면 세 입구(ApplyDebuff/ApplyDebuffTo/DebuffApply.To)가 다 덮인다.
        // 단 IStunAble.Stun을 직접 부르는 경로는 이 SO를 지나지 않으므로 구현체(EnemyBase.Stun)에서 따로 막는다.
        if ((type & ctx.immuneMask) != 0)
        {
            DebuffDebug.Log($"{name}({type}) 면역 — 대상={(ctx.targetObject != null ? ctx.targetObject.name : "?")}", ctx.targetObject);
            // 막았다는 사실을 대상에게 알린다 — 면역이 곧 트리거인 특성이 있다(화염족: 점화를 튕겨내며 재생을 얻음).
            // 장부에는 남기지 않는다. 안 걸린 디버프가 아이콘으로 뜨면 "보이는 것 == 걸린 것"이 깨진다.
            ctx.carrier?.OnDebuffBlocked(type, dur);
            return;
        }

        // 효과가 실제로 들어간 뒤에만 장부에 남긴다 — 통로가 없는 대상(영웅 스턴 등)에
        // 안 걸린 디버프가 아이콘으로 뜨는 것을 막는다.
        // 장부에도 효과와 같은 dur을 넘겨 시계가 갈리지 않게 한다.
        if (!OnApply(ctx, dur, scale))
        {
            DebuffDebug.Log($"{name}({type}) 안 걸림 — {GetType().Name}이 대상에서 통로를 못 찾았다" +
                $" (unit={(ctx.unit != null ? "O" : "X")}, damageable={(ctx.damageable != null ? "O" : "X")}," +
                $" stunnable={(ctx.stunnable != null ? "O" : "X")}, buffManager={(ctx.buffManager != null ? "O" : "X")})", this);
            return;
        }

        ctx.ledger?.Apply(type, dur);
        // 장부가 없는 대상 — 지금은 영웅 — 은 이펙트를 그려 줄 곳이 없다.
        // 적은 EnemyDebuffEffects가 장부를 보고 그리지만, 영웅엔 장부도 그 컴포넌트도 없다(Hero.cs는 팀원 소유).
        // 그래서 여기서 DebuffEffectView에 직접 알려 준다. 지속 피해는 DotRegistry가 자기 장부로
        // 이미 밀어 주므로 제외한다 — 둘 다 기록하면 시계가 둘이 되어, 대상이 죽어 DoT가 일찍 끊겨도
        // 이 쪽 만료 시각까지 이펙트가 남는다.
        if (ctx.ledger == null && !DrivesOwnEffectView) DebuffEffectView.Track(ctx.host, type, dur, ctx.source);

        DebuffDebug.Log($"{name}({type}) 걸림 — {dur}초, scale={scale:F2}, 대상={(ctx.targetObject != null ? ctx.targetObject.name : "?")}," +
            $" 장부={(ctx.ledger != null ? "기록됨" : "없음")}", ctx.targetObject);
    }

    /// <summary>
    /// 이 파생형이 장부 없는 대상의 이펙트 표시를 스스로 굴리는가.
    /// 지속 피해(DotDebuffSO)만 true — DotRegistry가 만료·사망·낮 전환까지 챙기며 매 프레임 DebuffEffectView에 밀어 준다.
    /// </summary>
    protected virtual bool DrivesOwnEffectView => false;    

    /// <summary>
    /// 실제 효과를 건다. 대상이 이 디버프를 받을 통로가 없으면 false.
    /// duration은 오버라이드가 적용된 최종값이므로 <see cref="duration"/> 필드를 직접 읽지 말 것.
    /// </summary>
    protected abstract bool OnApply(in DebuffContext ctx, float duration, float scale);

    /// <summary>
    /// 이 SO가 지원하는 종류들. 여러 개면 OR로 묶는다.
    /// public인 이유 — DebuffTableImporter가 CSV의 Type이 이 파생형에서 유효한지 검사하는 데 쓴다.
    /// (임포터가 필드를 코드로 넣으면 OnValidate가 안 불리므로 임포터 쪽에서 직접 걸러야 한다.)
    /// </summary>
    public abstract DebuffType AllowedTypes { get; }

    // 지원하지 않는 종류로 설정된 에셋을 인스펙터에서 바로 잡는다.
    // 방치하면 스턴 에셋이 장부에 Slow를 기록하고, DotRegistry는 종류로 엔트리를 구분하므로
    // 같은 종류로 둔 DoT 에셋 둘이 한 칸을 공유해 하나가 조용히 안 걸린다.
    protected virtual void OnValidate()
    {
        int bits = (int)type;
        bool single = bits > 0 && (bits & (bits - 1)) == 0;   // 단일 비트인가
        if (single && (type & AllowedTypes) != 0) return;

        int allowed = (int)AllowedTypes;
        type = (DebuffType)(allowed & -allowed);              // 지원 목록의 첫 종류로 되돌린다
        Debug.LogWarning($"[{name}] {GetType().Name}이(가) 지원하지 않는 디버프 종류였다 — {type}로 되돌렸다.", this);
    }
}
