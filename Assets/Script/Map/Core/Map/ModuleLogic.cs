using System;
using UnityEngine;


// <summary>
/// 모듈의 활성화 생명주기 상태만 담당
/// 책임: 상태 보유 + 전이 + 변경 알림.  
/// </summary>
public class ModuleLogic : MonoBehaviour
{

    //런타임 부여 시 SetModuleId.
    [SerializeField] private int moduleId;

    [SerializeField] private ModuleState _currentState = ModuleState.Locked;
    public int ModuleId => moduleId;

    public ModuleState CurrentState => _currentState;
    public bool IsUnlocked => _currentState != ModuleState.Locked;
    public bool IsPreparing => _currentState == ModuleState.Preparing;

    public event Action<ModuleState> OnStateChanged;
    //게임 중 새로 해금될 때만 알린다. 세이브 복원(SetState)에서는 울리지 않는다.
    public event Action<ModuleLogic> OnUnlocked;

    public void SetModuleId(int ModuleId)
    {
        moduleId = ModuleId;
    }
    //배치 가능 여부 전환(배치 가능 == 구역 해금으로 판단)
    public void SetState(ModuleState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        OnStateChanged?.Invoke(newState);
    }
    //구역을 새로 해금한다. 해금 전용 알림을 먼저 보낸 뒤 상태 변경 알림을 보낸다.
    public void Unlock()
    {
        if (IsUnlocked) return;

        _currentState = ModuleState.Preparing;
        OnUnlocked?.Invoke(this);
        OnStateChanged?.Invoke(_currentState);
    }
 
}

public enum ModuleState
{
    Locked,     // 데이터만/실루엣. 배치·전투 불가
    Preparing,  // 배치 가능, 적 미등장
    //Battle      // 현재 웨이브 전투 진행
}
