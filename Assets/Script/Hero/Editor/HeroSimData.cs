using System.Collections.Generic;
using UnityEngine;

// 영웅 한 명분 원본 데이터 묶음. 에셋에서 읽은 값만 담고 계산은 하지 않는다.
public class HeroSimEntry
{
    public HeroData HeroData;
    public StatDataSO StatData;
    public List<AttackDataSO> BasePattern = new();

    // 확률형 트레잇이 만들어내는 강공들. 프리팹에 붙은 컴포넌트 순서 그대로 담는다.
    public List<HeroSimHeavyChance> HeavyChances = new();

    // 강공 재발동 트레잇(HeavyRechanceTrait) 정보. 없으면 RechanceAttack이 비어 있다.
    public AttackDataSO RechanceAttack;
    public float RechanceChance;
    public bool RechanceRecursive;

    // 계산에 넣지 않은 트레잇(스탠스 교체·스택 버프 등) 이름 목록.
    public List<string> UnmodeledTraitNames = new();
}

// 확률형 트레잇 하나가 만들어내는 강공 하나. SwingRatio는 전체 스윙 대비 비율이다.
public class HeroSimHeavyChance
{
    public AttackDataSO HeavyAttack;
    public float SwingRatio;
}

// 사용자가 창에서 고른 가상 강화 조건. 레벨은 내부 강화 횟수(0부터)로 보관한다.
public class HeroSimInput
{
    public int TierUpgradeCount;
    public int ClassUpgradeCount;
    public int TitleUnlockCount;
}

// 표 한 줄에 출력할 계산 결과.
public class HeroSimResult
{
    public Sprite Icon;
    public string HeroName;
    public int Tier;

    public float MaxHp;
    public float AttackPower;
    public float Defence;
    public float AttackSpeed;
    public float BlockCount;

    // 강화 0단계(원본 StatDataSO) 대비 지금 스탯이 몇 % 늘었는지 표기용 문구.
    public string HpGrowth;
    public string AttackGrowth;
    public string DefenceGrowth;
    public string AttackSpeedGrowth;

    public float NormalHitDamage;
    public float AverageHitDamage;
    public float DamagePerSecond;

    public int CumulativeCost;
    public string TraitNote;

    // 상대 적을 골랐을 때만 값이 붙는 처치 시간(초). 안 골랐으면 "-".
    public string TimeToKill;
}
