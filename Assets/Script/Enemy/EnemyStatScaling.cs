using UnityEngine;

/// <summary>
/// 적 스탯의 라운드·지역 배율 계산. 실제로 스폰되는 값(EnemyBase.ApplyData)과
/// 스테이지 정보 툴팁에 미리 보여주는 값(StageInfoView)이 반드시 같아야 하므로 한 곳에 모았다.
/// 여기 표나 식을 고치면 양쪽 다 따라온다 — 예전처럼 한쪽만 바뀌어 툴팁이 거짓말하는 일이 없다.
/// </summary>
public static class EnemyStatScaling
{
    /// 일차·해금 지역 수를 반영한 스탯. AttackSpeed·MoveSpeed는 배율이 없어 여기 없다.
    public readonly struct Stats
    {
        public readonly float Hp;
        public readonly float Attack;
        public readonly float Defense;

        public Stats(float hp, float attack, float defense)
        {
            Hp = hp;
            Attack = attack;
            Defense = defense;
        }
    }

    /// 체력은 매일, 공격력·방어력은 5일마다 한 단계 오른다(정수 나눗셈이라 1~4일차는 0단계).
    /// 체력·방어력에는 해금 지역 수 배율을 곱한다 — 지역을 더 열수록 같은 몹도 계단식으로 단단해진다.
    public static Stats Compute(EnemyTable.Data data, EnemyClass cls, int dayCount)
    {
        int fiveDayStep = dayCount / 5;
        return new Stats(
            (data.Health + (dayCount * data.UpHealthScale)) * RegionHpScale(cls,dayCount),
            (data.Attack + (data.UpAttackScale * fiveDayStep)) * RegionAttackScale(cls,dayCount),
            (data.Defense + (data.UpDefenseScale * fiveDayStep)) * RegionDefenseScale(cls,dayCount));
    }

    /// CSV 원본 Class 문자열(EnemyTable.Data.Class)을 그대로 넘기는 쪽.
    public static Stats Compute(EnemyTable.Data data, string cls, int dayCount)
        => Compute(data, ParseClass(cls), dayCount);

    /// 대소문자·공백을 무시하고 파싱한다. EnemyBase가 Class를 만드는 방식과 같아야
    /// CSV에 "boss"라고 적혀도 실제 적과 툴팁이 같은 배율을 쓴다.
    public static EnemyClass ParseClass(string raw)
        => !string.IsNullOrEmpty(raw) && System.Enum.TryParse(raw.Trim(), true, out EnemyClass c)
            ? c
            : EnemyClass.Normal;

    // 해금된 지역 수별 배율(인덱스 = 해금 수). 계산식이 아니라 디자이너가 정한 곡선이라 표로 둔다.
    // 게임 시작 상태가 이미 1개 해금이므로 0·1 칸은 1배로 두고 2개째 해금부터 배율이 붙는다.
    // 표 길이를 넘는 해금 수는 마지막 값으로 고정되므로, 지역이 늘면 칸을 추가하면 된다.
    // 보스 전용 표를 따로 두고, Elite·Normal은 잡몹 표를 공유한다.
    private static readonly float[] RegionHpScaleTable = { 1f, 1f, 1.25f, 1.6f, 2.15f, 3f, 4f };
    private static readonly float[] RegionBossHpScaleTable = { 1f, 1f, 1.5f, 2.5f, 4.2f, 7.2f, 10f };
    private static readonly float[] RegionDefenseScaleTable = { 1f, 1f, 1.07f, 1.15f, 1.25f, 1.38f, 1.5f };
    private static readonly float[] RegionBossDefenseScaleTable = { 1f, 1f, 1.2f, 1.35f, 1.5f, 1.72f, 2f };
    private static readonly float[] RegionAttackScaleTable =  { 1f,1f,1f,1f,1f,1f,1f};
    private static readonly float[] RegionBossAttackScaleTable =  { 0.7f,0.7f,0.9f,1f,1.3f,1.5f,1.7f};

