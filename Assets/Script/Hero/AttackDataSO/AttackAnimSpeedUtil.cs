using UnityEngine;

// 공격 애니메이션 재생속도를 공격 간격(1/AS)에 맞춰 스케일하기 위한 헬퍼.
// clipLength(AttackDataSO에 설정된 기본 클립 길이)가 간격보다 길 때만 압축해
// "모션이 간격보다 길어서 잘리는 문제"를 없앤다. 간격보다 짧을 때는 배속하지 않고
// 자연 속도(1)로 재생한다 — 억지로 늘려 슬로우모션처럼 보이는 문제를 피하기 위함.
public static class AttackAnimSpeedUtil
{
    // 데드라인은 어디까지나 "Recovery 이벤트가 안 올 때"의 안전장치이므로 넉넉히 잡는다.
    // 애니메이터 전환(transition) 지연 등으로 이벤트가 조금 늦게 와도 타격이 버려지지 않도록.
    private const float WindowSlack = 0.25f;

    // clipLength가 간격보다 길 때만 압축(고AS). 그 외엔 자연 속도(1) — 느려지지 않는다.
    public static float ComputeScale(AttackDataSO data, float interval)
        => data.clipLength > interval && interval > 0f ? data.clipLength / interval : 1f;

    // 타격 이벤트 윈도우가 열려 있어야 할 최대 시간. 실제 재생 길이(간격과 clipLength 중 더 긴 쪽)에
    // 여유(WindowSlack)를 더해, 정상 속도로 재생될 때 이벤트가 데드라인에 잘려 누락되지 않게 한다.
    public static float ComputeWindowDuration(AttackDataSO data, float interval)
        => Mathf.Max(interval, data.clipLength) + WindowSlack;

    public static void SetSpeed(Animator anim, float scale)
    {
        if (anim != null) anim.speed = scale;
    }
}
