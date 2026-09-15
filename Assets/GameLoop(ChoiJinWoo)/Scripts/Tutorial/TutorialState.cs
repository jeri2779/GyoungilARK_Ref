// 튜토리얼 진행 상태(이 세이브 슬롯에서 튜토리얼을 이미 봤는지). 예전엔 PlayerPrefs(전역)에
// 저장해서, 튜토리얼 도중 타이틀로 나가 이미 튜토리얼을 끝낸 다른 세이브를 불러와도 "봤음"이
// 그대로 남아 있었다(반대로 안 끝낸 세이브를 불러왔는데 다른 슬롯에서 끝내서 다시 재생 안 되는
// 문제도 마찬가지). 이제 SaveData.tutorialSeen에 실려 세이브 슬롯별로 저장된다
// (SaveCapture.CaptureSaveData/SaveRestore.RestoreSaveData 참고).
//
// TutorialManager.Start()는 LoadManager의 실제 복원(다음 프레임)보다 먼저, 씬이 켜지는 바로 그
// 프레임에 이 값을 확인해야 한다 - 그래서 LoadManager를 기다리지 않고, 생성 시점(다른 컴포넌트의
// Start()보다 앞선 Awake/주입 단계)에 저장 슬롯 파일을 직접 한 번 읽어 초기값을 정한다. 새
// 게임이면 항상 false.
public class TutorialState
{
    public bool Seen { get; private set; }

    public TutorialState(SaveSlot saveSlot)
    {
        // 타이틀의 새 게임 튜토리얼 선택지("하기"/"건너뛰기")가 이번 진입에 값을 정해뒀다면 그걸
        // 최우선으로 따른다 - 새 슬롯은 세이브 파일이 아직 없거나 이전 사용자의 오래된 파일만 있어
        // 파일만으로는 그 선택을 반영할 수 없다. TitleUI.OnStartWithTutorial/OnSkipTutorial 참고.
        if (TutorialEntryChoice.SkipTutorial.HasValue)
        {
            Seen = TutorialEntryChoice.SkipTutorial.Value;
            TutorialEntryChoice.Clear();
            return;
        }

        Seen = !SelectedSaveSlot.IsNewGame
            && saveSlot.TryReadLatest(SelectedSaveSlot.SlotId, out SaveFile file)
            && file.saveData.tutorialSeen;
    }

    public void MarkSeen()
    {
        Seen = true;
        // 실제 파일 저장은 여기서 안 한다 - TutorialManager가 이 직후에 부르는
        // SaveManager.SaveNow() -> SaveCapture.CaptureSaveData()가 이 값을 읽어가 세이브 데이터에 실어준다.
    }

    // LoadManager가 세이브 데이터를 실제로 적용할 때 다시 한 번 맞춰준다 - 생성자의 초기 추정치와
    // 어긋날 일은 지금은 없지만(TryReadLatest로 같은 최신 저장본을 읽음), 나중에 일차별 저장을
    // 다시 켜서 과거 일차를 불러오는 경우가 생기면 그때는 이 값이 최종 진실이 된다.
    public void RestoreSeen(bool seen)
    {
        Seen = seen;
    }

    // 테스트용 - TutorialManager.DebugRestart()에서 인스펙터 우클릭으로 다시 볼 때 쓴다. 이후 실제
    // 저장은 여느 때처럼 MarkSeen() -> SaveNow() 흐름을 통해서만 이루어진다.
    public void Reset()
    {
        Seen = false;
    }
}
