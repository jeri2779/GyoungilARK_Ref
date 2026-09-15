using UnityEngine;

// 고른 타일에 선택·배치·제거·집기·내려놓기를 수행하고 결과를 MapView에 알린다.
public class PlaceAction
{
    // MapCommand가 조립할 때 넣어준다
    public PlacePalette palette;
    public UnitPlacer placer;
    public UnitRemover remover;
    public UnitReplace replace;
    public DayNightBuildRule dayNightRule;
    public MapView view;
    public HeroRoster heroRoster;
    public HeroSkillCastController skillCast;
    public PlayerSkillCastController playerSkillCast;
    public HeroCombineManager combineManager;
    public HoverPlaceData hoverPlace;
    public string placeSoundKey;
    public string removeSoundKey;

    private Hero lastClickedHero;
    private float lastClickTime;
    private const float DoubleClickWindow = 0.3f;

    public void SelectTile(Tile tile)
    {
        view.Select(tile);

        // 플레이어 스킬이 무장돼 있으면 이 클릭은 전부 그쪽이 처리한다 — 영웅 시전자 선택 로직은 건드리지 않는다.
        if (playerSkillCast != null && playerSkillCast.HandleClick(tile))
        {
            ShowOutline(tile);
            TryDoubleClickCombine(tile);
            return;
        }

        skillCast?.HandleClick(tile);
        ShowOutline(tile);
        TryDoubleClickCombine(tile);
    }

    // 짧은 시간 안에 같은 영웅이 다시 클릭되면 더블클릭으로 보고 그 자리에서 합성을 시도한다.
    private void TryDoubleClickCombine(Tile tile)
    {
        if (combineManager == null || !TryGetHero(tile, out Hero hero))
        {
            lastClickedHero = null;
            return;
        }

        bool isDoubleClick = hero == lastClickedHero && Time.time - lastClickTime <= DoubleClickWindow;
        lastClickedHero = hero;
        lastClickTime = Time.time;

        if (isDoubleClick)
        {
            if (hero.TryGetComponent(out HeroRosterLink link) && link.Entry != null)
            {
                combineManager.TryCombine(link.Entry);
            }
            lastClickedHero = null; // 연속 트리거 방지, 성공/실패 상관없이 한 번만 시도
        }
    }

    // 고른 칸에 영웅이 서 있으면 테두리를 켜고, 아니면 끈다.
    private void ShowOutline(Tile tile)
    {
        if (TryGetHero(tile, out Hero hero))
        {
            HeroSelectionService.Select(hero);
            //유닛 확인용 로그
            // Debug.Log(
            // $"{hero.HeroName} - 체력 {hero.SC[StatType.HP]} · 공격 {hero.SC[StatType.ATK]} · " +
            // $"방어 {hero.SC[StatType.DEF]} · 저지 {hero.SC[StatType.BLK]}");
            
            return;
        }

        HeroSelectionService.Clear();
    }

    // 그 칸에 올라간 것이 영웅인지 본다.
    private static bool TryGetHero(Tile tile, out Hero hero)
    {
        if (!tile.HasUnit)
        {
            hero = null;
            return false;
        }

        return tile.OccupantObject.TryGetComponent(out hero);
    }

    public void PlaceUnit(Tile tile)
    {
        // 미리보기가 이번 화면에 이미 구해서 hoverPlace에 남겨둔 자리를 그대로 쓴다.
        if (!palette.TryCurrentSlot(out Placeable slot)) return;
        if (!hoverPlace.HasData) return;
        PlaceData data = hoverPlace.Data;

        HeroRosterEntry entry = palette.CurrentRuntimeEntry;   // 배치 전에 미리 캡처(성공 후 모드가 바뀔 수 있음)
        if (IsEntryPlaced(entry)) return;
        if (IsAreaBlocked(data)) return;
        if (IsNightTime()) { /* Debug.Log("밤에는 배치할 수 없습니다."); */ return; } // 테스트용
        if (!placer.TryPlace(data, slot, out GameObject placedUnit)) return;
        if (!string.IsNullOrEmpty(placeSoundKey)) EnemySoundManager.Play(placeSoundKey);

        MarkRosterPlaced(entry, placedUnit);
        view.Select(tile);
        palette.ClearMode();   // 개체 하나뿐이니 배치 즉시 Place 모드 종료(연속 배치 방지)
    }

    // 영웅은 개체가 하나뿐이라 이미 판에 올라간 엔트리를 또 놓을 수 없다.
    private bool IsEntryPlaced(HeroRosterEntry entry)
    {
        if (entry == null) return false;
        return entry.State == HeroRosterState.Placed;
    }

    // 한 칸이라도 막히면 배치하지 않는다.
    private bool IsAreaBlocked(PlaceData data)
    {
        return !data.CanPlace;
    }

    // GameManager의 CanBuild가 낮을 뜻한다(DayState가 낮에 true, 밤에 false로 바꾼다).
    private bool IsNightTime()
    {
        return !dayNightRule.CanBuild();
    }

    // 로스터로 고른 영웅만 해당. 제거될 때 UnitRemover가 이 링크를 보고 엔트리를 되돌린다.
    private void MarkRosterPlaced(HeroRosterEntry entry, GameObject placedUnit)
    {
        if (entry == null) return;
        entry.MarkPlaced(placedUnit);
        HeroRosterLink.Attach(placedUnit, entry);
        heroRoster.NotifyStateChanged();
    }

    public void RemoveUnit(Tile tile)
    {
        bool wasHero = TryGetHero(tile, out Hero hero);   // 지운 뒤엔 칸이 비어 물어볼 수 없다
        if (!remover.TryRemoveUnit(tile)) return;
        if (!string.IsNullOrEmpty(removeSoundKey)) EnemySoundManager.Play(removeSoundKey);
        if (view.IsSelected(tile)) view.ClearSelection();

        if (wasHero)
        {
            HeroSelectionService.ClearIfSelected(hero);
        }
    }

    public void PickUpUnit(Tile tile)
    {
        if (IsNightTime()) return; 
        bool hasHero = TryGetHero(tile, out Hero hero);
        if (!replace.PickUp(tile)) return;
        if (hasHero && !string.IsNullOrEmpty(removeSoundKey)) EnemySoundManager.Play(removeSoundKey);
        view.Select(tile);

        if (hasHero)
        {
            HeroSelectionService.Select(hero);
        }
    }

    public void Drop(PlaceData data)
    {
        bool wasHero = replace.HeldUnit != null && replace.HeldUnit.TryGetComponent<Hero>(out _);
        if (!replace.TryDrop(data)) return;
        if (wasHero && !string.IsNullOrEmpty(placeSoundKey)) EnemySoundManager.Play(placeSoundKey);

        // 선택 표시는 칸 하나에 붙으므로 덮은 칸 중 시작 칸을 대표로 쓴다.
        if (data.Area.Board.TryGetCell(data.Area.Origin, out Tile tile)) view.Select(tile);
    }
}
