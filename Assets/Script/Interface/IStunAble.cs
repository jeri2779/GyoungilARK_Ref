// 스턴을 걸 수 있는 대상(적/영웅 공용). 호출자가 필요한 것만 계약으로 두고,
// 만료 시각·애니 처리 같은 상태/연출은 각 구현체가 알아서 한다.
public interface IStunAble
{
    bool IsStunned { get; }

    // duration초간 스턴. 이미 걸린 스턴보다 짧으면 무시(중첩 시 최댓값)하는 게 권장 동작.
    void Stun(float duration);
}
