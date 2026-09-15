using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// GroundZoneEffect와 같은 골격(자기 완결형 스폰 프리팹)을 쓰되 Hero owner에 의존하지 않는다.
// 플레이어는 ATK 스탯도, 트레잇도, 영웅별 이펙트 풀도 없으므로 데미지/힐은 장판 자신이 들고 있는
// 고정값을 쓰고, 이펙트는 풀링 없이 단순 Instantiate/Destroy로 처리한다. 오라(owner 생존 동안 무한
// 지속)도 지원하지 않는다 — duration은 항상 양수여야 한다.
[DisallowMultipleComponent]
public class PlayerGroundZoneEffect : MonoBehaviour
{
    public GroundZoneMode mode = GroundZoneMode.Damage;
    public RangeShape shape = RangeShape.Diamond;
    public int radius = 1;
    public float tickInterval = 1f;
    [Tooltip("mode==Damage: 틱당 고정 데미지 (플레이어는 ATK 스탯이 없어 고정값 사용)")]
    public float damageAmount = 0f;
    [Tooltip("mode==Heal: 틱당 고정 힐량 (범위 내 최저 체력 아군 1명)")]
    public float healAmount = 0f;
    [Tooltip("mode==Heal: 대상 최대 체력 비례 추가 힐량")]
    public float hpHealPer = 0f;
    [Tooltip("반드시 양수. 플레이어 장판은 오라(무한 지속)를 지원하지 않는다.")]
    public float duration = 3f;
    public List<TargetDebuffRef> targetDebuffs = new();
    public GameObject hitEffect;
    public float hitEffectLifetime = 0.5f;
    [Tooltip("mode==Heal: 범위 안에 머무는 동안 아군 발밑에 붙어 유지되는 이펙트. null이면 안 스폰.")]
    public GameObject healReceiveEffect;
    [Tooltip("mode==Buff: 버프 받는 아군 발밑에 붙어 범위 안에 머무는 동안 유지되는 이펙트. null이면 안 스폰.")]
    public GameObject buffReceiveEffect;
    [Tooltip("이 프리팹의 파티클/데칼이 기본 크기(localScale=1)로 나타내는 반경(타일 수). radius/이값만큼 자기 자신을 스케일한다. 0이면 스케일하지 않음.")]
    public float visualRadius = 0f;
    [Tooltip("장판이 살아있는 동안 자기 위치에 한 번 스폰해 유지하는 이펙트(범위 표시용). null이면 안 스폰.")]
    public GameObject selfEffect;
    [Tooltip("selfEffect가 기본 크기(localScale=1)로 나타내는 반경(타일 수). radius/이값만큼 스케일한다. 0이면 스케일하지 않음.")]
    public float selfEffectVisualRadius = 0f;
    [Tooltip("mode==Buff: 범위 안 아군에게 적용할 버프 목록. duration/maxStacks는 무시된다 — 범위에 머무는 동안 유지되고 벗어나면 즉시 제거된다.")]
    public List<BuffEffect> allyBuffs = new();

