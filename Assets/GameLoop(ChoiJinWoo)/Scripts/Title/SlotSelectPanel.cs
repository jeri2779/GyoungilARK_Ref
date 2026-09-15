using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬롯 패널에서 사용할 새 게임과 불러오기 모드를 구분한다.
public enum SlotSelectMode
{
    NewGame,
    Load
}

// 모드에 맞는 슬롯 목록과 확인·삭제 흐름을 관리한다.
public class SlotSelectPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform rowContainer;
    [SerializeField] private SlotRowView rowPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private ConfirmPopup confirmPopup;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform newGameButton; // 이 패널을 여는 "새 게임" 버튼
    [SerializeField] private RectTransform loadButton; // 이 패널을 여는 "불러오기" 버튼
    // 위 두 버튼은 alsoSelf로 넘겨야 열려있는 상태에서 다시 눌렀을 때 "바깥 클릭"으로 잡혀
    // Close()가 먼저 불리고 곧이어 OnNewGame/OnLoad의 토글 로직이 다시 여는 깜빡임이 안 생긴다.

    // 어떤 모드로 확인됐는지(새 게임/불러오기)를 같이 넘긴다 - TitleUI가 새 게임일 때만
    // 씬 전환 전에 튜토리얼 선택 화면을 끼워 넣어야 해서 구분이 필요하다.
    public event Action<SlotSelectMode> SlotConfirmed;
    public event Action SaveChanged;

    // LoadHeldLink/NewGameHeldLink가 "패널이 열려있고 + 지금 모드가 자기 것인지"를 매 프레임 폴링하는
    // 대신 구독할 수 있도록, 열림 여부/모드가 바뀌는 모든 지점(Open*/Close/삭제로 자동 닫힘)에서 발화한다.
    public event Action ModeChanged;

    private SlotPreviewReader previewReader;
    private readonly SlotDelete slotDelete = new SlotDelete();
    private readonly List<SlotRowView> spawnedRows = new List<SlotRowView>();

    private SlotSelectMode mode;
    public SlotSelectMode Mode => mode;
    private ClickOutsideCloser outsideCloser;

    // 닫기 버튼에 실행 메서드를 연결한다.
    private void Awake()
    {
        closeButton.onClick.AddListener(OnClose);
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, newGameButton, loadButton);
    }

    private void OnEnable()
    {
        outsideCloser.MarkOpened();
        GlobalUiInputSignals.ClickPerformed += HandleOutsideClick;
        GlobalUiInputSignals.EscapePerformed += HandleEscape;
    }

    private void OnDisable()
    {
        GlobalUiInputSignals.ClickPerformed -= HandleOutsideClick;
        GlobalUiInputSignals.EscapePerformed -= HandleEscape;
    }

    // TitleUI가 만든 리더를 그대로 받아 쓴다 (자기 것을 새로 안 만듦).
    public void SetPreviewReader(SlotPreviewReader reader)
    {
        previewReader = reader;
    }

    // 바깥 클릭 - 가장 위에 떠 있는 창 한 겹만 닫는다(확인 팝업이 떠 있으면 그것부터, 아니면 패널 자체를).
    private void HandleOutsideClick()
    {
        if (!outsideCloser.ClickedOutside()) return;

        if (confirmPopup.gameObject.activeSelf)
            confirmPopup.Cancel();
        else
            OnClose();
    }

    private void HandleEscape()
    {
        if (confirmPopup.gameObject.activeSelf)
            confirmPopup.Cancel();
        else
            OnClose();
    }

    // 새 게임 모드로 전체 슬롯 목록을 연다.
    public void OpenForNewGame()
    {
        mode = SlotSelectMode.NewGame;
        titleText.text = DataTableManager.StringTable.Get("Ui_NewGamePanelTitle");
        BuildRows(true);
        gameObject.SetActive(true);
        ResetScroll();
        ModeChanged?.Invoke();
    }

    // 불러오기 모드로 저장된 슬롯 목록만 연다.
    public void OpenForLoad()
    {
        mode = SlotSelectMode.Load;
        titleText.text = DataTableManager.StringTable.Get("Ui_LoadPanelTitle");
        BuildRows(false);
        gameObject.SetActive(true);
        ResetScroll();
        ModeChanged?.Invoke();
    }

    // 슬롯을 고르지 않고 패널과 열려있는 팝업을 모두 닫는다.
    private void OnClose()
    {
        confirmPopup.Cancel();
        gameObject.SetActive(false);
        ModeChanged?.Invoke();
    }

    // 현재 모드에 필요한 슬롯 행을 다시 만든다.
    private void BuildRows(bool includeEmpty)
    {
        ClearRows();

        for (int slotId = 1; slotId <= SaveSlotConfig.SlotCount; slotId++)
        {
            SlotPreviewInfo info = previewReader.ReadSaveSlot(slotId);
            if (!includeEmpty && !info.HasSave)
            {
                continue;
            }

            SlotRowView row = Instantiate(rowPrefab, rowContainer);
            bool canDelete = mode == SlotSelectMode.Load;
            row.BindSlot(slotId, info, canDelete, OnRowClicked, OnDeleteClicked);
            spawnedRows.Add(row);
        }
    }

    // 이전에 생성한 모든 슬롯 행을 제거한다.
    private void ClearRows()
    {
        for (int index = 0; index < spawnedRows.Count; index++)
        {
            Destroy(spawnedRows[index].gameObject);
        }

        spawnedRows.Clear();
    }

    // 목록의 스크롤 위치를 가장 위로 되돌린다.
    private void ResetScroll()
    {
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    // 슬롯 선택에 맞는 확인 팝업을 연다.
    private void OnRowClicked(int slotId, SlotPreviewInfo info)
    {
        if (mode == SlotSelectMode.Load)
        {
            ConfirmContinueLoad(slotId);
            return;
        }

        confirmPopup.ShowPopup(() => ConfirmSlot(slotId));
    }

    // 선택한 슬롯의 일차 목록 패널을 연다.
    // private void OpenDaySelect(int slotId)
    // {
    //     daySelectPanel.Open(slotId);
    // }

    // 이어하기 선택 시 최종 확인 팝업을 연다.
    private void ConfirmContinueLoad(int slotId)
    {
        confirmPopup.ShowPopup(() => ConfirmSlot(slotId));
    }

    // 슬롯 삭제 경고 팝업을 연다.
    private void OnDeleteClicked(int slotId)
    {
        confirmPopup.ShowPopup(() => DeleteSlot(slotId));
    }

    // 선택한 슬롯을 삭제하고 불러오기 목록을 갱신한다.
    private void DeleteSlot(int slotId)
    {
        if (!slotDelete.TryDelete(slotId))
        {
            Debug.LogError($"[SaveLoad] Slot {slotId} 삭제에 실패했습니다.");
            return;
        }

        previewReader.ClearCache();
        SaveChanged?.Invoke();
        BuildRows(false);

        if (spawnedRows.Count == 0)
        {
            gameObject.SetActive(false);
            ModeChanged?.Invoke();
            return;
        }

        ResetScroll();
    }

    // 확인된 슬롯을 새 게임 또는 불러오기 대상으로 저장한다.
    private void ConfirmSlot(int slotId)
    {
        if (mode == SlotSelectMode.Load)
        {
            SelectedSaveSlot.SetLoad(slotId);
        }
        else
        {
            SelectedSaveSlot.SetNewGame(slotId);
        }

        gameObject.SetActive(false);
        SlotConfirmed?.Invoke(mode);
        ModeChanged?.Invoke();
    }
}
