using System;
using System.Collections.Generic;
using UnityEngine;

// 검증된 데이터 모음을 게임 시스템에 적용한다 (Command만, 값을 반환하지 않음)
public class SaveRestore
{
    private readonly GameManager gameManager;
    private readonly ResourcesManager resourcesManager;
    private readonly CitizenManager citizenManager;
    private readonly RegionOverviewPanel regionPanel;
    private readonly MapRegistry mapRegistry;
    private readonly SpawnerManager spawnerManager;
    private readonly BaseConstructor baseConstructor;
    private readonly FacilityBuildChoicePanel buildPanel;
    private readonly HeroRoster heroRoster;
    private readonly HeroRegistry heroRegistry;
    private readonly HeroTierUpgradeState tierState;
    private readonly HeroClassUpgradeState classState;
    private readonly MapGame mapGame;
    private readonly MapAssemble mapAssemble;
    private readonly FacilityManager facilityManager;
    private readonly GimmickTileData gimmickTileData;
    private readonly TutorialState tutorialState;

    public SaveRestore(
        GameManager gameManager,
        ResourcesManager resourcesManager,
        CitizenManager citizenManager,
        RegionOverviewPanel regionPanel,
        MapRegistry mapRegistry,
        SpawnerManager spawnerManager,
        BaseConstructor baseConstructor,
        FacilityBuildChoicePanel buildPanel,
        HeroRoster heroRoster,
        HeroRegistry heroRegistry,
        HeroTierUpgradeState tierState,
        HeroClassUpgradeState classState,
        MapGame mapGame,
        MapAssemble mapAssemble,
        FacilityManager facilityManager,
        GimmickTileData gimmickTileData,
        TutorialState tutorialState)
    {
        this.gameManager = gameManager;
        this.resourcesManager = resourcesManager;
        this.citizenManager = citizenManager;
        this.regionPanel = regionPanel;
        this.mapRegistry = mapRegistry;
        this.spawnerManager = spawnerManager;
        this.baseConstructor = baseConstructor;
        this.buildPanel = buildPanel;
        this.heroRoster = heroRoster;
        this.heroRegistry = heroRegistry;
        this.tierState = tierState;
        this.classState = classState;
        this.mapGame = mapGame;
        this.mapAssemble = mapAssemble;
        this.facilityManager = facilityManager;
        this.gimmickTileData = gimmickTileData;
        this.tutorialState = tutorialState;
    }

    // 문서 §14.3 로드 순서대로 저장 데이터 전체를 게임에 적용한다
    public void RestoreSaveData(SaveData data)
    {
        Dictionary<int, HeroData> heroDataByUnitId = BuildHeroIndex();
        Dictionary<string, HeroRosterEntry> restoredEntries = new Dictionary<string, HeroRosterEntry>();

        EnemyArchiveData.RestoreAll(data.archiveList);
        tutorialState.RestoreSeen(data.tutorialSeen);
        RestoreProgress(data);
        RestoreRegions(data);
        RestoreResources(data);
        RestoreCitizen(data);
        RestoreBuilds(data);
        RestoreTiers(data);
        RestoreClasses(data);
        RestoreHeroes(data, heroDataByUnitId, restoredEntries);
        RestorePlacements(data, restoredEntries);
        RestorePortals(data);
    }

    // 일차·체력·아침체력·해금영웅 넣기   → GameManager
    private void RestoreProgress(SaveData data)
    {
        gameManager.RestoreDayCount(data.dayCount);
        gameManager.RestoreHp(data.baseHp);
        gameManager.RestoreTodayHp(data.baseHp);
        gameManager.RestoreUnlockedHero(data.heroUnlock);
        gameManager.RestoreGameSeed(data.gameSeed);
        gameManager.RestoreHeroDrawCounts(data.heroDrawMeleeCount, data.heroDrawRangedCount);
        gameManager.RestoreHeroesCreatedToday(data.heroesCreatedToday);
        gameManager.RestoreHeroCombineCounts(data.heroCombineMeleeCounts, data.heroCombineRangedCounts);
        gameManager.ChangeRequest(data.dayCount > 0 && data.dayCount % 10 == 0);
    }

    // 지역 상태·오프셋 넣기          → ModuleLogic / SpawnerManager
    private void RestoreRegions(SaveData data)
    {
        for (int i = 0; i < data.regionList.Length; i++)
        {
            RegionSave save = data.regionList[i];
            if (mapRegistry.TryGetModuleLogic(save.moduleId, out ModuleLogic module))
            {
                module.SetState(save.moduleState);
            }
            spawnerManager.RestoreOffset(save.moduleId, save.stageOffset, save.unlockDay);
            gimmickTileData.RestoreShown(save.moduleId, save.gimmickSeen);
        }
    }

    // 자원 6종 넣기                  → ResourcesManager
    private void RestoreResources(SaveData data)
    {
        resourcesManager.RestoreResources(
            data.woodAmount, data.foodAmount, data.goldAmount,
            data.ironAmount, data.stoneAmount, data.specialAmount);
    }

    // 시민 값 그대로 지정             → CitizenManager
    private void RestoreCitizen(SaveData data)
    {
        citizenManager.RestoreCitizen(data.currentCitizen);
        citizenManager.RestoreUsedCitizen(data.usedCitizen, data.heroUsedCitizen);
    }

    // 기반시설 비용 없이 짓기        → BaseConstructor
    private void RestoreBuilds(SaveData data)
    {
        for (int i = 0; i < data.buildList.Length; i++)
        {
            BuildSave save = data.buildList[i];
            if (!buildPanel.TryFindOption(save.buildKey, out BuildableFacility option)) continue;

            RegionFacilitySlots region = FindRegion(save.moduleId);
            if (region == null) continue;

            baseConstructor.RestoreBuild(option, region, save);
        }
    }

