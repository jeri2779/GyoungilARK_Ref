using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 마우스 눌림/뗌을 받아 현재 모드에 맞는 동작을 고른다. 스스로 일하지 않고 PlaceAction에 시킨다.
public class MapCommand : MonoBehaviour
{
    [SerializeField] private MapInput input;
    [SerializeField] private PlacePalette palette;

    // MapAssemble이 조립할 때 넣어준다
    public PointerPick pointerPick;
    public DragDetect dragDetect;
    public DragDetect rightDragDetect;
    public UnitReplace replace;
    public PlaceAction action;
    public PlaceGhost ghost;
    public HoverPlaceData hoverPlace;
    public GimmickPopup gimmickPopup;
    public Dictionary<PlaceMode, Action<Tile>> dispatch;

    private void OnEnable()
    {
        input.Pressed += OnPress;
        input.Released += OnRelease;
        input.RightPressed += OnRightPress;
        input.RightReleased += OnRightRelease;
    }

    private void OnDisable()
    {
        input.Pressed -= OnPress;
        input.Released -= OnRelease;
        input.RightPressed -= OnRightPress;
        input.RightReleased -= OnRightRelease;
    }

    private void Update()
    {
        FollowHeld();
        FollowGhost();
    }

    // 배치 모드면 미리보기를 커서 자리에 세우고, 아니면 감춘다.
    // 자리 계산은 TilePaintView가 이미 이번 프레임에 끝내고 hoverPlace에 남겼다 — 여긴 그 결과만 읽는다.
    private void FollowGhost()
    {
        if (!IsPlacing())
        {
            ghost.HideGhost();
            return;
        }

        // 프리팹 없는 슬롯은 미리보기를 만들 수 없다(배치를 시도하면 UnitPlacer가 알린다).
        if (!hoverPlace.HasData || !palette.TryCurrentSlot(out Placeable slot) || slot.prefab == null)
        {
            ghost.HideGhost();
            return;
        }

        ghost.ShowGhost(slot.prefab, hoverPlace.Data);
    }

    private bool IsPlacing()
    {
        return !input.Blocked && palette.Mode == PlaceMode.Place;
    }

    // 재배치 모드면 재배치 입력을, 아니면 현재 모드의 기능을 실행한다.
    private void OnPress()
    {
        dragDetect.MarkPress();
        Tile tile = pointerPick.UnderPointer();

        NotifyGimmickTile(tile);

        if (palette.Mode == PlaceMode.Replace)
        {
            ReplacePress(tile);
            return;
        }

        if (tile == null)
        {
            action.skillCast?.ClearSelection(); // 타일 밖 클릭 = 스킬 시전 취소
            return;
        }

        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        dispatch[palette.Mode](tile);
    }

    // 어떤 모드든 상관없이 기믹이 걸린 타일을 눌렀으면 팝업을 띄운다.
    private void NotifyGimmickTile(Tile tile)
    {
        if (tile == null)
        {
            return;
        }
        if (!GimmickTileCalc.IsGimmickTile(tile))
        {
            return;
        }
        gimmickPopup.Show(GimmickKeyCalc.MakeNameKey(tile), GimmickKeyCalc.MakeDescKey(tile));
    }

    // 제자리 우클릭(뗄 때까지 거의 안 움직임)이면 스킬 시전 취소. 우클릭 드래그는 카메라 팬/회전이라 무시한다.
    private void OnRightPress()
    {
        rightDragDetect.MarkPress();
    }

    private void OnRightRelease()
    {
        if (rightDragDetect.MovedEnough()) return;
        action.skillCast?.ClearSelection();
        action.playerSkillCast?.ClearArmed();
    }

    private void ReplacePress(Tile tile)
    {
        if (replace.IsHolding)
        {
            action.Drop(HeldData());
            return;
        }

        if (tile == null)
        {
            return;
        }

        if (tile.HasUnit)
        {
            action.PickUpUnit(tile);
            return;
        }

        action.SelectTile(tile);
    }

    // 떼는 순간: 집은 채 드래그였다면 목표 타일에 내려놓는다(제자리 클릭이면 집은 채 유지).
    private void OnRelease()
    {
        if (palette.Mode != PlaceMode.Replace)
        {
            return;
        }

        if (!replace.IsHolding)
        {
            return;
        }

        if (!IsDrag(pointerPick.UnderPointer()))
        {
            return;
        }

        action.Drop(HeldData());
    }

    // 집은 유닛 프리뷰가 목표 자리 한가운데를 따라가게 한다.
    // 자리 계산은 TilePaintView가 이미 이번 프레임에 끝내고 hoverPlace에 남겼다 — 여긴 그 결과만 읽는다.
    private void FollowHeld()
    {
        if (palette.Mode != PlaceMode.Replace)
        {
            return;
        }

        if (!replace.IsHolding)
        {
            return;
        }

        if (hoverPlace.HasData)
        {
            replace.MoveHeldTo(hoverPlace.Data);
        }
    }

    // 미리보기가 이번 화면에 이미 구해서 hoverPlace에 남겨둔 자리를 그대로 쓴다.
    private PlaceData HeldData()
    {
        return hoverPlace.Data;
    }

    // 다른 타일 위에서 뗐거나 화면상 충분히 움직였으면 드래그로 본다(제자리 클릭과 구분).
    private bool IsDrag(Tile releaseTile)
    {
        if (releaseTile != null && releaseTile != replace.HeldFromTile)
        {
            return true;
        }

        return dragDetect.MovedEnough();
    }
}
