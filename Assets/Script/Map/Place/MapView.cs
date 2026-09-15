using System;
using UnityEngine;

// UI가 읽을 맵 상태(선택 타일·상태 문구·현재 모드·사거리)를 보관하고 내준다.
public class MapView : MonoBehaviour
{
    [SerializeField] private MapInput input;
    [SerializeField] private PlacePalette palette;

    // MapCommand가 조립할 때 넣어준다
    public PointerPick pointerPick;
    public CitizenManager citizenManager;
    public ResourcesManager resourcesManager;

    // MapAssemble이 조립할 때 넣어준다 — ESC/우클릭 취소가 BuildModePanel/MapCommand에서 이 창구로 들어온다.
    public HeroSkillCastController skillCast;
    public PlayerSkillCastController playerSkillCast;

    private UnitReplace _replace;
    // MapAssemble이 대입하는 시점에 held 변경 이벤트를 걸어준다.
    public UnitReplace replace
    {
        get { return _replace; }
        set
        {
            if (_replace != null)
            {
                _replace.OnHoldChanged -= HandleHoldChanged;
                _replace.Replaced -= HandleReplaced;
            }
            _replace = value;
            if (_replace != null)
            {
                _replace.OnHoldChanged += HandleHoldChanged;
                _replace.Replaced += HandleReplaced;
            }
        }
    }

    private readonly SelectedTileData tileSelect = new();

    private void OnEnable()
    {
        palette.OnModeChanged += HandleModeChanged;
    }

    private void OnDisable()
    {
        palette.OnModeChanged -= HandleModeChanged;
    }

    // 모드·집은 상태 중 하나라도 바뀌면 UI가 폴링 없이 갱신할 수 있게 알린다.
    public event Action OnStateChanged;

    private void HandleModeChanged()
    {
        // 재배치 모드에서 집은 채로 다른 모드로 빠지면(취소) 원래 자리로 돌려놓는다.
        if (!IsReplacing && _replace != null && _replace.IsHolding)
        {
            _replace.ReturnHeld();
        }

        OnStateChanged?.Invoke();
        if (IsOff) OnOffMode?.Invoke();
    }

    private void HandleHoldChanged()
    {
        OnStateChanged?.Invoke();
    }

    // 재배치가 실제로 replace 인스턴스가 새로 만들어지는 시점(MapAssemble.Start) 이후에나
    // 붙는다. TutorialManager처럼 그보다 먼저 구독하는 쪽이 있을 수 있으니, MapView 자신을 통해
    // 재중계해서 구독 시점과 무관하게 항상 받을 수 있게 한다.
    public event Action<GameObject, OccupantKind> Replaced;
    private void HandleReplaced(GameObject unit, OccupantKind kind) => Replaced?.Invoke(unit, kind);

    // ---- 상태 기록(PlaceAction이 결과를 알릴 때 부른다) ----

    public void Select(Tile tile) { tileSelect.Select(tile); }
    public void ClearSelection() { tileSelect.Clear(); }
    public bool IsSelected(Tile tile) { return tileSelect.IsSelected(tile); }

    // ---- 읽기(TilePaintView가 본다) ----

    public bool IsHolding { get { return _replace != null && _replace.IsHolding; } }
    public bool InputBlocked { get { return input.WorldBlocked; } }
    public GameObject HeldUnit { get { return replace.HeldUnit; } }
    public OccupantKind HeldKind { get { return replace.HeldKind; } }
    public Tile HoverTile
    {
        get
        {
            if (pointerPick == null)
            {
                return null;
            }

            if (InputBlocked)
            {
                return null;
            }

            return pointerPick.UnderPointer();
        }
    }
    public bool IsPlacing { get { return palette.Mode == PlaceMode.Place; } }
    public bool IsReplacing { get { return palette.Mode == PlaceMode.Replace; } }
    public bool IsRemoving { get { return palette.Mode == PlaceMode.Remove; } }
    public bool IsOff { get { return palette.Mode == PlaceMode.Off; } }
    public Vector2Int HeldSize { get { return replace.HeldSize; } }

    // 재배치 미리보기 색칠용: 지금 든 유닛을 이 자리에 놓을 수 있는지(바로 놓거나, 자리를 바꿔서라도).
    public bool CanPlaceOrSwap(PlaceData data) { return replace.CanPlaceOrSwap(data); }

    public Tile Selected { get { return tileSelect.Selected; } }

    public event Action OnOffMode;

    // ---- 모드 전환(UI 버튼이 부른다) ----

    public void SetHero(HeroRosterEntry entry) => palette.SelectRuntimeSlot(entry);
    public void SetReplace()
    {
        palette.SelectReplace();
    }
    public void SetRemove() => palette.SelectRemove();
    public void ClearMode()
    {
        palette.ClearMode();
    }

    // 재배치 모드는 유지한 채, 집은 유닛만 원래 자리로 되돌린다(집기 취소).
    public void CancelHold()
    {
        if (_replace != null && _replace.IsHolding)
        {
            _replace.ReturnHeld();
        }
    }
    public void SetBlock(bool value) => input.SetBlock(value);

    // ---- 스킬 시전 취소(ESC가 BuildModePanel을 통해 부른다) ----

    public bool HasArmedOrSelectedSkill =>
        (skillCast != null && skillCast.SelectedCaster != null) ||
        (playerSkillCast != null && playerSkillCast.Armed != null);

    public void CancelSkillCasts()
    {
        skillCast?.ClearSelection();
        playerSkillCast?.ClearArmed();
    }
}
