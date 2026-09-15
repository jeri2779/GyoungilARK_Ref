using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

// 슬롯·저장 단계와 저장 호출 순서를 조정한다. 로드는 LoadManager가 전담한다.
// GameManager를 고치지 않기 위해, 이미 있는 ChangeToDay/ChangeToNight 이벤트를 구독만 해서 저장한다
// (대가: 저장이 실패해도 낮·밤 전환 자체는 막지 않는다 — 직전 정상 저장본은 그대로 안전하게 남는다).
public class SaveManager : IStartable
{


    private readonly SaveSlot saveSlot;
    private readonly SaveCapture saveCapture;
    private readonly GameManager gameManager;
    private readonly SaveTimeData saveTimeData;
    // 로드 복원 중인지 담는다
    private bool loadRestoring;

    public SaveManager(
        SaveSlot saveSlot,
        SaveCapture saveCapture,
        GameManager gameManager,
        SaveTimeData saveTimeData)
    {
        this.saveSlot = saveSlot;
        this.saveCapture = saveCapture;
        this.gameManager = gameManager;
        this.saveTimeData = saveTimeData;

        gameManager.ChangeToDay += OnDayTransitioned;
        gameManager.ChangeToNight += OnNightTransitioned;
    }

    // 새 슬롯 입장 다음 프레임에 최초 1일차 상태를 한 번 저장한다
    public void Start()
    {
        if (!SelectedSaveSlot.IsNewGame)
        {
            return;
        }

        SaveInitial().Forget();
    }

    // 씬의 Start 초기화가 끝난 다음 프레임까지 기다린 뒤 저장한다
    private async UniTaskVoid SaveInitial()
    {
        await UniTask.NextFrame();
        SaveInitialNow();
    }

    // 새 슬롯 최초 상태를 저장하고 파일 쓰기 실패를 알린다
    private void SaveInitialNow()
    {
        if (TutorialInputGate.BlockSave)
        {
            return;
        }

        if (!ToolEnabled)
        {
            Debug.LogError("[SaveLoad] 세이브 기능이 OFF라서 새 슬롯 최초 저장을 실행하지 못했습니다.");
            return;
        }

        bool saved = TrySave(SavePhase.DayStart, gameManager.DayCount, saveTimeData.DayList);
        if (!saved)
        {
            Debug.LogError("[SaveLoad] 새 슬롯 최초 저장 파일 작성 또는 검증에 실패했습니다.");
        }
    }

    // 새 일차로 전환된 직후(생산 전) 상태를 저장한다
    private void OnDayTransitioned()
    {
        int currentDayCount = gameManager.DayCount;
        TrySave(SavePhase.DayStart, currentDayCount, saveTimeData.DayList);

        // 일차별 저장(Save_Day_XX 아카이브 + savedDayList 갱신) 일단 중단
        // int[] appendedDayList = DayListCalc.AppendDay(saveTimeData.DayList, currentDayCount);
        // if (!TrySave(SavePhase.DayStart, currentDayCount, appendedDayList))
        // {
        //     return;
        // }
        // saveTimeData.SetDayList(appendedDayList);
        // bool archived = saveSlot.TryWriteDayArchive(SelectedSaveSlot.SlotId, currentDayCount);
        // LogFailure(archived, currentDayCount);
    }

    // 밤으로 전환된 직후(낮 준비가 끝난 최종) 상태를 저장한다
    private void OnNightTransitioned()
    {
        TrySave(SavePhase.NightReady, gameManager.DayCount, saveTimeData.DayList);
    }

    // 낮/밤 전환 이벤트가 아닌 시점에 명시적으로 한 번 저장해야 할 때 쓴다 - 튜토리얼 0일차 리셋이
    // 막 끝난 직후가 그 예다. ChangeToDay 시점엔 TutorialInputGate.BlockSave 때문에 저장이
    // 건너뛰어졌으니(0일차의 지워질 상태였으므로), 리셋이 끝나 진짜 깨끗한 1일차가 된 지금 한 번 더 찍는다.
    public void SaveNow()
    {
        TrySave(SavePhase.DayStart, gameManager.DayCount, saveTimeData.DayList);
    }

    // 로드 복원을 시작하며 파일 저장을 잠근다
    public void LockSaveForLoad()
    {
        loadRestoring = true;
    }

    // 로드 복원이 끝나 파일 저장 잠금을 푼다
    public void UnlockSaveForLoad()
    {
        loadRestoring = false;
    }

    // 낮 활동 상태를 저장한다
    public void SaveDayActive()
    {
        if (!NeedDayActiveSave())
        {
            return;
        }

        bool saved = TrySave(SavePhase.DayActive, gameManager.DayCount, saveTimeData.DayList);
        LogDayActiveFailure(saved);
    }

    // 낮 활동 저장이 필요한 상태인지 계산한다
    private bool NeedDayActiveSave()
    {
        if (gameManager.isGameOver)
        {
            return false;
        }

        if (TutorialInputGate.BlockSave)
        {
            return false;
        }

        if (!ToolEnabled)
        {
            return false;
        }

        return gameManager.CanBuild;
    }

    // 낮 활동 저장 실패를 로그로 알린다
    private void LogDayActiveFailure(bool saved)
    {
        if (saved)
        {
            return;
        }

        Debug.LogError("[SaveLoad] 낮 활동 상태 저장에 실패했습니다.");
    }

    private bool TrySave(SavePhase phase, int dayCount, int[] savedDayList)
    {
        if (loadRestoring)
        {
            return false;
        }

        if (!ToolEnabled)
        {
            return false;
        }

        // 0일차 튜토리얼 연습 상태는 다음 날이 되는 순간 전부 초기화되는 임시 데이터라 저장하지 않는다.
        if (TutorialInputGate.BlockSave)
        {
            return false;
        }

        SaveData data = saveCapture.CaptureSaveData(phase, dayCount, saveTimeData.PlayTime, savedDayList);
        SaveFile file = new SaveFile();
        file.fileTag = SaveCheck.FileTag;
        file.saveVersion = SaveCheck.SaveVersion;
        file.saveData = data;

        return saveSlot.TryWrite(SelectedSaveSlot.SlotId, file);
    }

    // 일차 파일 복사 실패를 로그로 알린다 (일차별 저장 중단으로 미사용)
    // private void LogFailure(bool archived, int dayCount)
    // {
    //     if (archived) return;
    //
    //     Debug.LogError($"[SaveLoad] {dayCount}일차 파일 복사에 실패했습니다.");
    // }

    #region Save Tools

    public const string ToolKey = "SaveLoad.ToolEnabled";

    // 개발자 Tools 메뉴에서 지정한 세이브 활성 상태를 반환한다.
    public static bool ToolEnabled
    {
        get
        {
#if UNITY_EDITOR
            return PlayerPrefs.GetInt(ToolKey, 1) == 1;
#else
            return true;
#endif
        }
    }

    #endregion
}
