
public enum TerrainType
{
    Core,   // 본진 영역. 방어 대상이며 일반 배치 불가
    Ground, // 지상. 적 경로·근접 영웅·생산 건물의 후보 타일
    High,   // 고지. 적 이동을 막는 벽 성격, 원거리 영웅 배치
    Empty,  // 장식/미사용 빈 타일(외곽 경계 포함)
    Special // 특수 타일. 이벤트/보상/장식용. 배치 불가
}
public enum PassType
{
    Walk, // 걸어서 지난다(기본)
    Swim  // 헤엄쳐야 지난다
}

public enum GimmickType
{
    None, // 아무 일도 없는 평범한 칸(기본)
    Fire, // 올라선 유닛이 지속피해를 받는다
    Campfire, // 추위 디버프를 막는 고정 모닥불 타일
    Windwall // 언덕 위 바위 가림막. 바람 불어가는 쪽 N칸을 막는다

    //차후 추가될 효과를 갱신한다.

}

/// <summary>기존 씬 데이터 호환용 값. 새 코드는 TileState의 명시 필드를 사용한다.</summary>
public enum TileFlags
{
    None              = 0,
    EnemyLane         = 1,
    MeleePlaceable    = 2,
    RangedPlaceable   = 4,
    BuildingPlaceable = 8
    //방식 변경 고려
}