    // 티어별 레벨 넣기               → HeroTierUpgradeState
    private void RestoreTiers(SaveData data)
    {
        for (int i = 0; i < data.tierList.Length; i++)
        {
            TierSave save = data.tierList[i];
            tierState.RestoreLevel(save.heroTier, save.tierLevel);
        }
    }

    // 클래스별 레벨 넣기             → HeroClassUpgradeState
    private void RestoreClasses(SaveData data)
    {
        for (int i = 0; i < data.classList.Length; i++)
        {
            ClassSave save = data.classList[i];
            classState.RestoreLevel(save.heroType, save.classLevel);
        }
    }

    // 저장 Guid로 로스터 복원        → HeroRoster
    private void RestoreHeroes(SaveData data, Dictionary<int, HeroData> heroDataByUnitId, Dictionary<string, HeroRosterEntry> restoredEntries)
    {
        for (int i = 0; i < data.heroList.Length; i++)
        {
            HeroSave save = data.heroList[i];
            if (!heroDataByUnitId.TryGetValue(save.unitId, out HeroData heroData)) continue;

            Guid rosterId = Guid.Parse(save.rosterId);
            Placeable slot = BuildSlot(heroData);
            HeroRosterEntry entry = heroRoster.AddRestored(rosterId, slot, heroData, save.citizenCost);
            restoredEntries[save.rosterId] = entry;
        }
    }

    // 맵에 영웅 다시 세우기          → UnitPlacer
    private void RestorePlacements(SaveData data, Dictionary<string, HeroRosterEntry> restoredEntries)
    {
        for (int i = 0; i < data.placeList.Length; i++)
        {
            PlaceSave save = data.placeList[i];
            if (!restoredEntries.TryGetValue(save.rosterId, out HeroRosterEntry entry)) continue;
            if (!mapRegistry.TryGetModuleLogic(save.moduleId, out ModuleLogic module)) continue;

            MapBoard board = module.GetComponent<MapBoard>();
            if (board == null) continue;

            PlacementArea area = BuildArea(board, save.cellOrigin, save.cellSize);
            Vector3 position = AreaPlace.Position(area, entry.Slot.kind, mapAssemble.PlaceYOffset, out bool canPlace);
            PlaceData placeData = new PlaceData(area, position, canPlace);

            if (!mapGame.Placer.TryPlace(placeData, entry.Slot, out GameObject placedUnit)) continue;

            entry.MarkPlaced(placedUnit);
            HeroRosterLink.Attach(placedUnit, entry);
        }

        heroRoster.NotifyStateChanged();
    }

    // 아래 둘은 SaveManager가 DayStart일 때만 골라서 부름 (여기선 조건문 없이 "실행만")

    // 시설 생산 1회               → FacilityManager (이미 public)
    public void ApplyProduction()
    {
        facilityManager.SumProduct();
    }

    // 지역 해금 레드닷 확인 여부 되돌리기 → RegionOverviewPanel
    public void RestoreRegionNotice(bool seen)
    {
        regionPanel.RestoreNoticeSeen(seen);
    }

    // 완벽방어 보상 1회            → ResourcesManager
    public void ApplyPerfectDefenseReward()
    {
        resourcesManager.GetSpecial();
    }

    // 활성 포탈 재구성 (NightReady 저장본만 목록이 차 있다) → SpawnerManager
    private void RestorePortals(SaveData data)
    {
        if (data.portalList.Length == 0) { spawnerManager.RefreshPortals(); return; }
        for (int i = 0; i < data.portalList.Length; i++)
        {
            PortalSave save = data.portalList[i];
            spawnerManager.RestorePortal(save.regionId, save.spawnCells);
        }
    }

    // HeroRegistry.AllHeroDatas를 UnitId로 한 번 색인한다
    private Dictionary<int, HeroData> BuildHeroIndex()
    {
        Dictionary<int, HeroData> index = new Dictionary<int, HeroData>();
        IReadOnlyList<HeroData> all = heroRegistry.AllHeroDatas;
        for (int i = 0; i < all.Count; i++)
        {
            index[all[i].UnitId] = all[i];
        }
        return index;
    }

    // HeroData로 배치용 슬롯을 만든다 (RestoreHeroes 전용)
    private static Placeable BuildSlot(HeroData heroData)
    {
        Placeable slot = new Placeable();
        slot.prefab = heroData.HeroPrefab;
        slot.kind = ResolveKind(heroData.HeroType);
        return slot;
    }

    // HeroData.HeroType(0=근접/1=원거리)을 OccupantKind로 바꾼다
    private static OccupantKind ResolveKind(int heroType)
    {
        if (heroType == 1) return OccupantKind.RangedHero;
        return OccupantKind.MeleeHero;
    }

    // moduleId로 지역 슬롯 목록을 찾는다 (RestoreBuilds 전용)
    private RegionFacilitySlots FindRegion(int moduleId)
    {
        IReadOnlyList<RegionFacilitySlots> regions = regionPanel.Regions;
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].ModuleId == moduleId) return regions[i];
        }
        return null;
    }

    // 저장된 칸 원점·크기로 배치 자리를 만든다 (RestorePlacements 전용)
    private static PlacementArea BuildArea(MapBoard board, Vector2Int origin, Vector2Int size)
    {
        List<Vector2Int> cells = AreaCalc.GetCells(origin, size);
        Vector2 mid = new Vector2(origin.x + size.x * 0.5f, origin.y + size.y * 0.5f);
        Vector3 center = board.CellPointToWorld(mid);
        return new PlacementArea(board, origin, size, cells, center);
    }
}
