using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EnemyArchiveManager : MonoBehaviour, IExclusiveUiPanel
{
    // 스테이지 정보 팝업은 풀링 프리팹이라 씬 오브젝트를 인스펙터로 참조할 수 없다 → 런타임 창구.
    public static EnemyArchiveManager Instance { get; private set; }

    public GameObject archive;
    public GameObject guardPanal;
    public Button hidePanal;
    public Button infoOpenButton;
    public Button infoCloseButton;
    [Tooltip("적 목록 컴포넌트. 비워두면 archive 하위에서 찾는다.")]
    [SerializeField] private EnemyArchive archiveList;
    [Tooltip("여기 등록한 오브젝트가 하나라도 켜지면 도감을 닫는다. 다른 UI의 루트 패널을 넣으면 된다.")]
    [SerializeField] private GameObject[] closeWhenOpened;
    [SerializeField] private GameObject basepanal;

    private CancellationTokenSource cts;
    private bool isOpenCheck;
    // "열려는 의도". isOpenCheck는 열기 애니메이션이 끝나야 true가 되므로 토글 판단에 쓸 수 없다 —
    // 펼쳐지는 도중에 버튼을 다시 누르면 아직 false라 또 열기로 가서 안 닫힌다.
    // 이 값은 열기/닫기가 시작되는 순간 바로 뒤집혀서 애니메이션 어느 지점에서 눌러도 반대로 간다.
    private bool isOpenRequested;
    private Keyboard keyboard;
    private Key openKey = Key.O;

    // UiManager가 ESC로 메뉴를 열지 말지 판단할 때 쓴다 - 도감이 열려 있으면(닫히는 애니메이션
    // 도중 포함) 메뉴를 열지 않고 도감부터 닫아야 하므로.
    public bool IsOpen => isOpenCheck;

    void Awake() => Instance = this;

    // 도감을 열고 그 적 페이지를 띄운다. 이미 열려 있으면 페이지만 갈아끼운다.
    public void OpenAt(EnemyTable.Data data)
    {
        if (data == null || archive == null) return;

        // OpenArchiveCor는 첫 await 전까지 동기로 도므로 archive.SetActive(true)가 여기서 이미 끝난다.
        // → 그 뒤에 ShowEnemy를 불러야 EnemyArchive.OnEnable(Build) 다음 순서가 된다.
        // 보여줄 적이 이미 정해져 있으니 마지막 페이지 복원은 건너뛴다(두 번 펼치지 않게).
        if (!isOpenCheck) OpenArchive(restoreLastPage: false);

        if (!TryGetArchiveList(out EnemyArchive list)) return;
        list.ShowEnemy(data);
    }

    // archive 하위의 EnemyArchive를 늦게 찾아 캐시한다. 인스펙터에 안 꽂아도 동작하게.
    private bool TryGetArchiveList(out EnemyArchive list)
    {
        if (archiveList == null) archiveList = archive.GetComponentInChildren<EnemyArchive>(true);
        list = archiveList;
        if (list != null) return true;

        Debug.LogWarning("EnemyArchiveManager: archive 하위에 EnemyArchive가 없어 페이지를 띄울 수 없습니다.", this);
        return false;
    }

    void Start()
    {
        keyboard = Keyboard.current;
        archive.SetActive(false);
        archive.transform.localScale = Vector3.zero; // 닫힘 = 스케일 0 기준 (resume 로직이 진행도를 스케일로 읽음)
        isOpenCheck = false;
        isOpenRequested = false;
        guardPanal.SetActive(false);
        hidePanal.gameObject.SetActive(false);
        ResetCts();
        infoOpenButton.onClick.AddListener(OnClickOpenArchive);
        infoCloseButton.onClick.AddListener(OnClickCloseArchive); // 닫기 버튼: 튜토리얼 중에도 항상 동작
        hidePanal.onClick.AddListener(OnClickOutsideClose);        // 바깥 클릭: ESC와 동일하게 튜토리얼 가드 적용, 여기서 한 번만 등록(열 때마다 누적 방지)
        // 버튼 라벨을 코드가 채우므로 LocalizeText가 없다 → 언어 전환 이벤트를 직접 받아 갱신한다.

    }

    // 진행 중이던 애니메이션 취소 + 새 토큰 발급
    private void ResetCts()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
    }
    private void Update()
    {
        OnEscInput();
        CloseIfOtherUiOpened();

        if (keyboard == null) return;
        if (basepanal != null&&basepanal.activeSelf==true)
        {
            return;
        }
        if (keyboard[openKey].wasPressedThisFrame && !TutorialInputGate.BlockHotkeys)
        {
            // 버튼을 그대로 Invoke하는 이유: 인스펙터에 손으로 걸어둔 onClick 항목(튜토리얼 훅 등)도
            // 단축키에서 똑같이 돌아야 한다. 판단은 archiveList.activeSelf가 아니라 isOpenRequested로 —
            // archiveList는 인스펙터 미할당 시 null이라 여기서 NRE가 났고, 자식 오브젝트의 activeSelf는
            // 도감 열림 상태와 애초에 무관하다(껐다 켜는 건 archive 루트다).
            if (isOpenRequested)
                infoCloseButton.onClick?.Invoke();
            else
                infoOpenButton.onClick?.Invoke();
        }
    }

    private void CloseIfOtherUiOpened()
    {
        if (!isOpenCheck) return;               // 완전히 열려 있을 때만 본다
        if (closeWhenOpened == null) return;

        for (int i = 0; i < closeWhenOpened.Length; i++)
        {
            GameObject go = closeWhenOpened[i];
            if (go == null || !go.activeInHierarchy) continue;
            CloseArchive();
            return;
        }
    }

    public void CloseArchive()
    {
        if (!isOpenCheck) return;
        isOpenCheck = false;   // CloseArchiveCor가 끝나기 전에 매 프레임 다시 불리는 것을 막는다
        OnClickCloseArchive();
    }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
        ExclusiveUiCoordinator.NotifyClosed(this);
    }

    public void RequestClose() => CloseArchive();
    private void OnEscInput()
    {
        if (Keyboard.current == null) return;
        // 여기서만 Time.timeScale==0일 때 건너뛰면, 튜토리얼의 pauseTimeWhileActive 단계나
        // 완료 메시지(ShowCompletionMessage)처럼 배속을 0으로 걸어두는 동안 도감이 열려 있으면
        // ESC로 못 닫는다 - 같은 파일의 닫기 버튼(infoCloseButton)/바깥 클릭(hidePanal)은 이 체크가
        // 없어 그때도 정상 작동하니, ESC만 막을 이유가 없다.
        if (TutorialInputGate.BlockEscapeClose) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame&&isOpenCheck)
            OnClickCloseArchive();   // 동일 닫기 창구 재사용
    }
    // 도감 버튼(과 O 키)이 쓰는 토글 창구 — 닫혀 있으면 열고, 열려 있으면 같은 버튼으로 닫는다.
    private void OnClickOpenArchive()
    {
        if (isOpenRequested) OnClickCloseArchive();
        else OpenArchive(restoreLastPage: true);   // 마지막으로 보던 페이지(없으면 기본 적)로 되돌린다
    }

    private void OpenArchive(bool restoreLastPage)
    {
        isOpenRequested = true;
        ExclusiveUiCoordinator.NotifyOpened(this);
        ResetCts();
        OpenArchiveCor(cts.Token).Forget();

        if (!restoreLastPage) return;
        if (TryGetArchiveList(out EnemyArchive list)) list.ShowLastOrDefault();
    }

    private void OnClickOutsideClose()
    {
        if (TutorialInputGate.BlockEscapeClose) return;
        OnClickCloseArchive();
    }
    // 버튼/판넬/ESC 공용 닫기 창구 — 진행 중이던 열기 코루틴을 취소하고 닫는다(동시 실행 방지)
    private void OnClickCloseArchive()
    {
        isOpenRequested = false;
        ExclusiveUiCoordinator.NotifyClosed(this);
        ResetCts();
        CloseArchiveCor(cts.Token).Forget();
    }
    private async UniTask OpenArchiveCor(CancellationToken token)
    {
        archive.SetActive(true);
        guardPanal.SetActive(true);
        float t = Progress01(archive.transform.localScale); // 현재 스케일에서 이어서 열기(연타 시 튐 방지)
        float speed = 5f;
        EnemySoundManager.Play("BookOpen");
        while(t<1f)
        {
            t+=Time.unscaledDeltaTime*speed;
            archive.transform.localScale = Vector3.Lerp(Vector3.zero,Vector3.one,t);
            await UniTask.Yield(token);
        }
        hidePanal.gameObject.SetActive(true);
        guardPanal.SetActive(false);
        isOpenCheck =true;
    }
    private async UniTask CloseArchiveCor(CancellationToken token)
    {
        guardPanal.SetActive(true);
        hidePanal.gameObject.SetActive(false);
        float t = 1f - Progress01(archive.transform.localScale); 
        float speed = 5f;
        EnemySoundManager.Play("BookClose");
        while(t<1f)
        {
            t+=Time.unscaledDeltaTime*speed;
            archive.transform.localScale = Vector3.Lerp(Vector3.one,Vector3.zero,t);
            await UniTask.Yield(token);
        }
        guardPanal.SetActive(false);
        archive.SetActive(false);
        isOpenCheck =false;
    }
    

    // 현재 스케일이 0~1 열림 진행도의 어디쯤인지(연타/중간취소 시 이어서 애니메이션)
    private static float Progress01(Vector3 scale)
        => Mathf.Clamp01(scale.x);
}