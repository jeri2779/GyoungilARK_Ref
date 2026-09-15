using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class GameManager : MonoBehaviour
{
    private FSM fsm = new();

    private IState day;
    private IState night;
    private IState result;
    private IState gameover;

    private bool canBuild = true;
    private UiManager uiManager;
    public UiManager UiManager => uiManager;
    private SpawnerManager waveSpawner;
    private UpgradeState upgradeState;
    [SerializeField] private int dayCount; // Construct()에서 튜토리얼 진행 여부에 따라 -1 또는 0으로 초기화
    [Tooltip("에디터에서 타이틀을 거치지 않고 MainScene을 바로 실행해 디버깅할 때 체크 - " +
        "Construct()가 dayCount를 튜토리얼 진행 여부로 덮어쓰지 않고 위 인스펙터 값을 그대로 쓴다. " +
        "DayState.Enter()가 진입하며 1 증가시키니, 원하는 날짜보다 1 작게 넣어야 한다.")]
    [SerializeField] private bool debugKeepInspectorDayCount;
    [SerializeField] private int hp = 20;
    private int initialHp; // 0일차 튜토리얼 리셋용 스냅샷
    [SerializeField] private List<BaseUpgradeData> hpUpgrades;
    private bool requestSupport = false;
    public int DayCount => dayCount;
    public bool CanBuild => canBuild;
    public bool RequestSupport => requestSupport;
    private bool canSpawnEnemy = false;
    public bool CanSpawnEnemy => canSpawnEnemy;

    public event Action ChangeToDay;
    public event Action ChangeToNight;
    public event Action ExpandMap;

    [SerializeField] private HeroType initialUnlockedHero = HeroType.SwordMan | HeroType.Archer;
    private byte unlockedHero;
    public byte UnlockHero => unlockedHero;
    private byte unlockedEnemy = 0b000111;
    public bool isGameOver = false;
    public event Action HpChanged;
    public int Hp => hp;
    public int todayHp;
    public bool perfactDefence = false;

    private string gameSeed = Guid.NewGuid().ToString("N");
    private int heroDrawMeleeCount;
    private int heroDrawRangedCount;
    private int[] heroCombineMeleeCounts = new int[3];   // 인덱스 0=1→2티어, 1=2→3티어, 2=3→4티어
    private int[] heroCombineRangedCounts = new int[3];
    private int heroesCreatedToday; // 오늘 생성한 영웅 수 (근접+원거리 합산) - 영웅 생성 가격 점증에 사용, OnDay에서 초기화

    public string GameSeed => gameSeed;
    public int HeroDrawMeleeCount => heroDrawMeleeCount;
    public int HeroDrawRangedCount => heroDrawRangedCount;
    public int[] HeroCombineMeleeCounts => heroCombineMeleeCounts;
    public int[] HeroCombineRangedCounts => heroCombineRangedCounts;
    public int HeroesCreatedToday => heroesCreatedToday;

    [Inject]
    private void Construct(UiManager uiManager, SpawnerManager waveSpawner, UpgradeState upgradeState, TutorialState tutorialState)
    {
        this.uiManager = uiManager;
        this.waveSpawner = waveSpawner;
        this.upgradeState = upgradeState;
        unlockedHero = (byte)initialUnlockedHero;
        uiManager.UnlockedEnemy = unlockedEnemy;
        uiManager.UnlockedHero = unlockedHero;

        hp += (int)upgradeState.GetTotalEffect(hpUpgrades);
        initialHp = hp;

        // 튜토리얼을 이번 세션에서 처음 보는 거면 0일차(연습)부터, 이미 본 적 있으면 0일차 없이 곧장 1일차부터.
#if UNITY_EDITOR
        if (!debugKeepInspectorDayCount)
        {
            dayCount = tutorialState.Seen ? 0 : -1;
        }
#else
        dayCount = tutorialState.Seen ? 0 : -1;
#endif

        day = new DayState(this);
        night = new NightState(this);
        result = new ResultState(this, uiManager);
        gameover = new GameOverState(this, upgradeState, UiManager);
        waveSpawner.AllRegionsClear += OnResult;
        fsm.ChangeState(day);
        uiManager.UnlockChanged += UpdateUnlock;
    }

    private void OnDestroy()
    {
        waveSpawner.AllRegionsClear -= OnResult;
        uiManager.UnlockChanged -= UpdateUnlock;
    }

    public void OnNight()
    {
        if (isGameOver)
            return;

        fsm.ChangeState(night);
        ChangeToNight?.Invoke();
    }

    public void OnDay()
    {
        if (isGameOver)
            return;

        fsm.ChangeState(day);
        heroesCreatedToday = 0; // 하루가 바뀌면 영웅 생성 가격 점증을 초기화 (SaveManager의 DayStart 저장보다 먼저)
        ChangeToDay?.Invoke();
    }

    public void OnResult()
    {
        if (isGameOver)
            return;

        fsm.ChangeState(result);
    }

    public void ChangeCanBuild(bool value)
    {
        canBuild = value;
    }

    public void SpawnEnemy()
    {
        waveSpawner.SpawnWave(DayCount);
    }

    public void IncreaseDayCount()
    {
        dayCount++;
    }

    // 테스트용 - TutorialManager.DebugRestart()가 0일차 흐름을 다시 재현할 때 쓴다. dayCount를
    // 안 맞춰주면 이미 진행된 실제 날짜의 웨이브가 나가고, 밤이 끝난 뒤 0일차 리셋 타이밍도
    // 어긋난다 - 지금 낮을 "0일차"로 다시 취급하도록 되돌린다.
    public void ResetDayCountForTutorialReplay()
    {
        dayCount = 0;
    }

    public void ChangeRequest(bool value)
    {
        requestSupport = value;
    }

    public void ExpandMapForce()
    {
        ExpandMap?.Invoke();
    }

    public void ChangeCanSpawnEnemy(bool value)
    {
        canSpawnEnemy = value;
    }

    public void HpDamage(EnemyClass enemyclass)
    {
        switch (enemyclass)
        {
            case EnemyClass.Normal:
                hp--;
                break;
            case EnemyClass.Elite:
                hp -= 2;
                break;
            case EnemyClass.Boss:
                hp = 0;
                break;
        }
        HpChanged?.Invoke();
        if (hp <= 0)
        {
            hp = 0;
            fsm.ChangeState(gameover);
        }
    }

    public void UpdateUnlock()
    {
        unlockedHero = uiManager.UnlockedHero;
        unlockedEnemy = uiManager.UnlockedEnemy;
    }
    public void RestoreDayCount(int amount)
    {
        // 타이틀 없이 MainScene을 바로 실행해 디버깅할 때(debugKeepInspectorDayCount)는 세이브
        // 로드(LoadManager)로도 dayCount를 덮어쓰지 않는다 - 안 그러면 Construct()에서 지켜낸
        // 인스펙터 값을 로드가 곧바로 다시 덮어써버린다.
#if UNITY_EDITOR
        if (debugKeepInspectorDayCount) return;
#endif
        dayCount = amount;
    }

    // 세이브 데이터로 기지 체력을 그대로 덮어쓴다 (로드 복원 전용)
    public void RestoreHp(int amount)
    {
        hp = amount;
        HpChanged?.Invoke();
    }

    // 세이브 데이터로 오늘 아침 기준 체력을 그대로 덮어쓴다 (로드 복원 전용)
    public void RestoreTodayHp(int amount)
    {
        todayHp = amount;
    }

    // 세이브 데이터로 해금된 영웅 목록을 그대로 덮어쓴다 (로드 복원 전용)
    public void RestoreUnlockedHero(byte value)
    {
        unlockedHero = value;
        uiManager.UnlockedHero = value;
    }

    // 세이브 데이터로 시드를 그대로 덮어쓴다 (로드 복원 전용) - 비어있으면(패치 이전 세이브) 새로 만든다.
    public void RestoreGameSeed(string seed)
    {
        gameSeed = string.IsNullOrEmpty(seed) ? Guid.NewGuid().ToString("N") : seed;
    }

    // 세이브 데이터로 영웅 뽑기 순번(근접/원거리)을 그대로 덮어쓴다 (로드 복원 전용) -
    // 뽑기 직전 상태로 되돌아가도 같은 순번을 다시 소비하게 되어 결과가 항상 같아진다.
    public void RestoreHeroDrawCounts(int meleeCount, int rangedCount)
    {
        heroDrawMeleeCount = meleeCount;
        heroDrawRangedCount = rangedCount;
    }

    // 영웅 생성에 성공할 때마다 HeroSetPanel이 호출한다 - 가격 점증에 쓰는 오늘의 생성 횟수를 늘린다.
    public void AddHeroesCreatedToday(int count)
    {
        heroesCreatedToday += count;
    }

    public void RestoreHeroesCreatedToday(int count)
    {
        heroesCreatedToday = count;
    }

    // 영웅을 뽑을 때마다 호출한다 - 종류에 맞는 순번을 하나 늘리고 (시드, 순번)을 반환한다.
    public (string seed, int count) ConsumeHeroDraw(OccupantKind kind)
    {
        if (kind == OccupantKind.RangedHero)
        {
            heroDrawRangedCount++;
            return (gameSeed, heroDrawRangedCount);
        }

        heroDrawMeleeCount++;
        return (gameSeed, heroDrawMeleeCount);
    }

    // 세이브 데이터로 영웅 합성 순번(근접/원거리 × 티어)을 그대로 덮어쓴다 (로드 복원 전용)
    public void RestoreHeroCombineCounts(int[] meleeCounts, int[] rangedCounts)
    {
        heroCombineMeleeCounts = meleeCounts ?? new int[3];
        heroCombineRangedCounts = rangedCounts ?? new int[3];
    }

    // 영웅을 합성할 때마다 호출한다 - 종류+티어에 맞는 순번을 하나 늘리고 (시드, 순번)을 반환한다.
    public (string seed, int count) ConsumeHeroCombine(OccupantKind kind, int tier)
    {
        int[] counts = kind == OccupantKind.RangedHero ? heroCombineRangedCounts : heroCombineMeleeCounts;
        int index = Mathf.Clamp(tier - 1, 0, counts.Length - 1);
        counts[index]++;
        return (gameSeed, counts[index]);
    }

    public void ResetHpToFull()
    {
        hp = initialHp;
        HpChanged?.Invoke();
    }
}
