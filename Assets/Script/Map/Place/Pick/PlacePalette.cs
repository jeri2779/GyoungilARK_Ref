using System;
using UnityEngine;

// 현재 배치 대상(로스터 엔트리)과 모드를 보관한다.
public class PlacePalette : MonoBehaviour
{
    private PlaceMode _mode = PlaceMode.Off;
    private HeroRosterEntry _runtimeEntry;
    private HeroRoster _roster;

    // MapAssemble이 조립할 때 넣어준다.
    public DayNightBuildRule dayNightRule;

    // 모드가 바뀔 때마다 알린다(UI가 매 프레임 폴링하지 않게).
    public event Action OnModeChanged;

    // MapAssemble이 조립할 때 한 번 불러준다.
    public void Bind(HeroRoster roster)
    {
        if (_roster != null) _roster.Changed -= ValidateRuntimeEntry;
        _roster = roster;
        _roster.Changed += ValidateRuntimeEntry;
    }

    private void OnDestroy()
    {
        if (_roster != null) _roster.Changed -= ValidateRuntimeEntry;
    }

    // 로스터가 바뀔 때(합성 등으로 엔트리가 사라졌을 때) 지금 배치 대기 중인 엔트리가
    // 더 이상 로스터에 없으면 배치모드를 꺼서 죽은 엔트리로 유닛이 생기는 걸 막는다.
    private void ValidateRuntimeEntry()
    {
        if (_runtimeEntry != null && !_roster.Contains(_runtimeEntry))
        {
            ClearMode();
        }
    }

    public PlaceMode Mode
    {
        get { return _mode; }
    }

    public HeroRosterEntry CurrentRuntimeEntry
    {
        get { return _runtimeEntry; }
    }

    // 현재 슬롯을 돌려준다(엔트리가 있다는 가정). 검사는 TryCurrentSlot이 한다.
    public Placeable CurrentSlot()
    {
        return _runtimeEntry.Slot;
    }

    // 배치 대상 엔트리가 있으면 슬롯을 내주고 true(검사 담당 게이트).
    public bool TryCurrentSlot(out Placeable slot)
    {
        if (_runtimeEntry == null) { slot = null; return false; }
        slot = _runtimeEntry.Slot;
        return true;
    }

    // 로스터 엔트리를 현재 배치 대상으로 선택한다(생성 직후 또는 이미 생성된 영웅의 재배치).
    // 밤에는 배치 자체가 막히므로 미리보기 상태로도 들어가지 않는다.
    public void SelectRuntimeSlot(HeroRosterEntry entry)
    {
        if (dayNightRule != null && !dayNightRule.CanBuild()) return;
        _runtimeEntry = entry;
        SetMode(PlaceMode.Place);
    }

    public void SelectReplace()
    {
        _runtimeEntry = null;
        SetMode(PlaceMode.Replace);
    }

    public void SelectRemove()
    {
        _runtimeEntry = null;
        SetMode(PlaceMode.Remove);
    }

    public void ClearMode()
    {
        _runtimeEntry = null;
        SetMode(PlaceMode.Off);
    }

    private void SetMode(PlaceMode mode)
    {
        _mode = mode;
        OnModeChanged?.Invoke();
    }
}