    [Header("사운드")]
    [Tooltip("장판 생성 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    public string spawnSoundKey;
    [Tooltip("장판이 살아있는 동안 계속 루프 재생할 EnemySoundManager 키. 소멸 시 정지. 비워두면 재생하지 않음")]
    public string sustainSoundKey;
    [Tooltip("장판이 대상에게 데미지/힐을 적용할 때 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    public string hitSoundKey;

    private MapBoard board;
    private BuffManager buffManager;
    private Action<GameObject> release;
    private CancellationTokenSource cts;
    private GameObject selfEffectInstance;
    private readonly HashSet<Hero> buffedAllies = new();
    private readonly Dictionary<Hero, GameObject> buffEffectInstances = new();
    private readonly HashSet<Hero> healPresentAllies = new();
    private readonly Dictionary<Hero, GameObject> healEffectInstances = new();
    private GameManager gameManager;
    private AudioSource sustainVoice;

    // 스폰 직후 호출한다. release가 null이면 만료 시 스스로 Destroy된다(풀링 없음).
    public void Init(MapBoard board, BuffManager buffManager, Action<GameObject> release = null, GameManager gameManager = null)
    {
        this.board = board;
        this.buffManager = buffManager;
        this.release = release;
        this.gameManager = gameManager;
        if (this.gameManager != null) this.gameManager.ChangeToDay += ForceEnd;
        ApplyVisualScale();
        SpawnSelfEffect();
        if (!string.IsNullOrEmpty(spawnSoundKey)) EnemySoundManager.Play(spawnSoundKey, at: transform.position);
        if (!string.IsNullOrEmpty(sustainSoundKey)) sustainVoice = EnemySoundManager.PlayLoop(sustainSoundKey);
        if (duration > 0f) FitParticlesToDuration(duration);
    }

    private void OnEnable()
    {
        cts = new CancellationTokenSource();
        RunLifetime(cts.Token).Forget();
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.ChangeToDay -= ForceEnd;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    // 밤이 끝나면(ChangeToDay) 남은 duration과 무관하게 즉시 만료 처리한다.
    private void ForceEnd() => cts?.Cancel();

    private async UniTask RunLifetime(CancellationToken token)
    {
        try
        {
            float elapsed = 0f, tickTimer = 0f;
            while (!token.IsCancellationRequested && elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                tickTimer += dt;
                if (tickTimer >= tickInterval) { tickTimer = 0f; Tick(); }
                await UniTask.Yield(token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // sustainVoice는 EnemySoundManager 쪽 오브젝트에 속한 별개의 AudioSource라, this(장판
            // 자신)가 이미 파괴된 상태여도 안전하게 정지할 수 있다.
            if (sustainVoice != null) { sustainVoice.Stop(); sustainVoice = null; }

            if (mode == GroundZoneMode.Buff)
                ClearAllyBuffs();
            else if (mode == GroundZoneMode.Heal)
                ClearHealEffects();
            DespawnSelfEffect();

            if (this != null)
            {
                if (release != null) release.Invoke(gameObject);
                else Destroy(gameObject);
            }
        }
    }

    // 장판이 어떤 경로로 끝나든(자연 만료/ForceEnd 강제 종료) 로직과 시각 이펙트를 같은 타이밍에 정리한다.
    private void DespawnSelfEffect()
    {
        if (selfEffectInstance == null) return;
        Destroy(selfEffectInstance);
        selfEffectInstance = null;
    }

    // Buff 모드 장판이 사라질 때(정상 만료/파괴) 아직 범위 안에 있던 아군의 버프도 정리한다.
    private void ClearAllyBuffs()
    {
        if (buffedAllies.Count == 0) return;
        foreach (Hero ally in buffedAllies)
        {
            if (ally == null) continue;
            foreach (BuffEffect effect in allyBuffs)
                buffManager.RemoveBuff(ally, effect.statType, this);
            DespawnAllyBuffEffect(ally);
        }
        buffedAllies.Clear();
    }

    private void SpawnAllyBuffEffect(Hero ally)
    {
        if (buffReceiveEffect == null) return;
        GameObject fx = Instantiate(buffReceiveEffect, ally.transform.position, buffReceiveEffect.transform.rotation, ally.transform);
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);
        buffEffectInstances[ally] = fx;
    }

    private void DespawnAllyBuffEffect(Hero ally)
    {
        if (!buffEffectInstances.Remove(ally, out GameObject fx)) return;
        if (fx != null) Destroy(fx);
    }

    private void SpawnHealPresenceEffect(Hero ally)
    {
        if (healReceiveEffect == null) return;
        GameObject fx = Instantiate(healReceiveEffect, ally.transform.position, healReceiveEffect.transform.rotation, ally.transform);
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);
        healEffectInstances[ally] = fx;
    }

    private void DespawnHealPresenceEffect(Hero ally)
    {
        if (!healEffectInstances.Remove(ally, out GameObject fx)) return;
        if (fx != null) Destroy(fx);
    }

    private void ClearHealEffects()
    {
        if (healPresentAllies.Count == 0) return;
        foreach (Hero ally in healPresentAllies)
            DespawnHealPresenceEffect(ally);
        healPresentAllies.Clear();
    }

    private void UpdateHealPresence(List<GameObject> alliesInRange)
    {
        var inRange = new HashSet<Hero>();
        foreach (GameObject go in alliesInRange)
            if (go.GetComponentInParent<Hero>() is Hero ally)
                inRange.Add(ally);

        foreach (Hero ally in inRange)
            if (healPresentAllies.Add(ally))
                SpawnHealPresenceEffect(ally);

        healPresentAllies.RemoveWhere(ally =>
        {
            if (ally != null && inRange.Contains(ally)) return false;
            if (ally != null) DespawnHealPresenceEffect(ally);
            return true;
        });
    }

    private void Tick()
    {
        if (board == null) return;

        if (mode == GroundZoneMode.Heal)
        {
            List<GameObject> alliesInRange = QueryAllies();
            UpdateHealPresence(alliesInRange);

            if (healAmount <= 0f && hpHealPer <= 0f) return;
            bool healedAny = false;
            foreach (GameObject go in alliesInRange)
            {
                if (go.GetComponentInParent<Hero>() is not Hero ally) continue;
                float heal = healAmount + ally.SC[StatType.HP] * hpHealPer;
                if (heal <= 0f) continue;
                ally.Heal(heal);
                healedAny = true;
            }
            if (healedAny)
            {
                if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, at: transform.position);
                SpawnHitEffect(transform.position);
            }
            return;
        }

        if (mode == GroundZoneMode.Buff)
        {
            var inRange = new HashSet<Hero>();
            foreach (GameObject go in QueryAllies())
                if (go.GetComponentInParent<Hero>() is Hero ally)
                    inRange.Add(ally);

            foreach (Hero ally in inRange)
            {
                if (!buffedAllies.Add(ally)) continue;
                foreach (BuffEffect effect in allyBuffs)
                    buffManager.ApplyStackingModifier(ally, effect.statType, effect.modifierType, effect.value, 0f, effect.maxStacks, this);
                SpawnAllyBuffEffect(ally);
            }

            buffedAllies.RemoveWhere(ally =>
            {
                if (ally != null && inRange.Contains(ally)) return false;
                if (ally != null)
                {
                    foreach (BuffEffect effect in allyBuffs)
                        buffManager.RemoveBuff(ally, effect.statType, this);
                    DespawnAllyBuffEffect(ally);
                }
                return true;
            });
            return;
        }

        int dmg = Mathf.RoundToInt(damageAmount);
        // 장판(지상)은 공중 적을 절대 때릴 수 없다 — GroundZoneEffect와 동일한 불변식.
        foreach (GameObject go in QueryEnemies(EnemyAttribute.Fly))
        {
            if (dmg > 0 && go.GetComponentInParent<IDamageAble>() is IDamageAble d)
            {
                d.TakeDamage(dmg);
                if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, at: go.transform.position);
                SpawnHitEffect(go.transform.position);
            }

            AttackDamageUtil.ApplyTargetDebuffs(go.transform, targetDebuffs, buffManager, this);
        }
    }

    // Hero.GetObjectsInRange(RangeQueryAffinity.Ally)와 동일한 로직 — 타일당 1개 점유자 모델을 훑는다.
    private List<GameObject> QueryAllies()
    {
        var found = new List<GameObject>();
        Vector2Int originCell = board.WorldToCell(transform.position);
        foreach (Tile tile in TileShapeQuery.GetTiles(board, originCell, radius, shape))
        {
            GameObject occupant = tile.OccupantObject;
            if (occupant != null && tile.OccupantHero is Hero ally && !ally.IsDead)
                found.Add(occupant);
        }
        return found;
    }

    // Hero.GetObjectsInRange(RangeQueryAffinity.Enemy)와 동일한 로직.
    private List<GameObject> QueryEnemies(EnemyAttribute areaUnattackableMask)
    {
        var found = new List<GameObject>();
        var seen = new HashSet<GameObject>();
        Vector2Int originCell = board.WorldToCell(transform.position);
        foreach (Tile tile in TileShapeQuery.GetTiles(board, originCell, radius, shape))
            foreach (GameObject enemy in tile.Enemies)
            {
                if (enemy == null || !seen.Add(enemy)) continue;
                if (PassesAreaMask(enemy, areaUnattackableMask))
                    found.Add(enemy);
            }
        return found;
    }

    private static bool PassesAreaMask(GameObject enemy, EnemyAttribute mask)
    {
        if (mask == EnemyAttribute.None) return true;
        var eb = enemy.GetComponent<EnemyBase>();
        return eb == null || (eb.Attribute & mask) == 0;
    }

    private void SpawnHitEffect(Vector3 pos)
    {
        if (hitEffect == null) return;
        GameObject go = Instantiate(hitEffect, pos, hitEffect.transform.localRotation);
        if (hitEffectLifetime > 0f) Destroy(go, hitEffectLifetime);
    }

    private void SpawnSelfEffect()
    {
        if (selfEffect == null) return;
        selfEffectInstance = Instantiate(selfEffect, transform.position, selfEffect.transform.rotation);
        // 파티클 프리팹은 프로젝트 관례상 Play On Awake가 꺼져 있다 — Hero.SpawnPersistentEffect와
        // 마찬가지로 직접 Play()를 걸어줘야 실제로 재생된다(안 그러면 스폰만 되고 안 보인다).
        foreach (ParticleSystem ps in selfEffectInstance.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);
        if (selfEffectVisualRadius > 0f)
            selfEffectInstance.transform.localScale = Vector3.one * (radius / selfEffectVisualRadius);
    }

    private void ApplyVisualScale()
    {
        if (visualRadius <= 0f) return;
        transform.localScale = Vector3.one * (radius / visualRadius);
    }

    private void FitParticlesToDuration(float targetDuration)
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            // hitEffect가 장판의 자식으로 박혀있는 프리팹이 있어, 이 스캔이 장판 자신의 연출용
            // 자식뿐 아니라 hitEffect까지 잘못 건드려 재생 속도를 깎을 수 있다. hitEffect(및 그
            // 하위)는 부모가 손댈 수 없도록 애초에 순회 대상에서 제외한다.
            if (hitEffect != null && ps.transform.IsChildOf(hitEffect.transform)) continue;
            ParticleSystem.MainModule main = ps.main;
            if (main.loop || main.duration <= 0f) continue;
            main.simulationSpeed = main.duration / targetDuration;
        }
    }
}
