using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

// 기믹 타일 클릭 시 등장해 잠시 머물다 자동으로 닫히는 슬라이드 팝업.
[RequireComponent(typeof(Animator))]
public class GimmickPopup : MonoBehaviour
{
    private const float HoldSeconds = 3f;

    private static readonly int InHash = Animator.StringToHash("GimmickIn");
    private static readonly int OutHash = Animator.StringToHash("GimmickOut");

    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;

    private Animator slideAnim;
    private CancellationTokenSource closeTimer;

    // 같은 오브젝트의 Animator를 보관한다.
    private void Awake()
    {
        slideAnim = GetComponent<Animator>();
    }

    // 이름·설명을 채운 뒤 패널을 연다.
    public void Show(string nameKey, string descKey)
    {
        nameText.text = DataTableManager.StringTable.Get(nameKey);
        descText.text = DataTableManager.StringTable.Get(descKey);
        Show();
    }

    // 패널을 열고, 일정 시간 뒤 자동으로 닫히는 타이머를 건다.
    public void Show()
    {
        gameObject.SetActive(true);
        slideAnim.Play(InHash, 0, 0f);
        RestartCloseTimer();
    }

    // 퇴장 애니메이션을 재생한다.
    private void ClosePanel()
    {
        slideAnim.Play(OutHash, 0, 0f);
    }

    // 닫기 애니메이션이 끝난 패널을 비활성화한다(Animation Event가 호출).
    public void FinishClose()
    {
        gameObject.SetActive(false);
    }

    // 새로 뜬 시각부터 지정 시간 뒤 자동으로 닫히도록 타이머를 다시 건다.
    private void RestartCloseTimer()
    {
        CancelCloseTimer();
        closeTimer = new CancellationTokenSource();
        WaitAndCloseAsync(closeTimer.Token).Forget();
    }

    // 기존 타이머가 있으면 취소한다(다시 열렸을 때 중복 방지).
    private void CancelCloseTimer()
    {
        if (closeTimer == null)
        {
            return;
        }
        closeTimer.Cancel();
        closeTimer.Dispose();
        closeTimer = null;
    }

    // 지정 시간을 기다렸다가 패널을 닫는다.
    private async UniTaskVoid WaitAndCloseAsync(CancellationToken token)
    {
        bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(HoldSeconds), cancellationToken: token).SuppressCancellationThrow();
        if (cancelled)
        {
            return;
        }
        ClosePanel();
    }

    // 오브젝트가 파괴될 때 대기 중인 타이머를 정리한다.
    private void OnDestroy()
    {
        CancelCloseTimer();
    }
}
