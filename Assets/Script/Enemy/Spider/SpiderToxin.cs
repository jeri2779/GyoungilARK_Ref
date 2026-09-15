using UnityEngine;
using VContainer.Unity;

public class SpiderToxin : EnemyBase
{
    public float hp;
    public float speed;
    public int attack;
    public int def; //테스트용 인스펙터 확인용 스탯들

    // 독 수치(틱 비율·간격·지속시간)는 DebuffTable.csv의 Poison_Spider가 들고 있다.
    // 표의 틱 피해는 고정값이 아니라 "맞는 쪽 최대 체력의 %"다 — 이 거미의 공격력과는 무관하다.
    // 아래 scale(StatRatio(ATK))은 "강화된 거미는 독도 세다"만 남긴 배율이다. 기본 상태면 1이라 표 값 그대로 들어가고,
    // 공격력 버프를 받으면 그 비율만큼 %도 커진다(공격력이 곱해지는 게 아니라 %가 곱해진다).
    [SerializeField] private string poisonDebuffId = "Poison_Spider";
    private DebuffSO poisonDebuff;
    private bool poisonResolved;   // 결과가 아니라 "시도했는지"를 기억한다 — 에셋이 없을 때 매 타격마다 경고가 쏟아지지 않게

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    private void Start()
    {
        hp = Hp;
        speed = MoveSpeed;
        attack = AttackPower;
        def = Defense; //테스트용
    }
    public override void EnemySoundAttack()
    {
        base.EnemySoundAttack();
        EnemySoundManager.Play("SpiderAttack", at: transform.position);
    }
    public override void DieSound()
    {
        base.DieSound();
        EnemySoundManager.Play("MiniSpiderDie", at: transform.position);
    }
    // 평타가 적중한 영웅에게 독을 건다. 독 상태는 DotRegistry가 들고 굴리므로
    // 이 거미가 죽거나 풀에 반납돼도 남은 독은 계속 들어간다.
    public override void AnimEvent_AttackHit()
    {
        base.AnimEvent_AttackHit();
        if (IsDead) return;
        GameObject target = FindAttackTarget();
        if (target == null)
        {
            DebuffDebug.Log("SpiderToxin 독 못 걸었다 — 공격 대상을 못 찾음", this);
            return;
        }
        if (target.GetComponentInParent<Hero>() is not Hero hero) // 영웅이 아닌 점유물(건물 등)은 제외
        {
            DebuffDebug.Log($"SpiderToxin 독 못 걸었다 — {target.name}은 영웅이 아니다", this);
            return;
        }

        // 첫 적중에 1회만 해석한다. SO는 Resources 에셋이라 모든 거미가 같은 것을 공유하지만
        // 상태를 담지 않으므로(수치는 걸 때마다 읽는다) 공유해도 안전하다.
        if (!poisonResolved)
        {
            poisonResolved = true;
            poisonDebuff = DebuffLoader.Get(poisonDebuffId);
            DebuffDebug.Log($"SpiderToxin 독 에셋 해석 — '{poisonDebuffId}' → {(poisonDebuff != null ? poisonDebuff.name : "실패(null)")}", this);
        }

        float scale = StatRatio(StatType.ATK);
        DebuffDebug.Log($"SpiderToxin {hero.name}에게 독 시도 — 공격력 {AttackPower}(기본 {BaseStat(StatType.ATK):F0}), scale={scale:F2}", this);
        ApplyDebuffTo(hero, poisonDebuff, scale: scale);
    }
}