    // 전 지역 해금이 끝나면 위 표가 마지막 칸에서 멈춘다 — 그 뒤로는 판이 더 길어져도 보스가 그 자리에 선다.
    // 그래서 해금이 다 끝난 뒤부터 BossStatStepRounds 라운드마다 배율을 한 단계씩 더해 계속 오르게 한다.
    // 기준점은 "전 지역 해금을 확인한 라운드"라 해금 직후엔 +0에서 시작한다(끊기지 않게).
    // 보스만 적용한다 — 잡몹은 DayCount × UpHealthScale로 이미 매 라운드 오르고 있다.
    //
    // 한 단계에 얼마나 오를지는 BossStatDeepenDay를 경계로 두 구간이다: [0]=그 전, [1]=그 뒤.
    // 이미 쌓인 단계에 새 계수를 소급해서 곱하지 않는다 — 경계 전에 오른 만큼은 [0]으로 굳고
    // 경계 뒤에 오르는 단계만 [1]로 붙는다. 그래서 경계일에 배율이 튀지 않고 기울기만 가팔라진다.
    private const int BossStatStepRounds = 10;
    private const int BossStatDeepenDay = 100;
    private static readonly float[] BossHpStepScale = { 2.5f, 6f};
    private static readonly float[] BossDefStepScale = { 0.5f, 1.5f};
    private static readonly float[] BossAttackStepScale = {0.2f,0.6f};
    public static float RegionHpScale(EnemyClass cls,int dayCount)
        => RegionScale(RegionHpScaleTable, RegionBossHpScaleTable, cls) + BossFullUnlockHpBonus(cls,dayCount);

    public static float RegionDefenseScale(EnemyClass cls,int dayCount)
        => RegionScale(RegionDefenseScaleTable, RegionBossDefenseScaleTable, cls) + BossFullUnlockDefBonus(cls,dayCount);

    public static float RegionAttackScale(EnemyClass cls,int dayCount)
        => RegionScale(RegionAttackScaleTable,RegionBossAttackScaleTable,cls) + BossFullUnlockAtkBonus(cls,dayCount);

    // 표 길이는 자기 표 기준으로 클램프한다(표마다 칸 수가 달라져도 안전하게).
    private static float RegionScale(float[] normalTable, float[] bossTable, EnemyClass cls)
    {
        float[] table = cls == EnemyClass.Boss ? bossTable : normalTable;
        int unlocked = SpawnerManager.UnlockedRegionCount;
        if (unlocked <= 0) return table[0];
        return table[Mathf.Min(unlocked, table.Length - 1)];
    }

    private static float BossFullUnlockHpBonus(EnemyClass cls, int dayCount)
        => BossFullUnlockBonus(cls, dayCount, BossHpStepScale);

    private static float BossFullUnlockDefBonus(EnemyClass cls, int dayCount)
        => BossFullUnlockBonus(cls, dayCount, BossDefStepScale);
    private static float BossFullUnlockAtkBonus(EnemyClass cls, int dayCount)
        => BossFullUnlockBonus(cls,dayCount,BossAttackStepScale);
    // 해금 뒤 쌓인 단계를 BossStatDeepenDay 경계로 갈라, 구간마다 제 계수로 따로 더한다.
    // dayCount는 두 호출부 모두 전역 일차를 넘긴다(EnemyBase는 GameManager.DayCount,
    // StageInfoView는 SetDay로 받은 CurrentDay) — RoundsSinceFullUnlock이 세는 시계와 같다.
    private static float BossFullUnlockBonus(EnemyClass cls, int dayCount, float[] stepScale)
    {
        if (cls != EnemyClass.Boss) return 0f;

        int rounds = SpawnerManager.RoundsSinceFullUnlock;
        int steps = rounds / BossStatStepRounds;
        if (steps <= 0) return 0f;

        // 전 지역 해금이 확인된 일차. RoundsSinceFullUnlock이 그 뒤로 흐른 라운드 수다.
        int fullUnlockDay = dayCount - rounds;
        // 경계 전까지 해금 상태로 지난 라운드 → 그중 몇 단계가 [0] 계수로 굳었는지.
        // 경계 뒤에 해금이 끝났으면 0(전부 [1]), 아직 경계 전이면 rounds 전체(전부 [0]).
        int earlyRounds = Mathf.Clamp(BossStatDeepenDay - fullUnlockDay, 0, rounds);
        int earlySteps = earlyRounds / BossStatStepRounds;
        // 나머지를 빼서 구하면 두 몫의 합이 항상 steps다 — 경계에서 단계가 새거나 겹치지 않는다.
        int lateSteps = steps - earlySteps;

        return earlySteps * stepScale[0] + lateSteps * stepScale[1];
    }
}
