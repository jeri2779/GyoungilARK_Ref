/// <summary>
/// 디버프 장부를 들고 있는 대상(적/영웅 공용). IStunAble과 같은 취지 — 호출자는 이 계약만 안다.
///
/// EnemyBase가 구현한다. Hero는 아직 구현하지 않으므로(팀원 소유 파일) 영웅에게 디버프를 걸면
/// 효과는 들어가되 장부에는 안 남는다 — DebuffSO가 null 장부를 건너뛴다.
/// Hero에 이 두 줄이 생기는 날 SO는 한 줄도 안 고치고 영웅 쪽 조회가 켜진다.
/// </summary>
public interface IDebuffCarrier
{
    DebuffTracker Debuffs { get; }

    /// <summary>걸리지 않는 디버프 종류들(OR 조합). 없으면 DebuffType.None.</summary>
    DebuffType ImmuneDebuffs { get; }

    /// <summary>
    /// ImmuneDebuffs로 막힌 디버프를 대상에게 알린다. "막았다는 사실 자체가 효과"인 특성에 쓴다 —
    /// 화염족(Flame)이 점화를 튕겨내는 대신 그 지속시간만큼 재생을 얻는 식.
    /// duration은 오버라이드가 적용된 최종값, 즉 막히지 않았다면 걸렸을 시간이다.
    ///
    /// 기본 구현이 비어 있으므로 새 구현체(Hero 등)는 이 멤버를 몰라도 된다 —
    /// 위 두 줄만 구현하면 여전히 장부·면역이 켜진다.
    /// </summary>
    void OnDebuffBlocked(DebuffType type, float duration) { }
}
