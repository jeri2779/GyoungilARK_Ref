// 타이틀의 "새 게임" 진입 시 튜토리얼 선택지(하기/건너뛰기)를 고른 결과를 MainScene으로 넘긴다.
// 새로 고른 슬롯은 아직 저장 파일이 없거나(또는 이전에 그 슬롯을 쓰던 다른 세이브의 오래된
// 파일만 있어) TutorialState가 세이브 파일에서 곧바로 정확한 값을 읽을 수 없다 - 그래서 타이틀에서
// 고른 값을 씬 전환 동안만 들고 있다가, MainScene의 TutorialState 생성자가 파일보다 우선해서 확인한다.
// SelectedSaveSlot과 같은 컨벤션(정적 필드로 씬 경계를 넘겨줌)을 따른다.
public static class TutorialEntryChoice
{
    public static bool? SkipTutorial { get; private set; }

    public static void Set(bool skip) => SkipTutorial = skip;

    // TutorialState 생성자가 한 번 소비하고 나면 지운다 - 다음 로드/새 게임 진입 때 잘못 들러붙지 않게.
    public static void Clear() => SkipTutorial = null;
}
