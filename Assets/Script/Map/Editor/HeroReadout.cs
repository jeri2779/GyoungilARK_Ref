using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// 아군 유닛 정보를 티어→근접/원거리로 모으는 에디터 전용 도구. 표시 전용 — 아무것도 쓰지 않는다.
///
/// HeroData(Assets/HeroData/HeroData)와 StatDataSO(Assets/HeroData/StatDataSO)는 서로 참조가 없다 —
/// HeroTableImporter·HeroStatTableImporter가 같은 HeroName으로 파일명만 맞춰 각자 구워 두므로,
/// 여기서도 그 파일명 규칙으로 짝짓는다.
/// </summary>
public static class HeroReadout
{
    private const string StatFolder = "Assets/HeroData/StatDataSO";

    /// <summary>아군 한 종류와 그 스탯. 스탯을 못 찾으면 null(아직 Import 안 한 경우).</summary>
    public class Entry
    {
        public HeroData Hero;
        public StatDataSO Stat;
    }

    /// <summary>한 티어의 근접/원거리 목록.</summary>
    public class Group
    {
        public int Tier;
        public List<Entry> Melee = new();
        public List<Entry> Ranged = new();
    }

    /// <summary>프로젝트의 모든 HeroData를 티어 오름차순으로 모은다.</summary>
    public static List<Group> Collect()
    {
        var byTier = new Dictionary<int, Group>();

        foreach (string guid in AssetDatabase.FindAssets("t:HeroData"))
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
            if (hero == null)
            {
                continue;
            }

            Group group = GroupOf(byTier, hero.Tier);
            var entry = new Entry { Hero = hero, Stat = FindStat(hero.HeroName) };

            if (hero.HeroType == 1)
            {
                group.Ranged.Add(entry);
            }
            else
            {
                group.Melee.Add(entry);
            }
        }

        var groups = new List<Group>(byTier.Values);
        groups.Sort((a, b) => a.Tier.CompareTo(b.Tier));
        return groups;
    }

    /// <summary>이 HeroData에 해당하는 항목을 찾는다. 고른 것이 없으면 null.</summary>
    public static Entry Find(List<Group> groups, HeroData hero)
    {
        if (hero == null)
        {
            return null;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            Entry found = FindIn(groups[i].Melee, hero) ?? FindIn(groups[i].Ranged, hero);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Entry FindIn(List<Entry> entries, HeroData hero)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Hero == hero)
            {
                return entries[i];
            }
        }

        return null;
    }

    private static Group GroupOf(Dictionary<int, Group> byTier, int tier)
    {
        if (byTier.TryGetValue(tier, out Group found))
        {
            return found;
        }

        var made = new Group { Tier = tier };
        byTier[tier] = made;
        return made;
    }

    // StatDataSO는 HeroData를 참조하지 않는다 — 임포터가 구운 파일명 규칙으로만 찾는다.
    private static StatDataSO FindStat(string heroName)
    {
        if (string.IsNullOrEmpty(heroName))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<StatDataSO>($"{StatFolder}/{heroName}StatData.asset");
    }
}
