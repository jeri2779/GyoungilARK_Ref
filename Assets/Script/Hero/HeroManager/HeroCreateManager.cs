using System;
using System.Collections.Generic;
using UnityEngine;

// 근접/원거리 생성 버튼이 호출한다. 해금된 지역 수만큼 진행도 행(row)을 고르고,
// 그 행의 티어별 가중치로 티어를 뽑은 뒤, 그 티어+종류의 HeroData 중 하나를 균등하게 고른다.
// 뽑힌 티어에 아직 등록된 HeroData가 없으면(예: 4티어 데이터 미비) 한 티어씩 낮춰 재시도한다.
// 뽑기 자체는 GameManager가 슬롯마다 들고 있는 고정 시드+순번(ConsumeHeroDraw)으로만 진행한다 -
// 뽑기 전 상태로 로드해서 다시 뽑아도 같은 순번을 다시 소비하니 결과가 항상 같아, 재추첨 시도가 무의미해진다.
public class HeroCreateManager : MonoBehaviour
{
    [SerializeField] private HeroRegistry heroRegistry;
    [SerializeField] private MapGame game;
    [Tooltip("영웅 생성 성공 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    [SerializeField] private string createSoundKey;

    [Serializable]
    public class TierWeightRow
    {
        public List<int> tierWeights = new(); // tierWeights[i] = (i+1)티어 가중치
    }

    // 인덱스 = "게임 시작 이후 새로 열린 지역 수"(0부터). 시작부터 열려있는 지역은 안 센다.
    // 리스트보다 지역을 더 열면 마지막 행 유지.
    [SerializeField]
    private List<TierWeightRow> probabilityByStage = new()
    {
        new TierWeightRow { tierWeights = new List<int> { 99, 1, 0, 0 } },
        new TierWeightRow { tierWeights = new List<int> { 90, 9, 1, 0 } },
        new TierWeightRow { tierWeights = new List<int> { 80, 15, 5, 0 } },
        new TierWeightRow { tierWeights = new List<int> { 70, 20, 9, 1 } },
    };

    private int extraUnlockedRegions;
    public int ExtraUnlockedRegions => extraUnlockedRegions; // HeroSetPanel의 지역 기반 가격 점증에 사용

    // MapRegistry.Awake가 먼저 끝나야 AllModules를 읽을 수 있어서 Start에서 구독한다(HeroRegistry가
    // game.HeroRoster.Changed를 Start에서 구독하는 것과 같은 이유).
    private void Start()
    {
        if (MapRegistry.Instance == null) return;
        foreach (ModuleLogic module in MapRegistry.Instance.AllModules.Values)
            module.OnStateChanged += OnModuleStateChanged;
    }

    private void OnDestroy()
    {
        if (MapRegistry.Instance == null) return;
        foreach (ModuleLogic module in MapRegistry.Instance.AllModules.Values)
            module.OnStateChanged -= OnModuleStateChanged;
    }

    // 시작부터 열려있는 지역은 구독 전이라 안 잡히고, 그 이후 새로 열리는 지역만 카운트된다.
    private void OnModuleStateChanged(ModuleState state)
    {
        if (state == ModuleState.Preparing) extraUnlockedRegions++;
    }

    public bool TryRollHero(OccupantKind kind, out HeroData result)
    {
        result = null;
        if (probabilityByStage.Count == 0) return false;

        int stageIndex = Mathf.Clamp(extraUnlockedRegions, 0, probabilityByStage.Count - 1);
        List<int> weights = probabilityByStage[stageIndex].tierWeights;

        if (weights.Count == 0) return false;

        (string seed, int count) = game.Rule.ConsumeHeroDraw(kind);
        System.Random rng = new System.Random(GameSeeding.Derive(seed, count));

        int tier = WeightedPickTier(weights, rng);
        while (tier >= 1)
        {
            if (heroRegistry.TryGetHeroDatas(tier, kind, out List<HeroData> candidates))
            {
                result = candidates[rng.Next(candidates.Count)];
                if (!string.IsNullOrEmpty(createSoundKey)) EnemySoundManager.Play(createSoundKey);
                return true;
            }
            tier--; // 그 티어 데이터가 아직 없으면 한 단계 낮은 티어로 대신 채운다.
        }

        return false;
    }

    private static int WeightedPickTier(List<int> weights, System.Random rng)
    {
        int total = 0;
        foreach (int w in weights) total += Mathf.Max(0, w);
        if (total <= 0) return 1;

        int roll = rng.Next(total);
        int cumulative = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += Mathf.Max(0, weights[i]);
            if (roll < cumulative) return i + 1;
        }
        return weights.Count;
    }
}
