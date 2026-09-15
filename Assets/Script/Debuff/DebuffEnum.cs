using UnityEngine;

// 서로 조합 가능하므로 비트 플래그(EnemyAttribute와 같은 취지).
// [Flags]가 없어도 비트 연산은 되지만, 인스펙터 다중 선택(면역 마스크)과 ToString()의 "Slow, Stun" 표기가 안 된다.
[System.Flags]
public enum DebuffType
{
    None = 0, // 없음
    //능력치 류 감소
    Slow = 1 << 0, // 이동속도 감소
    ATKDown = 1<< 1, // 공격력 감소
    ASDown = 1 << 2, //공격속도 감소
    ArmorBreak = 1 << 3, // 방어력 감소
    //데미지류 디버프
    Poison = 1 << 4, //독
    Ignite = 1 << 5, //점화
    Bleed = 1 << 6, //출혈
    //상태이상 디버프
    Stun = 1 << 7, //기절 (공격 스킬 불가)
    Root = 1 << 8, //속박 (공격 스킬은 가능 이동은 불가 스킬이 이동류 일경우 사용x)
    Exhaust = 1 << 9, //탈진 (공속 이속 데미지 감소)
    Silence = 1 << 10, //침묵 (잠시 스킬 사용불가)
    Frost = 1 << 11, //빙결 (이속 공속 감소)
    SandStom = 1 << 12,// 모래폭풍 (사막 지형으로 인한 데미지)
}
