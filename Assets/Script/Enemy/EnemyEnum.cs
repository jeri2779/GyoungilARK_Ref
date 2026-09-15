using UnityEngine;

public enum EnemyType
{
    Melee,
    Ranged,
}
public enum EnemyClass
{
    Normal,
    Elite,
    Boss,
}
// 적 특성 — 서로 조합 가능하므로 비트 플래그. CSV엔 '|' 또는 ';'로 여러 개 표기(예: "Fly|Cloaking").
[System.Flags]
public enum EnemyAttribute
{
    None     = 0,
    Cloaking = 1 << 0, // 은신: 저지당했을 때만 피격 가능
    Fly      = 1 << 1, // 공중: 원거리 영웅만 타격 가능
    UnJudged = 1 << 2, // 무시: 저지 불가(막는 영웅을 통과)
    Berserk  = 1 << 3, // 폭주: 체력50%이하 일경우 이속+공격력 증가
    Regeneration = 1 << 4, // 재생: 체력이 빠르게 참
    HitsShield = 1 << 5, //타수 보호막: 데미지1고정으로 일정 타수로만 피해를받음
    Burrow = 1 << 6, //잠행 : 은신이랑 비슷 사막전용
    Swim = 1 << 7, //수영 : 기믹타일 물 을 지나갈수 있음
    Flame = 1 << 8, //화염족 : 기믹타일 불에 닿으면 데미지를 받지않고 일시적 강해짐
}
// 디버프 종류는 DebuffType(Script/Debuff/DebuffEnum.cs) 하나로 통일했다 — 아이콘도 그걸로 고른다.
// EnemyDebuffKind는 삭제됐다. 종류가 늘면 DebuffEnum에 비트를 추가하면 된다.
