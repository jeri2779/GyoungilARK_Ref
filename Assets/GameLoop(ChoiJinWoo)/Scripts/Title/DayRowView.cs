using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 일차 한 줄의 정보 표시와 선택 입력을 담당한다.
public class DayRowView : MonoBehaviour
{
    private const int SecondsPerHour = 3600;
    private const int SecondsPerMinute = 60;

    [SerializeField] private Button rowButton;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text saveTimeText;
    [SerializeField] private TMP_Text heroCountText;

    private DayPreviewInfo dayInfo;
    private Action<int> onRowClicked;

    // 행 버튼에 실행 메서드를 연결한다.
    private void Awake()
    {
        rowButton.onClick.AddListener(OnClicked);
    }

    // 언어 변경 시 현재 일차 문구를 다시 표시한다.
    private void OnEnable()
    {
        LocalizeTextManager.OnLanguageChanged += RefreshText;
    }

    // 비활성화된 행의 언어 변경 구독을 해제한다.
    private void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= RefreshText;
    }

    // 일차 정보와 선택 동작을 받아 행을 준비한다.
    public void BindDay(DayPreviewInfo info, Action<int> onRowClicked)
    {
        dayInfo = info;
        this.onRowClicked = onRowClicked;
        RefreshText();
    }

    // 현재 언어로 일차 행의 표시 문구를 갱신한다.
    private void RefreshText()
    {
        StringTable table = DataTableManager.StringTable;
        dayText.text = string.Format(table.Get("Ui_DaySelectRow"), dayInfo.DayCount);
        playTimeText.text = string.Format(
            table.Get("Ui_PlayTimeFormat"),
            GetHours(dayInfo.PlayTime),
            GetMinutes(dayInfo.PlayTime));
        saveTimeText.text = string.Format(table.Get("Ui_SlotSaveTime"), SaveTimeCalc.FormatSaveTime(dayInfo.SaveTime));
        heroCountText.text = string.Format(table.Get("Ui_HeroCount"), dayInfo.HeroCount);
    }

    // 누적 플레이 시간에서 시간 단위를 구한다.
    private int GetHours(float playTime)
    {
        return (int)(playTime / SecondsPerHour);
    }

    // 누적 플레이 시간에서 분 단위를 구한다.
    private int GetMinutes(float playTime)
    {
        int remainder = (int)playTime % SecondsPerHour;
        return remainder / SecondsPerMinute;
    }

    // 일차 선택을 일차 번호와 함께 전달한다.
    private void OnClicked()
    {
        onRowClicked.Invoke(dayInfo.DayCount);
    }
}
