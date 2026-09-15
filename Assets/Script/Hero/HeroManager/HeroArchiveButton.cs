using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HeroArchiveButton : MonoBehaviour, IExclusiveUiPanel
{
    [SerializeField] private GameObject archiveUI;
    [SerializeField] private Key openKey = Key.P;
    [Tooltip("페이지 UI 컴포넌트. 비워두면 archiveUI 하위에서 찾는다.")]
    [SerializeField] private HeroArchiveUI heroArchiveUI;
    [SerializeField] private GameObject basePanel;
    private CancellationTokenSource cts;
    private bool isOpen;
    private ClickOutsideCloser outsideCloser;
    private Keyboard keyboard;

    public bool IsOpen => isOpen;

    private void Start()
    {
        archiveUI.SetActive(false);
        archiveUI.transform.localScale = Vector3.zero; // 닫힘 = 스케일 0 기준 (이어서 열기 진행도 계산용)
        isOpen = false;
        outsideCloser = new ClickOutsideCloser((RectTransform)archiveUI.transform, transform);
        keyboard = Keyboard.current;
    }

    private void Update()
    {
        if (isOpen && outsideCloser.ClickedOutside()) Close();

        if (keyboard == null) return;

        if (isOpen && keyboard.escapeKey.wasPressedThisFrame && !TutorialInputGate.BlockEscapeClose)
        {
            Close();
        }

        if (keyboard[openKey].wasPressedThisFrame && !TutorialInputGate.BlockHotkeys && !basePanel.activeSelf)
        {
            if (archiveUI.activeSelf)
                Close();
            else
                Open();
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
        ExclusiveUiCoordinator.NotifyClosed(this);
    }

    public void RequestClose() => Close();

    public void OnClick()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        outsideCloser.MarkOpened();
        ExclusiveUiCoordinator.NotifyOpened(this);
        ResetCts();
        OpenArchiveCor(cts.Token).Forget();

        if (TryGetArchiveUI(out HeroArchiveUI ui)) ui.Refresh();
    }

    // archiveUI 하위의 HeroArchiveUI를 늦게 찾아 캐시한다. 인스펙터에 안 꽂아도 동작하게.
    private bool TryGetArchiveUI(out HeroArchiveUI ui)
    {
        if (heroArchiveUI == null) heroArchiveUI = archiveUI.GetComponentInChildren<HeroArchiveUI>(true);
        ui = heroArchiveUI;
        return ui != null;
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        ExclusiveUiCoordinator.NotifyClosed(this);
        ResetCts();
        CloseArchiveCor(cts.Token).Forget();
    }

    // 진행 중이던 애니메이션 취소 + 새 토큰 발급
    private void ResetCts()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
    }

    private async UniTask OpenArchiveCor(CancellationToken token)
    {
        archiveUI.SetActive(true);
        float t = Progress01(archiveUI.transform.localScale); // 현재 스케일에서 이어서 열기(연타 시 튐 방지)
        float speed = 5f;
        EnemySoundManager.Play("BookOpen");
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * speed;
            archiveUI.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            await UniTask.Yield(token);
        }
    }

    private async UniTask CloseArchiveCor(CancellationToken token)
    {
        float t = 1f - Progress01(archiveUI.transform.localScale);
        float speed = 5f;
        EnemySoundManager.Play("BookClose");
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * speed;
            archiveUI.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            await UniTask.Yield(token);
        }
        archiveUI.SetActive(false);
    }

    // 현재 스케일이 0~1 열림 진행도의 어디쯤인지(연타/중간취소 시 이어서 애니메이션)
    private static float Progress01(Vector3 scale)
        => Mathf.Clamp01(scale.x);
}
