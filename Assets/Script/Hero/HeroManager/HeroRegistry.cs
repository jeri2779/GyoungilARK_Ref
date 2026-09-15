using System.Collections.Generic;
using UnityEngine;

// HeroData/HeroPrefab(정적 데이터)과 현재 배치된 Hero 인스턴스(동적 상태)를 함께 보관하는 창구.
// HeroCombineManager는 여기서 후보/후보프리팹만 받아 합성 실행(제거/파괴/로스터 갱신)만 담당한다.
public class HeroRegistry : MonoBehaviour
{
    [SerializeField] private List<HeroData> datas;
    [SerializeField] private MapGame game;

    public IReadOnlyList<HeroData> AllHeroDatas => datas;

    // HeroRoster.Entries를 MergeKey로 묶어둔 캐시. HeroRoster가 원본, 여긴 조회용 인덱스일 뿐.
    // Placed/Available 상태와 무관하게 엔트리 자체를 담아, 배치되지 않은 로스터 사본도 합성 후보가 되게 한다.
    private readonly Dictionary<MergeKey, List<HeroRosterEntry>> heroesByKey = new();

    // 영웅 생성 뽑기용: (티어, HeroData.HeroType(0=근접/1=원거리))로 후보를 묻는 인덱스.
    private readonly Dictionary<(int Tier, int Kind), List<HeroData>> heroDatasByTierAndKind = new();

    private void Awake()
    {
        foreach (HeroData data in datas)
        {
            if (data.HeroPrefab == null) continue; // 프리팹 참조가 끊긴 데이터는 합성/생성 후보 풀에서 제외.

            var tierKindKey = (data.Tier, data.HeroType);
            if (!heroDatasByTierAndKind.TryGetValue(tierKindKey, out List<HeroData> kindList))
            {
                kindList = new List<HeroData>();
                heroDatasByTierAndKind[tierKindKey] = kindList;
            }
            kindList.Add(data);
        }
    }

    // MapGame의 [Inject] Construct가 Awake 단계에 끝나므로, HeroRoster를 읽는 구독은 Start에서 시작한다.
    private void Start()
    {
        game.HeroRoster.Changed += RebuildHeroIndex;
        RebuildHeroIndex();
    }

    private void OnDestroy()
    {
        game.HeroRoster.Changed -= RebuildHeroIndex;
    }

    // 로스터가 바뀔 때마다(배치/제거/합성 등) 로스터 엔트리 전체(배치 여부 무관)를 MergeKey로 다시 그룹핑한다.
    private void RebuildHeroIndex()
    {
        heroesByKey.Clear();

        foreach (HeroRosterEntry entry in game.HeroRoster.Entries)
        {
            if (!entry.TryGetMergeKey(out MergeKey key)) continue;

            if (!heroesByKey.TryGetValue(key, out List<HeroRosterEntry> list))
            {
                list = new List<HeroRosterEntry>();
                heroesByKey.Add(key, list);
            }
            list.Add(entry);
        }
    }

    // 특정 MergeKey로 합성 가능한(3개 이상 모인) 후보가 있는지.
    // 배치된(강화 진행도가 있는) 영웅이 불필요하게 소모되지 않도록, 로스터에만 있는 사본을 앞에 두고 돌려준다.
    public bool TryGetCombinable(MergeKey key, out List<HeroRosterEntry> candidates)
    {
        if (!heroesByKey.TryGetValue(key, out List<HeroRosterEntry> list) || list.Count < 3)
        {
            candidates = null;
            return false;
        }

        candidates = new List<HeroRosterEntry>(list);
        candidates.Sort((a, b) => (a.PlacedUnit != null ? 1 : 0).CompareTo(b.PlacedUnit != null ? 1 : 0));
        return true;
    }

    // currentTier 영웅들을 합성했을 때 나올 수 있는 다음 티어(currentTier + 1), 같은 종류(근접/원거리) HeroData 후보들.
    public bool TryGetNextTierHeroDatas(int currentTier, OccupantKind kind, out List<HeroData> datas)
    {
        int heroTypeKind = kind == OccupantKind.RangedHero ? 1 : 0;
        return heroDatasByTierAndKind.TryGetValue((currentTier + 1, heroTypeKind), out datas) && datas.Count > 0;
    }

    // 영웅 생성 뽑기용: 그 티어+종류(근접/원거리)에 해당하는 HeroData들.
    public bool TryGetHeroDatas(int tier, OccupantKind kind, out List<HeroData> datas)
    {
        int heroTypeKind = kind == OccupantKind.RangedHero ? 1 : 0;
        return heroDatasByTierAndKind.TryGetValue((tier, heroTypeKind), out datas) && datas.Count > 0;
    }
}
