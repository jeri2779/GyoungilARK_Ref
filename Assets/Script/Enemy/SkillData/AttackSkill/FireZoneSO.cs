using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 주변 영웅을 태우는 화염 오라. DamageZoneSO와 같은 구조(배경 오라 — 죽을 때까지 지속되므로
/// 일반 공격 루프를 막지 않는다)지만 세 가지가 다르다.
///
/// 1) 피해량·틱 간격을 Ignite 디버프 에셋에서 읽어온다 — 불 칸 점화와 수치가 항상 같이 움직인다.
/// 2) 디버프를 걸지 않고 즉시 피해를 넣는다 — 아이콘도, 칸을 벗어난 뒤 남는 화상도 없다.
/// 3) 영웅만 때린다. 건물·자원은 별도 배치물이라 피해를 받지 않고, 적(아군)도 영향받지 않는다.
///
/// Map은 공개 읽기 API(WorldToCell/GetTiles, Tile.State.Occupant/OccupantObject)만 본다 — 수정하지 않는다.
///
/// 이 스킬은 화염족(EnemyAttribute.Flame)이 특성으로 갖는다 — EnemyBase.LoadStats가
/// FlameAuraSkillId로 자동으로 붙이므로 CSV Skills 칸에 적지 않아도 된다.
/// 화염족이 아닌 적에게 주고 싶을 때만 Skills 칸에 직접 적는다.
/// </summary>
public class FireZoneSO : AttackSkillDataSO
{
    // 배경 오라 — 죽을 때까지 지속되므로 일반 공격 루프를 막지 않는다(안 그러면 평생 공격 불가).
    public override bool BlocksBasicAttack => false;

    public GameObject fireZoneEffectPrefab;

    [Header("이펙트 크기")]
    [Tooltip("range가 1일 때 이펙트에 줄 스케일. 눈으로 보면서 맞추면 된다. " +
             "range가 늘어나면 여기에 비례해 커진다. 0이면 크기를 안 건드린다(프리팹 그대로).")]
    [SerializeField] private float scaleAtRange1 = 0f;

    [Header("발동 조건")]
    [Tooltip("켜면 화염족이 불에 닿아 강해진 동안(EnemyBase.FlameEmpowered)에만 오라가 나온다 — " +
             "불을 밟는 순간 켜지고, 벗어나 점화 창이 끝나면 꺼진다. " +
             "끄면 스폰부터 죽을 때까지 계속 나온다(화염족이 아닌 일반 오라 몹에 쓴다).")]
    [SerializeField] private bool onlyWhileFlameEmpowered = true;

    [Header("수치 출처")]
    [Tooltip("피해량·틱 간격을 가져올 지속피해 디버프 에셋 ID(Resources/DebuffSO 아래). " +
             "불 칸 점화와 같은 수치를 쓰려고 여기서 읽는다 — 한쪽만 고쳐서 어긋나는 것을 막는다. " +
             "못 찾으면 아래 fallbackPercentPerTick / tickInterval 값으로 되돌아간다.")]
    [SerializeField] private string damageSourceDebuffId = "Ignite_Basic";

    [Tooltip("위 디버프 에셋을 못 찾았을 때 쓸 틱 피해 비율(대상 최대 체력의 %, 1 = 1%). " +
             "AttackSkillDataSO의 damage는 고정 피해값이라 여기 쓰지 않는다 — " +
             "그대로 비율로 읽으면 damage=30이 한 틱에 최대 체력 30%가 되어 버린다.")]
    [SerializeField, Min(0.01f)] private float fallbackPercentPerTick = 1f;

    private bool loggedScale;
    private bool loggedNumbers;

    /// <summary>
    /// Ignite 에셋에서 틱 피해 비율·간격을 읽는다. 못 찾으면 이 SO에 저작된 값으로 되돌아간다.
    /// 반환하는 것은 고정 피해값이 아니라 <b>대상 최대 체력의 %</b>다 — 점화(DotDebuffSO)와 단위를 맞춘다.
    /// 실제 피해값은 대상마다 다르므로 때리는 자리에서 DotRegistry.PercentDamage로 환산한다.
    /// </summary>
    private void ResolveNumbers(out float percentPerTick, out float interval, out float atkPercent)
    {
        percentPerTick = fallbackPercentPerTick;
        interval = tickInterval;
        atkPercent = 0f;

        var dot = DebuffLoader.Get(damageSourceDebuffId) as DotDebuffSO;
        if (dot != null)
        {
            percentPerTick = dot.percentPerTick;
            interval = dot.interval;
            // 공격력 몫도 같이 가져온다 — 점화에 이 칸을 적었는데 오라만 빠지면
            // "불 칸과 오라의 체감이 같다"는 이 메서드의 전제가 조용히 깨진다.
            atkPercent = dot.atkPercent;
        }
        else if (!string.IsNullOrEmpty(damageSourceDebuffId))
        {
            Debug.LogWarning($"FireZoneSO({name}): '{damageSourceDebuffId}'를 DotDebuffSO로 못 읽었다 " +
                             $"— 이 SO의 fallbackPercentPerTick={fallbackPercentPerTick}%, tickInterval={tickInterval}로 진행한다.", this);
        }

        // DotRegistry와 같은 하한. 0이면 매 프레임 때려서 순삭이 된다.
        interval = Mathf.Max(0.05f, interval);
        // 공격력 몫이 있으면 최대 체력 비율은 0이어도 된다 — 0.01%를 억지로 끼워 넣으면
        // "비율 0 + 공격력 비례" 저작이 불 칸에서만 조용히 달라진다.
        if (atkPercent <= 0f) percentPerTick = Mathf.Max(0.01f, percentPerTick);
        else percentPerTick = Mathf.Max(0f, percentPerTick);

        if (!loggedNumbers)
        {
            loggedNumbers = true;
            // Debug.Log($"FireZoneSO({name}): 틱 피해 대상 최대체력 {percentPerTick:F2}% + 시전자 공격력 {atkPercent:F2}%" +
            //           $" / {interval}초 간격 (출처 {damageSourceDebuffId}), " +
            //           $"발동={(onlyWhileFlameEmpowered ? "화염족이 불에 닿은 동안만" : "항상")}");
        }
    }

    // 이펙트를 range(타일 수)에 비례해 키운다.
    // 파티클 이펙트의 '실제로 보이는 반지름'은 코드로 재봐야 못 맞춘다
    // (텍스처의 투명 여백, Size over Lifetime 커브, 서브 이미터가 다 섞인다).
    // 그래서 추정하지 않고, range 1에서의 스케일을 인스펙터에서 눈으로 맞추게 한다.
    private void FitEffectToRange(GameObject effect, int cellRange)
    {
        if (effect == null || scaleAtRange1 <= 0f) return;   // 0이면 프리팹 원래 크기 유지

        float factor = scaleAtRange1 * cellRange;
        // 프리팹이 비균일 스케일로 만들어졌을 수 있으니 비율을 유지한 채 곱한다.
        effect.transform.localScale = fireZoneEffectPrefab.transform.localScale * factor;

        if (!loggedScale)
        {
            loggedScale = true;
            // Debug.Log($"FireZoneSO({name}): range {cellRange} → localScale {factor:0.###}");
        }
    }

    // 이 칸의 점유물이 영웅인가. 건물·자원은 화염 오라를 안 맞는다(별도 배치물).
    private static bool IsHeroOccupant(Tile tile)
    {
        return tile.State.Occupant is OccupantKind.MeleeHero or OccupantKind.RangedHero;
    }

    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.IsDead) return;

        ResolveNumbers(out float percentPerTick, out float interval, out float atkPercent);

        // 이 SO는 Resources.Load로 모든 적이 공유하는 애셋이다(EnemyStatLoader.ResolveSkills).
        // 이펙트 핸들을 필드에 두면 나중에 시전한 적이 앞선 적의 핸들을 덮어써서
        // 남의 살아있는 이펙트를 Despawn하고 자기 것은 풀에 못 돌려주는 누수가 난다. 반드시 지역 변수로.
        GameObject go = null;

        // 큰 영웅은 여러 칸을 함께 점유하므로 같은 OccupantObject가 여러 번 잡힌다 —
        // 그대로 때리면 칸 수만큼 피해가 곱해지므로 한 틱에 한 번만 맞게 걸러낸다.
        var hit = new HashSet<GameObject>();

        try
        {
            int cellRange = range > 0f ? Mathf.RoundToInt(range) : 1;
            float t = 0f;
            EnemySoundManager.Play("DebuffIgnite", at: owner.transform.position);
            while (!owner.IsDead)
            {
                // 오라가 지금 켜져 있어야 하는가. 화염족은 불에 닿아 강해진 동안만(창은 1초마다 갱신되고
                // 불을 벗어나면 점화 지속시간만큼 남았다가 꺼진다), 아니면 죽을 때까지 계속.
                bool on = !onlyWhileFlameEmpowered || owner.FlameEmpowered;

                if (on && go == null && fireZoneEffectPrefab != null)
                {
                    // Spawn은 넘긴 회전을 '월드 회전'으로 덮어쓴다(PoolManager의 SetPositionAndRotation).
                    // Quaternion.identity를 넘기면 프리팹에 구워둔 눕힌 회전이 지워져 장판이 세워진다.
                    go = PoolManager.Instance.Spawn(
                        fireZoneEffectPrefab, owner.transform.position,
                        fireZoneEffectPrefab.transform.rotation, owner.transform);
                    FitEffectToRange(go, cellRange);
                }
                else if (!on && go != null)
                {
                    PoolManager.Instance.Despawn(go);   // 창이 닫히면 이펙트를 풀에 반납하고 다시 켜질 때 새로 꺼낸다
                    go = null;
                    t = 0f;                            // 다음 창은 처음부터 — 꺼진 동안 쌓인 시간이 넘어오지 않게
                }

                if (!on)
                {
                    await UniTask.Yield(token);
                    continue;
                }

                // Time.deltaTime이라 GameSpeedUI의 배속·일시정지를 그대로 따라간다(DotRegistry와 같은 기준).
                t += Time.deltaTime;
                if (t > interval)
                {
                    if (owner.Board == null) return;

                    t = 0f;
                    hit.Clear();
                    Vector2Int origin = owner.Board.WorldToCell(owner.transform.position);

                    foreach (Tile tile in owner.Board.GetTiles(origin, cellRange))
                    {
                        if (tile.OccupantObject == null) continue;
                        if (!IsHeroOccupant(tile)) continue;
                        if (!hit.Add(tile.OccupantObject)) continue;   // 같은 영웅을 두 번 때리지 않는다

                        if (tile.OccupantObject.GetComponentInParent<IDamageAble>() is IDamageAble dmg)
                            // 비율 피해라 피해값이 대상마다 다르다 — 여기서 그 영웅의 최대 체력으로 환산하고,
                            // 시전자 공격력 몫을 더한다. 점화(DotRegistry)와 같은 식이라 불 칸과 오라의 체감이 어긋나지 않는다.
                            // 방어무시(true)도 같은 이유다 — 점화 틱이 방어력을 무시하므로 여기서만 먹히면 둘이 갈라진다.
                            // 공격력은 매 틱 owner에서 다시 읽는다 — 오라는 시전자가 살아 있는 동안만 도는 것이라
                            // 지속 피해와 달리 시전자가 사라진 뒤를 걱정할 필요가 없다.
                            dmg.TakeDamage(DotRegistry.TickDamage(
                                DotRegistry.MaxHp(tile.OccupantObject.GetComponentInParent<IUnit>()),
                                percentPerTick, owner.AttackPower * atkPercent * 0.01f), true);
                    }
                }
                await UniTask.Yield(token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (go != null) PoolManager.Instance.Despawn(go);
        }
    }
}
