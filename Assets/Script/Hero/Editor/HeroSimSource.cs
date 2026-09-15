using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 시뮬레이터가 쓰는 에셋을 읽어오는 창구. 읽기만 하고 어떤 에셋·씬·런타임 상태도 건드리지 않는다.
public static class HeroSimSource
{
    private const string HeroDataFilter = "t:HeroData";
    private const string TierConfigFilter = "t:HeroUpgradeConfig";
    private const string ClassConfigFilter = "t:HeroClassUpgradeConfig";
    private const string TitleUpgradeFilter = "t:BaseUpgradeData";
    private const string TitleStatFolder = "Assets/GameLoop(ChoiJinWoo)/Prefabs/ScriptableObject/Title/Hero/Stat";
    private const int RangedHeroType = 1;
    private const int MinimumHitPeriod = 1;

    // 프로젝트의 모든 HeroData를 읽어 티어·이름 순으로 돌려준다.
    public static List<HeroSimEntry> CollectEntries()
    {
        var entries = new List<HeroSimEntry>();
        string[] guids = AssetDatabase.FindAssets(HeroDataFilter);

        // 영웅 에셋 개수는 저작에 따라 늘고 주는 동적 컬렉션이라 한 번 순회한다.
        foreach (string guid in guids)
        {
            HeroData heroData = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
            if (heroData == null) continue;
            if (heroData.HeroPrefab == null) continue;

            Hero prefabHero = heroData.HeroPrefab.GetComponent<Hero>();
            if (prefabHero == null) continue;
            if (prefabHero.StatData == null) continue;

            entries.Add(BuildEntry(heroData, prefabHero));
        }

        entries.Sort(CompareEntry);
        return entries;
    }

    // 티어 강화 설정 에셋을 찾아 돌려준다.
    public static HeroUpgradeConfig LoadTierConfig() => LoadSingle<HeroUpgradeConfig>(TierConfigFilter);

    // 직업 강화 설정 에셋을 찾아 돌려준다.
    public static HeroClassUpgradeConfig LoadClassConfig() => LoadSingle<HeroClassUpgradeConfig>(ClassConfigFilter);

    // 타이틀 강화 Hero/Stat 갈래 에셋을 해금 순서대로 읽어 돌려준다.
    public static List<BaseUpgradeData> LoadTitleStatUpgrades()
    {
        var upgrades = new List<BaseUpgradeData>();
        string[] guids = AssetDatabase.FindAssets(TitleUpgradeFilter, new[] { TitleStatFolder });

        // 타이틀 강화 칸도 저작에 따라 개수가 바뀌는 동적 컬렉션이다.
        foreach (string guid in guids)
        {
            BaseUpgradeData upgrade = AssetDatabase.LoadAssetAtPath<BaseUpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (upgrade == null) continue;
            upgrades.Add(upgrade);
        }

        upgrades.Sort(CompareUpgradeName);
        return upgrades;
    }

    // HeroData 하나와 그 프리팹에서 시뮬레이터가 쓸 값을 모두 뽑아낸다.
    private static HeroSimEntry BuildEntry(HeroData heroData, Hero prefabHero)
    {
        var entry = new HeroSimEntry
        {
            HeroData = heroData,
            StatData = prefabHero.StatData,
        };

        CollectBasePattern(entry, prefabHero);
        CollectTraits(entry, prefabHero);
        return entry;
    }

    // 프리팹의 평타 목록을 그대로 옮겨 담는다.
    private static void CollectBasePattern(HeroSimEntry entry, Hero prefabHero)
    {
        // 평타 로테이션 길이는 영웅마다 다른 동적 컬렉션이다.
        foreach (AttackDataSO attack in prefabHero.BasePattern)
        {
            if (attack == null) continue;
            entry.BasePattern.Add(attack);
        }
    }

    // 프리팹에 붙은 트레잇을 확률형과 미반영형으로 갈라 담는다.
    private static void CollectTraits(HeroSimEntry entry, Hero prefabHero)
    {
        HeroTrait[] traits = prefabHero.GetComponents<HeroTrait>();
        for (int index = 0; index < traits.Length; index++)
        {
            AddTrait(entry, traits[index]);
        }
    }

    // 트레잇 한 개를 종류에 맞는 자리에 넣는다.
    private static void AddTrait(HeroSimEntry entry, HeroTrait trait)
    {
        if (trait is ChanceHeavyTrait chanceHeavy)
        {
            entry.HeavyChances.Add(new HeroSimHeavyChance
            {
                HeavyAttack = chanceHeavy.heavyAttackData,
                SwingRatio = chanceHeavy.chance,
            });
            return;
        }

        if (trait is NthHitHeavyTrait nthHitHeavy)
        {
            entry.HeavyChances.Add(new HeroSimHeavyChance
            {
                HeavyAttack = nthHitHeavy.heavyAttackData,
                SwingRatio = 1f / Mathf.Max(MinimumHitPeriod, nthHitHeavy.everyN),
            });
            return;
        }

        if (trait is HeavyRechanceTrait heavyRechance)
        {
            entry.RechanceAttack = heavyRechance.heavyAttackData;
            entry.RechanceChance = heavyRechance.chance;
            entry.RechanceRecursive = heavyRechance.allowRecursive;
            return;
        }

        entry.UnmodeledTraitNames.Add(trait.GetType().Name);
    }

    // 지정한 타입의 설정 에셋 하나를 찾아 돌려준다.
    private static T LoadSingle<T>(string filter) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets(filter);
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // 표에서 보기 좋도록 티어 → 근접/원거리 → 이름 순으로 정렬한다.
    private static int CompareEntry(HeroSimEntry left, HeroSimEntry right)
    {
        int byTier = left.HeroData.Tier.CompareTo(right.HeroData.Tier);
        if (byTier != 0) return byTier;

        int byType = left.HeroData.HeroType.CompareTo(right.HeroData.HeroType);
        if (byType != 0) return byType;

        return string.CompareOrdinal(left.HeroData.HeroName, right.HeroData.HeroName);
    }

    // 타이틀 강화 칸을 Stat1~Stat5 순서로 맞춘다.
    private static int CompareUpgradeName(BaseUpgradeData left, BaseUpgradeData right)
        => string.CompareOrdinal(left.name, right.name);

    // 원거리 영웅인지 알려준다.
    public static bool IsRangedHero(HeroData heroData) => heroData.HeroType == RangedHeroType;
}
