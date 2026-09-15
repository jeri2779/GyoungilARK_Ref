using System.Collections.Generic;
using UnityEngine;

// 같은 영웅 3개를 합성해 다음 티어 영웅 1개를 만드는 담당. 결과는 항상 원본과 같은 종류(근접/원거리)다.
// 합쳐지는 3개 중 필드에 배치된 게 있었다면 결과 영웅을 그 자리에 그대로 배치하고(더블클릭한
// 대상 자신이 배치돼 있었다면 그걸 최우선으로, 아니면 자동으로 같이 골린 나머지 중 배치된 걸
// 대신 씀 - Combine 참고), 셋 다 미배치였다면 기존처럼 HeroRoster에 Available 엔트리로만
// 추가한다(배치는 플레이어가 직접).
// 합성 후보/다음 티어 프리팹 정보는 HeroRegistry에서 받아오고, 여긴 합성 실행(제거/파괴/로스터 갱신/배치)만 한다.
public class HeroCombineManager : MonoBehaviour
{
    [SerializeField] private HeroRegistry heroRegistry;
    [SerializeField] private MapGame game;
    [Tooltip("영웅 합성 성공 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    [SerializeField] private string combineSoundKey;

    // 특정 MergeKey로 합성 가능한(3개 이상 모인) 후보가 있는지.
    public bool TryGetCombinable(MergeKey key, out List<HeroRosterEntry> candidates)
    {
        return heroRegistry.TryGetCombinable(key, out candidates);
    }

    // 더블클릭된 특정 엔트리를 반드시 포함해 합성 시도 — 나머지 2개는 기존과 같은 순서(로스터 전용 사본 우선)로 채운다.
    public bool TryCombine(HeroRosterEntry pinnedEntry)
    {
        if (pinnedEntry == null || !pinnedEntry.TryGetMergeKey(out MergeKey key)) return false;
        if (!TryGetCombinable(key, out List<HeroRosterEntry> candidates)) return false;
        if (!candidates.Contains(pinnedEntry)) return false;

        List<HeroRosterEntry> entries = new List<HeroRosterEntry>(3) { pinnedEntry };
        foreach (HeroRosterEntry candidate in candidates)
        {
            if (entries.Count == 3) break;
            if (candidate == pinnedEntry) continue;
            entries.Add(candidate);
        }

        return Combine(entries, pinnedEntry);
    }

    // 정확히 이 3개로 합성 시도(테스트 UI용) — 맵에서 선택된 배치 영웅만 다루므로 MergeKey는 인스턴스에서 바로 읽는다.
    public bool TryCombine(List<Hero> heroes)
    {
        if (heroes == null || heroes.Count != 3) return false;

        MergeKey key = heroes[0].MergeKey;
        var entries = new List<HeroRosterEntry>(3);
        foreach (Hero hero in heroes)
        {
            if (!hero.MergeKey.Equals(key)) return false;
            if (!hero.TryGetComponent(out HeroRosterLink link) || link.Entry == null) return false;
            entries.Add(link.Entry);
        }

        return Combine(entries, null);
    }

    // 호출부(예: 인벤토리 일괄합성)가 이미 필터링 등을 거쳐 고른 정확히 3개를 그대로 합성 — 후보 선정은
    // 호출부 책임이고 여긴 검증(개수/MergeKey 일치) 후 실행만 한다.
    public bool TryCombine(List<HeroRosterEntry> entries)
    {
        if (entries == null || entries.Count != 3) return false;
        if (!entries[0].TryGetMergeKey(out MergeKey key)) return false;

        foreach (HeroRosterEntry entry in entries)
            if (!entry.TryGetMergeKey(out MergeKey entryKey) || !entryKey.Equals(key))
                return false;

        return Combine(entries, null);
    }

    private bool Combine(List<HeroRosterEntry> entries, HeroRosterEntry pinnedEntry)
    {
        if (game.Rule != null && !game.Rule.CanBuild) return false; // 밤에는 합성 불가
        if (!entries[0].TryGetMergeKey(out MergeKey key)) return false;

        int tier = key.Tier;
        OccupantKind kind = entries[0].Data.HeroType == 1 ? OccupantKind.RangedHero : OccupantKind.MeleeHero;
        if (!heroRegistry.TryGetNextTierHeroDatas(tier, kind, out List<HeroData> nextTierDatas))
            return false; // 최고 티어거나, 같은 종류의 다음 티어 데이터 없음

        (string seed, int count) = game.Rule.ConsumeHeroCombine(kind, tier);
        System.Random rng = new System.Random(GameSeeding.Derive(seed, count));
        HeroData nextTierData = nextTierDatas[rng.Next(nextTierDatas.Count)];
        GameObject nextTierPrefab = nextTierData.HeroPrefab;

        // 합성 결과는 결과 티어만큼 인구수를 차지 — 원본 3개가 쓰던 인구수는 여기서 정산해 반환한다.
        int mergedHeroCitizenCost = nextTierData.PopulationCost;
        int refund = 0;
        PlacementArea pinnedArea = null;
        Vector3 pinnedPosition = default;

        // 결과 영웅을 되돌려놓을 자리는 더블클릭한 대상(pinnedEntry)이 배치돼 있었다면 그걸 최우선으로
        // 쓴다 - 기존 의도(플레이어가 직접 고른 배치 영웅을 더블클릭했다면 그 자리 유지)를 그대로
        // 지킨다. pinnedEntry가 없거나 미배치였다면, 자동으로 같이 골린 나머지 후보 중 배치된 게
        // 있는지 대신 찾는다 - 안 그러면 자동 선택된 후보 하나가 실제로는 필드에 배치돼 있었을 때
        // 그 영웅만 제거되고 결과물은 인벤토리로 들어가버려(전투 중이던 영웅이 조용히 사라짐) 버그가 된다.
        HeroRosterEntry anchorEntry = pinnedEntry != null && pinnedEntry.PlacedUnit != null ? pinnedEntry : null;
        if (anchorEntry == null)
        {
            foreach (HeroRosterEntry candidate in entries)
            {
                if (candidate.PlacedUnit == null) continue;
                anchorEntry = candidate;
                break;
            }
        }

        foreach (HeroRosterEntry entry in entries)
        {
            refund += entry.CitizenCost;

            GameObject unit = entry.PlacedUnit;
            if (unit != null)
            {
                unit.TryGetComponent(out Hero hero);
                //game.Placer.zoneEffectApplier.ExitZone(hero); // 파괴 전 지대 효과 추적에서 해제

                if (game.Units.TryGetArea(unit, out PlacementArea area))
                {
                    if (entry == anchorEntry)
                    {
                        pinnedArea = area;
                        pinnedPosition = unit.transform.position;
                    }

                    AreaPlace.Remove(area);
                    game.Units.Remove(unit);
                }

                if (hero != null)
                {
                    hero.PrepareForDespawn();
                    PoolManager.Instance.Despawn(unit);
                }
                else
                {
                    Destroy(unit);
                }
            }

            game.HeroRoster.Remove(entry);
        }

        game.CitizenManager.FreeCitizenForHero(refund);
        game.CitizenManager.UseCitizenForHero(mergedHeroCitizenCost);

        Placeable newSlot = new Placeable
        {
            label = nextTierPrefab.name,
            prefab = nextTierPrefab,
            kind = nextTierPrefab.GetComponent<Hero>().OccupantKind,
        };

        HeroRosterEntry newEntry = game.HeroRoster.Add(newSlot, nextTierData, mergedHeroCitizenCost);

        // anchorEntry(더블클릭 대상 우선, 없으면 배치된 다른 후보)가 필드에 배치돼 있었다면,
        // 결과 영웅을 그 자리에 그대로 배치한다.
        if (pinnedArea != null && AreaPlace.CanPlace(pinnedArea, newSlot.kind))
        {
            PlaceData placeData = new PlaceData(pinnedArea, pinnedPosition, true);
            if (game.Placer.TryPlace(placeData, newSlot, out GameObject placedUnit))
            {
                newEntry.MarkPlaced(placedUnit);
                HeroRosterLink.Attach(placedUnit, newEntry);
                game.HeroRoster.NotifyStateChanged();
            }
        }

        if (!string.IsNullOrEmpty(combineSoundKey)) EnemySoundManager.Play(combineSoundKey);
        return true;
    }
}
