using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 이어하기·이전 일차·취소 3가지 로드 방식을 고르는 팝업.
public class LoadOptionPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text continueText;
    [SerializeField] private TMP_Text pastDayText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button pastDayButton;
    [SerializeField] private Button cancelButton;

    private Action onContinue;
    private Action onPastDay;
    private ClickOutsideCloser outsideCloser;

    // 버튼 3개에 실행 메서드를 연결한다.
    private void Awake()
    {
        continueButton.onClick.AddListener(OnContinueClicked);
        pastDayButton.onClick.AddListener(OnPastDayClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        pastDayButton.gameObject.SetActive(false);
        outsideCloser = new ClickOutsideCloser((RectTransform)transform);
    }

    // 언어 변경 시 버튼 문구를 다시 표시한다.
    private void OnEnable()
    {
        LocalizeTextManager.OnLanguageChanged += RefreshText;
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
        outsideCloser.MarkOpened();
    }

    // 비활성화 시 언어 변경 구독을 해제한다.
    private void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= RefreshText;
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;
    }

    // ESC와 바깥 클릭 모두 취소 버튼과 동일하게 처리한다.
    private void HandleCloseCheck()
    {
        if (outsideCloser.ShouldClose()) Cancel();
    }

    // 이어하기·이전 일차 콜백을 갈아끼워 팝업을 띄운다.
    public void ShowPopup(Action onContinue, Action onPastDay)
    {
        this.onContinue = onContinue;
        this.onPastDay = onPastDay;
        RefreshText();
        gameObject.SetActive(true);
        outsideCloser.MarkOpened();
    }

    // ESC로도 취소 버튼과 동일하게 닫을 수 있게 외부에 열어준다.
    public void Cancel()
    {
        OnCancelClicked();
    }

    // 현재 언어로 버튼 문구를 갱신한다.
    private void RefreshText()
    {
        StringTable table = DataTableManager.StringTable;
        titleText.text = table.Get("Ui_LoadOptionPanelTitle");
        continueText.text = table.Get("Ui_LoadOptionContinue");
        pastDayText.text = table.Get("Ui_LoadOptionPastDay");
    }

    // 이어하기 콜백을 1회 실행하고 닫는다.
    private void OnContinueClicked()
    {
        Action callback = onContinue;
        HidePopup();
        callback.Invoke();
    }

    // 이전 일차 콜백을 1회 실행하고 닫는다.
    private void OnPastDayClicked()
    {
        Action callback = onPastDay;
        HidePopup();
        callback.Invoke();
    }

    // 아무 것도 실행하지 않고 닫는다.
    private void OnCancelClicked()
    {
        HidePopup();
    }

    // 저장된 콜백을 비우고 팝업을 닫는다.
    private void HidePopup()
    {
        onContinue = null;
        onPastDay = null;
        gameObject.SetActive(false);
    }
}
