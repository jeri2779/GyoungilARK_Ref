using System.Collections.Generic;

public class ActiveBuff
{
    public IUnit Target;
    public StatType StatType;
    public List<Modifier> Modifiers = new();
    public int Stacks;
    public float RemainingTime;
    // duration<=0으로 생성됨 — 장판형 버프처럼 시간이 아니라 외부(범위 진입/이탈)가 생존을 관리한다.
    // Tick()의 자동 만료 대상에서 제외되고, BuffManager.RemoveBuff로만 끝난다.
    public bool Persistent;
    public object Source;
}
