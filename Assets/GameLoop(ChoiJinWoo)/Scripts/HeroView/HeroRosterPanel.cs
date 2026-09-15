using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;

// 보유 영웅 목록 표시 전용. Available 아이콘 클릭은 배치 모드 진입, Placed 아이콘 클릭은 카메라 이동.
public class HeroRosterPanel : MonoBehaviour
{
    [SerializeField] private MapView view;
    [SerializeField] private MapGame game;
    [SerializeField] private CameraRig cameraRig;
    [SerializeField] private HeroRosterIcon iconPrefab;
    [SerializeField] private Transform container;
    [SerializeField] private HeroCombineManager combineManager;

    private readonly Dictionary<HeroRosterEntry, HeroRosterIcon> icons = new();
    private ObjectPool<HeroRosterIcon> iconPool;

    private void Awake()
    {
        // Hero.effectPools(Hero.cs)와 같은 패턴 — 이 패널 하나가 소유하는 로컬 풀이라 전역 PoolManager에
        // UI를 끼워 넣지 않아도 되고, 캔버스 계층 밖으로 reparent되는 부작용도 없다.
        iconPool = new ObjectPool<HeroRosterIcon>(
            createFunc: () => Instantiate(iconPrefab, container),
            actionOnGet: icon => icon.gameObject.SetActive(true),
            actionOnRelease: icon => { icon.PrepareForReuse(); icon.gameObject.SetActive(false); },
            actionOnDestroy: icon => Destroy(icon.gameObject),
            collectionCheck: true,
            defaultCapacity: 16);
    }

    private void OnEnable()
    {
        game.HeroRoster.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        game.HeroRoster.Changed -= Refresh;
    }
    // 빠진 엔트리의 아이콘만 제거하고 새 엔트리만 생성하며, 남은 아이콘은 Set()으로 값만 갱신한다.
    private void Refresh()
    {
        var current = new HashSet<HeroRosterEntry>(game.HeroRoster.Entries);

        List<HeroRosterEntry> stale = null;
        foreach (KeyValuePair<HeroRosterEntry, HeroRosterIcon> kv in icons)
            if (!current.Contains(kv.Key))
                (stale ??= new List<HeroRosterEntry>()).Add(kv.Key);

        if (stale != null)
            foreach (HeroRosterEntry entry in stale)
            {
                iconPool.Release(icons[entry]);
                icons.Remove(entry);
            }

        foreach (HeroRosterEntry entry in game.HeroRoster.Entries)
        {
            if (!icons.TryGetValue(entry, out HeroRosterIcon icon))
            {
                icon = iconPool.Get();
                icons[entry] = icon;
            }
            icon.Set(entry, OnIconClicked, OnIconDoubleClicked);
            // 재사용된 아이콘은 예전 sibling index를 그대로 갖고 있어 순서가 어긋날 수 있다 —
            // 매번 Entries 순회 순서대로 맨 끝으로 밀어 넣어 목록 순서를 항상 일치시킨다.
            icon.transform.SetAsLastSibling();

            //// 배치 중이면 실시간 값을, 제거되어 있으면 제거 시점에 저장해둔 값을 보여준다.
            //Hero placedHero = entry.PlacedUnit != null ? entry.PlacedUnit.GetComponent<Hero>() : null;
            //if (placedHero != null)
            //    icon.UpdateLevel(placedHero.StatLevel, placedHero.SkillLevel);
            //else
            //    icon.UpdateLevel(entry.StatLevel, entry.SkillLevel);
        }
    }

    private void OnIconClicked(HeroRosterEntry entry, HeroRosterIcon icon)
    {
        if (entry.State == HeroRosterState.Placed)
        {
            cameraRig.focus = entry.PlacedUnit.transform.position;
            cameraRig.ApplyNow();
            HeroSelectionService.Select(entry.PlacedUnit.GetComponent<Hero>());
            // 바깥 클릭으로 패널이 닫혀 있으면(activeSelf == false) 같은 영웅이라도 다시 열어야 한다.
            //if (!heroUpgradePanel.gameObject.activeSelf || currentObject != entry.PlacedUnit)
            //{
            //    currentObject = entry.PlacedUnit;
            //    heroUpgradePanel.gameObject.SetActive(true);
            //    heroUpgradePanel.InitHeroInfo(entry.PlacedUnit.GetComponent<Hero>());
            //    heroUpgradePanel.PositionAtIconY(icon, container);
            //}
        }
        else
        {
            view.SetHero(entry);
        }
    }

    // 로스터 아이콘을 더블클릭하면(배치 여부 상관없이) 그 영웅의 MergeKey로 바로 합성을 시도한다.
    private void OnIconDoubleClicked(HeroRosterEntry entry)
    {
        combineManager.TryCombine(entry);
    }
}
