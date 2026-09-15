using System;
using UnityEngine;
using UnityEngine.UI;

// 팝업 오브젝트 1개를 확인 콜백만 갈아끼워 재사용한다 (문구는 LocalizeText가 담당).
public class ConfirmPopup : MonoBehaviour, IExclusiveUiPanel
{
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private PanelReveal panelReveal;

    private Action onConfirmed;

    // 확인 버튼과 취소 버튼에 실행 메서드를 연결한다.
    private void Awake()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
    }

    // 설정/업그레이드/종료 확인 등 다른 타이틀 오버레이와 동시에 겹쳐 뜨면 안 된다.
    private void OnEnable() => ExclusiveUiCoordinator.NotifyOpened(this);
    private void OnDisable() => ExclusiveUiCoordinator.NotifyClosed(this);

    // ExclusiveUiCoordinator가 다른 배타 패널이 열렸을 때 이 패널을 닫으라고 부르는 창구.
    public void RequestClose() => Cancel();

    // 확인 콜백만 갈아끼워 팝업을 띄운다 (호출한 패널보다 항상 위에 보이게 그리기 순서를 맨 뒤로 보낸다).
    public void ShowPopup(Action onConfirmed)
    {
        this.onConfirmed = onConfirmed;
        transform.SetAsLastSibling();
        panelReveal.Show();
    }

    // 확인 버튼: 콜백을 1회 실행하고 닫는다.
    private void OnConfirm()
    {
        Action callback = onConfirmed;
        HidePopup();
        callback.Invoke();
    }

    // 취소 버튼: 아무 것도 실행하지 않고 닫는다.
    private void OnCancel()
    {
        HidePopup();
    }

    // ESC로도 취소 버튼과 동일하게 취소할 수 있게 외부에 열어준다.
    public void Cancel()
    {
        OnCancel();
    }

    // 저장된 동작을 비우고 팝업을 닫는다.
    private void HidePopup()
    {
        onConfirmed = null;
        panelReveal.Hide();
    }
}
