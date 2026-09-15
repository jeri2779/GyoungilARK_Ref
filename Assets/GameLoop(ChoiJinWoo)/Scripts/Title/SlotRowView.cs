using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬롯 한 줄의 정보 표시와 선택·삭제 입력을 담당한다.
public class SlotRowView : MonoBehaviour
{
    private const int SecondsPerHour = 3600;
    private const int SecondsPerMinute = 60;
    private const float LeftEnd = 0.40f;
    private const float HeroEnd = 0.62f;
    private const float DeleteRight = 0.88f;
    private const float FullRight = 1f;

    [SerializeField] private Button rowButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private TMP_Text deleteText;
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text saveTimeText;
    [SerializeField] private TMP_Text heroCountText;
    [SerializeField] private RectTransform dividerLeft;
    [SerializeField] private RectTransform dividerRight;

    private int slotId;
    private SlotPreviewInfo slotInfo;
    private Action<int, SlotPreviewInfo> onRowClicked;
    private Action<int> onDeleteClicked;

    // 슬롯 버튼과 삭제 버튼에 실행 메서드를 연결한다.
    private void Awake()
    {
        rowButton.onClick.AddListener(OnClicked);
        deleteButton.onClick.AddListener(OnDelete);
    }

    // 언어 변경 시 현재 슬롯 문구를 다시 표시한다.
    private void OnEnable()
    {
        LocalizeTextManager.OnLanguageChanged += RefreshText;
    }

    // 비활성화된 행의 언어 변경 구독을 해제한다.
    private void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= RefreshText;
    }

    // 슬롯 정보와 선택·삭제 동작을 받아 행을 준비한다.
    public void BindSlot(
        int slotId,
        SlotPreviewInfo info,
        bool canDelete,
        Action<int, SlotPreviewInfo> onRowClicked,
        Action<int> onDeleteClicked)
    {
        this.slotId = slotId;
        slotInfo = info;
        this.onRowClicked = onRowClicked;
        this.onDeleteClicked = onDeleteClicked;

        deleteButton.gameObject.SetActive(canDelete);
        ApplyColumnLayout(canDelete);
        RefreshText();
    }

    // 삭제 버튼 표시 여부에 맞춰 슬롯 행의 모든 칸 경계를 같은 비율로 재배치한다.
    private void ApplyColumnLayout(bool canDelete)
    {
        float rightEdge = GetRight(canDelete);
        float scale = rightEdge / DeleteRight;
        float leftEnd = LeftEnd * scale;
        float heroEnd = HeroEnd * scale;

        SetAnchorMaxX(slotText.rectTransform, leftEnd);
        SetAnchorMaxX(playTimeText.rectTransform, leftEnd);

        SetAnchorX(heroCountText.rectTransform, leftEnd, heroEnd);
        SetAnchorX(dividerLeft, leftEnd, leftEnd);
        SetAnchorX(dividerRight, heroEnd, heroEnd);

        SetAnchorMinX(dayText.rectTransform, heroEnd);
        SetAnchorMinX(saveTimeText.rectTransform, heroEnd);
        SetAnchorMaxX(dayText.rectTransform, rightEdge);
        SetAnchorMaxX(saveTimeText.rectTransform, rightEdge);
    }

    // 삭제 버튼 표시 여부에 맞는 정보 영역의 오른쪽 끝을 반환한다.
    private float GetRight(bool canDelete)
    {
        if (canDelete)
        {
            return DeleteRight;
        }

        return FullRight;
    }

    // RectTransform의 가로 시작 앵커만 갈아끼운다.
    private void SetAnchorMinX(RectTransform rect, float x)
    {
        rect.anchorMin = new Vector2(x, rect.anchorMin.y);
    }

    // RectTransform의 가로 끝 앵커만 갈아끼운다.
    private void SetAnchorMaxX(RectTransform rect, float x)
    {
        rect.anchorMax = new Vector2(x, rect.anchorMax.y);
    }

    // RectTransform의 가로 시작·끝 앵커를 한 번에 갈아끼운다.
    private void SetAnchorX(RectTransform rect, float min, float max)
    {
        rect.anchorMin = new Vector2(min, rect.anchorMin.y);
        rect.anchorMax = new Vector2(max, rect.anchorMax.y);
    }

    // 현재 언어로 슬롯의 모든 표시 문구를 갱신한다.
    private void RefreshText()
    {
        StringTable table = DataTableManager.StringTable;
        slotText.text = string.Format(table.Get("Ui_SlotLabel"), slotId);
        deleteText.text = table.Get("Ui_Delete");

        if (!slotInfo.HasSave)
        {
            SetEmptyText(table);
            return;
        }

        SetSaveText(table);
    }

    // 빈 슬롯에 새 게임 시작 문구만 표시한다.
    private void SetEmptyText(StringTable table)
    {
        dayText.text = table.Get("Ui_SlotEmptyAction");
        playTimeText.text = string.Empty;
        saveTimeText.text = string.Empty;
        heroCountText.text = string.Empty;
    }

    // 저장된 슬롯의 진행도·플레이 시간·저장 시각을 표시한다.
    private void SetSaveText(StringTable table)
    {
        dayText.text = string.Format(table.Get("Ui_SlotDay"), slotInfo.DayCount);
        playTimeText.text = string.Format(
            table.Get("Ui_PlayTimeFormat"),
            GetHours(slotInfo.PlayTime),
            GetMinutes(slotInfo.PlayTime));
        saveTimeText.text = string.Format(table.Get("Ui_SlotSaveTime"), SaveTimeCalc.FormatSaveTime(slotInfo.SaveTime));
        heroCountText.text = string.Format(table.Get("Ui_HeroCount"), slotInfo.HeroCount);
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

    // 슬롯 선택을 슬롯 번호와 함께 전달한다.
    private void OnClicked()
    {
        onRowClicked.Invoke(slotId, slotInfo);
    }

    // 슬롯 삭제 요청을 슬롯 번호와 함께 전달한다.
    private void OnDelete()
    {
        onDeleteClicked.Invoke(slotId);
    }
}
