using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 구역 클릭 시 포탈 옆에 뜨는 스테이지 정보 팝업(월드 스페이스 캔버스)의 표시 담당.
// 기존에는 TMP_Text 한 개에 "적이름 x 마릿수"를 줄바꿈으로 이어붙였다.
// 이제 적 하나당 StageEnemyRow(아이콘 + x마릿수)를 한 줄씩 만들고,
// 그 줄에 마우스를 올리거나 클릭하면 이름 + 간단한 설명 툴팁을 띄운다.
//
// 프리팹(TextPrefabs.prefab) 루트에 붙인다. WaveSpawner가 Begin()/AddRow()로 채운다.
public class StageInfoView : MonoBehaviour, IStageTooltipHost
{
    [Tooltip("행들이 담길 부모 (Vertical Layout Group 권장)")]
    [SerializeField] private Transform rowContainer;
    [Tooltip("적 한 줄 프리팹 (StageEnemyRow 붙은 것)")]
    [SerializeField] private StageEnemyRow rowPrefab;
    [Tooltip("행 목록을 담은 ScrollRect. 비워두면 자식에서 자동으로 찾는다. 없으면 행 맞춤 로직 전체가 no-op.")]
    [SerializeField] private ScrollRect scroll;

    [Tooltip("패널을 닫는 버튼(선택). 없으면 스폰 칸 밖을 클릭하거나 밤이 되면 닫힌다.")]
    [SerializeField] private Button closeButton;

    [Header("행 맞춤")]
    [Tooltip("행이 창에 다 들어오도록 목록 전체를 축소한다. 끄면 넘칠 때 스크롤한다. " +
             "고정 UI 패널의 일반 ScrollView에서는 꺼두는 쪽이 맞다 — 켜면 셀 폭을 뷰포트에 맞춰 " +
             "1열 목록으로 강제하고, 스크롤 대신 내용을 축소해 버린다.")]
    [SerializeField] private bool fitAllRows = false;
    [Tooltip("행 하나의 기준 높이(축소 배율 1일 때). 행 프리팹 높이와 같게 둔다.")]
    [SerializeField] private float rowHeight = 50f;
    [Tooltip("축소 하한. 이보다 작아지면 글자를 못 읽으므로 여기서 멈추고, 남는 만큼만 스크롤한다.")]
    [SerializeField] private float minFitScale = 0.35f;
    [Tooltip("행을 배치하는 Grid Layout Group. 비워두면 rowContainer에서 찾는다.")]
    [SerializeField] private GridLayoutGroup rowGrid;

    [Header("툴팁")]
    [Tooltip("툴팁 패널 (켜고 끔). 행 위에 겹치지 않게 offset으로 밀어준다.")]
    [SerializeField] private GameObject tooltip;
    [Tooltip("툴팁에 띄울 적 아이콘(선택). 아이콘 PNG가 없는 적이면 자동으로 숨긴다.")]
    [SerializeField] private Image tooltipIcon;
    [SerializeField] private TMP_Text tooltipNameText;
    [SerializeField] private TMP_Text tooltipDescText;
    [Tooltip("툴팁의 '도감 열기' 버튼. 누르면 도감이 열리고 그 적 페이지가 뜬다. 없어도 동작한다.")]
    [SerializeField] private Button archiveButton;
    [Tooltip("특성 줄의 라벨 StringTable 키. Ui_Type=타입, Ui_Attribute=속성. 값은 EnemyTable.Attribute다.")]
    [SerializeField] private string attributeLabelKey = "Ui_Attribute";
    [Tooltip("마우스를 몇 초 올리고 있으면 뜨는지. 클릭은 이 지연 없이 즉시 뜬다.")]
    [SerializeField] private float hoverDelay = 0.2f;
    [Tooltip("행·툴팁 어디에도 커서가 없을 때 툴팁을 닫기까지의 여유. " +
             "행과 툴팁 사이 빈 공간을 지나 도감 버튼까지 갈 시간을 준다.")]
    [SerializeField] private float hideGrace = 0.3f;
    [Tooltip("툴팁을 행 기준 어디에 띄울지(화면 픽셀). x를 음수로 두면 행의 왼쪽에 뜬다.")]
    [SerializeField] private Vector3 tooltipOffset = new Vector3(120f, 0f, 0f);
    [Tooltip("화면 가장자리에서 최소 이만큼은 띄운다. 툴팁이 화면을 벗어나면 반대쪽으로 넘기거나 안으로 밀어넣는다.")]
    [SerializeField] private float screenMargin = 8f;

    [Header("미해금 처리")]
    [Tooltip("체크하면 도감에서 아직 만나지 않은 적은 이름/설명을 ???로 가린다. " +
             "스테이지 정보는 '오늘 밤 뭐가 오는지' 보고 대비하는 화면이라 기본값은 끔(그대로 공개).")]
    [SerializeField] private bool maskUnknownEnemy = false;
    [SerializeField] private string unknownName = "???";
    [SerializeField] private string unknownDesc = "???";

    private readonly List<StageEnemyRow> rows = new();
    private int used;                       // 이번 표시에 실제로 쓴 행 수
    private StageEnemyRow hoverRow;          // 마우스가 올라와 있는 행
    private StageEnemyRow shownRow;          // 툴팁이 떠 있는 행
    private float hoverTimer;
    private bool pointerOverTooltip;
    private bool hidePending;
    private float hideTimer;
    private float zoomScale = 1f;            // StageInfoFollow가 넣어주는 줌 배율(1 = 기준 거리)
    // 스탯 표시용 글로벌 일차. 패널을 켜는 SpawnerManager가 SetDay로 넣어준다.
    // VContainer로 GameManager를 직접 받지 않는 이유: 이 패널은 GameLifeTimeScope에 등록돼 있지 않고
    // (autoInjectGameObjects도 비어 있다) 비활성으로 시작하므로 [Inject]가 아예 호출되지 않는다.
    private int dayCount;

    void Awake()
    {
        // 인스펙터에 안 꽂아도 동작하게 자식에서 찾아둔다(프리팹 구조가 바뀌어도 조용히 죽지 않는다).
        if (scroll == null) scroll = GetComponentInChildren<ScrollRect>(true);
        if (rowGrid == null && rowContainer != null) rowContainer.TryGetComponent(out rowGrid);
        HideTooltip();
        DisableContainerRaycast();
        CacheTooltipRects();
        SetupTooltipInteraction();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
    }
    // 스탯 배율 계산에 쓸 글로벌 일차. Show() 직후 SpawnerManager가 호출한다.
    public void SetDay(int day) => dayCount = day;

    // 고정 UI 패널로 쓸 때의 열기/닫기. SpawnerManager가 부른다.
    // 예전처럼 풀에서 스폰/회수하지 않으므로 상태 초기화는 Begin()/Clear()가 전부 맡는다.
    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        Clear();                       // 켜져 있는 동안 정리해야 툴팁/행이 확실히 내려간다
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    // 툴팁을 '살아있게' 만드는 배선.
    // 예전엔 툴팁의 모든 Graphic 레이캐스트를 껐다(커서 밑에 걸리면 행의 Enter/Exit가 반복되는 깜빡임 방지).
    // 이제 도감 버튼을 눌러야 하므로 레이캐스트를 살려두고, 대신 '툴팁 위에 있으면 닫지 않기'로 깜빡임을 막는다.
    private void SetupTooltipInteraction()
    {
        if (tooltip == null) return;

        var pointer = tooltip.GetComponent<StageTooltipPointer>();
        if (pointer == null) pointer = tooltip.AddComponent<StageTooltipPointer>();
        pointer.Bind(this);

        if (archiveButton != null)
        {
            archiveButton.onClick.RemoveAllListeners();
            archiveButton.onClick.AddListener(OpenArchiveForShownRow);
        }
    }

    // 툴팁의 도감 버튼. 지금 보고 있는 적 페이지로 도감을 연다.
    private void OpenArchiveForShownRow()
    {
        if (shownRow == null || shownRow.Data == null) return;
        EnemyTable.Data data = shownRow.Data;   // ResetHover가 shownRow를 비우므로 먼저 챈다

        EnemyArchiveManager manager = EnemyArchiveManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("StageInfoView: 씬에 EnemyArchiveManager가 없어 도감을 열 수 없습니다.", this);
            return;
        }
        manager.OpenAt(data);
        ResetHover();   // 도감이 화면을 덮으므로 툴팁은 닫는다
    }

    // 툴팁 위에 커서가 올라왔는지(StageTooltipPointer가 알려준다).
    public void SetPointerOverTooltip(bool over)
    {
        pointerOverTooltip = over;
        if (over) CancelHide();
        else BeginHide();
    }

    // rowContainer는 행을 담기만 하는 껍데기다. 여기에 Image가 붙어 있으면(특히 전체 화면 스트레치)
    // 화면 전체의 클릭을 먹어 IsPointerOverGameObject()가 항상 true가 되고,
    // 그러면 MapCommand의 UI 위 클릭 체크가 막혀 타일·바닥 클릭이 전부 죽는다.
    // 자기 Graphic만 끈다 — 자식인 행(row)은 클릭 대상이라 건드리면 안 된다.
    private void DisableContainerRaycast()
    {
        if (rowContainer == null) return;
        if (rowContainer.TryGetComponent(out Graphic g)) g.raycastTarget = false;
    }

    // StageInfoFollow가 패널을 옮긴 직후 호출한다. 같은 오브젝트에 붙은 두 컴포넌트의
    // LateUpdate 순서는 보장되지 않으므로, 옮긴 쪽이 직접 알려줘야 한 프레임도 밀리지 않는다.
    public void RefreshTooltipPosition()
    {
        if (shownRow == null || tooltip == null) return;
        PlaceTooltip(shownRow, false);
    }

    // 줌에 따라 패널이 커지면 툴팁도 같이 커진다. 그런데 tooltipOffset은 화면 픽셀 상수라
    // 그대로 두면 확대할수록 행과의 간격이 상대적으로 좁아져 툴팁이 행 위로 올라탄다.
    // StageInfoFollow가 패널 크기를 바꿀 때마다 같은 배율을 알려준다.
    public void SetZoomScale(float scale) => zoomScale = scale;

    void OnEnable()
    {
        // 이 팝업 문구는 코드가 직접 채우므로 LocalizeText가 붙지 않는다.
        // 언어를 바꿔도 갱신되지 않으니 여기서 직접 구독한다. (EnemyInfo와 같은 이유)
        LocalizeTextManager.OnLanguageChanged += Relocalize;
        // LateUpdate 하나로는 부족하다 — ScrollRect와 이 스크립트의 LateUpdate 순서는 보장되지 않아
        // 우리 쪽이 먼저 돌면 그 프레임의 밀림이 한 번 그려진다. 위치가 바뀌는 즉시 잘라 준다.
        if (scroll != null) scroll.onValueChanged.AddListener(OnScrollMoved);
    }

    void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= Relocalize;
        if (scroll != null) scroll.onValueChanged.RemoveListener(OnScrollMoved);
        ResetHover();
    }

    private void OnScrollMoved(Vector2 _) => ClampContentPosition();

    void Update()
    {
        TickHide();                          // 아래 조기 return에 삼켜지지 않게 맨 앞에서 돈다

        if (hoverRow == null) return;
        if (shownRow == hoverRow) return;    // 이미 떠 있음

        hoverTimer += Time.unscaledDeltaTime;
        if (hoverTimer >= hoverDelay) ShowTooltip(hoverRow);
    }

    // 행에서 커서가 벗어나도 바로 닫지 않는다. 툴팁(도감 버튼)까지 가는 동안 빈 공간을 지나므로
    // 여유 시간을 주고, 그 사이 툴팁이나 행에 다시 들어오면 취소된다.
    private void BeginHide()
    {
        if (shownRow == null) return;   // 떠 있는 게 없으면 예약할 것도 없다
        hidePending = true;
        hideTimer = 0f;
    }

    private void CancelHide()
    {
        hidePending = false;
        hideTimer = 0f;
    }

    private void TickHide()
    {
        if (!hidePending) return;
        if (pointerOverTooltip || hoverRow != null) { CancelHide(); return; }

        hideTimer += Time.unscaledDeltaTime;
        if (hideTimer < hideGrace) return;

        ResetHover();   // 안에서 CancelHide + HideTooltip
    }

    // 팝업 패널은 StageInfoFollow가 매 프레임 포탈의 스크린 좌표로 옮긴다.
    // 툴팁은 그 패널의 자식이라 같이 밀려나므로, 화면 밖 보정도 매 프레임 다시 해줘야 한다.
    // (Follow가 없는 구성이나 Follow보다 먼저 도는 경우까지 커버하는 안전망 — 계산은 멱등하다)
    void LateUpdate()
    {
        RefreshTooltipPosition();
        UpdateScrollLock();
    }

    // 행 수가 바뀌었다는 표시. ContentSizeFitter가 반영되기 전에 재면 첫 프레임 판정이 틀리므로,
    // 바뀐 프레임에만 레이아웃을 강제로 밀어 다시 잰다(매 프레임 밀면 팝업이 떠 있는 동안 계속 재계산된다).
    private bool rowsDirty;

    // 행을 창에 맞춰 줄이고, 다 들어오면 스크롤을 잠근다.
    // 잠그지 않으면 Clamped라도 내용이 뷰포트보다 작을 때 남는 여백만큼 드래그로 밀려 다닌다
    // (Clamped는 "내용이 창보다 클 때" 경계를 잡아주는 설정이다).
    private void UpdateScrollLock()
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;

        float view = scroll.viewport.rect.height;
        float scale = 1f;
        if (fitAllRows && used > 0 && rowHeight > 0f && view > 0f)
        {
            float natural = used * rowHeight;                     // 축소 없이 필요한 높이
            if (natural > view) scale = Mathf.Max(minFitScale, view / natural);
        }
        ApplyFitScale(scale);

        if (rowsDirty)
        {
            rowsDirty = false;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        }

        // 줄인 뒤에도 넘치면(minFitScale에 걸린 경우) 그때만 스크롤을 살린다.
        // 1px 여유 — 딱 맞을 때 부동소수 오차로 잠금이 깜빡이지 않게 한다.
        bool overflow = scroll.content.rect.height * scale > view + 1f;
        scroll.vertical = overflow;
        ClampContentPosition();
    }

    // 지금 적용된 축소 배율. onValueChanged 콜백에서도 같은 값으로 잘라야 하므로 들고 있는다.
    private float fitScale = 1f;

    // 첫 줄은 위에, 마지막 줄은 아래에 딱 붙여 고정한다 — 그 범위를 넘어가는 위치를 잘라낸다.
    //
    // ScrollRect의 Clamped에 맡길 수 없는 이유: 그쪽 경계 계산(GetBounds)은 Content의 rect
    // 네 꼭짓점만 본다. 우리는 Content를 축소해 두고 셀 폭을 rect 밖으로 키워 쓰기 때문에
    // 그 사각형이 실제로 그려지는 범위와 어긋나고, 그래서 끝을 지나쳐 밀려 버린다.
    private void ClampContentPosition()
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;

        RectTransform content = scroll.content;
        // anchoredPosition은 부모(Viewport) 좌표라 Content 자신의 localScale에 안 곱해진다.
        // 반면 화면에서 차지하는 높이는 배율이 곱해진 값이다 — 둘을 섞지 않게 따로 계산한다.
        float visible = content.rect.height * fitScale;
        float max = Mathf.Max(0f, visible - scroll.viewport.rect.height);

        Vector2 p = content.anchoredPosition;
        // Content는 pivot·anchor가 위쪽이라 y=0이 "첫 줄이 창 맨 위", y=max가 "마지막 줄이 창 맨 아래".
        float y = Mathf.Clamp(p.y, 0f, max);
        if (p.x == 0f && Mathf.Approximately(p.y, y)) return;

        content.anchoredPosition = new Vector2(0f, y);
        // 끝에 닿았는데 관성이 남아 있으면 매 프레임 밀었다 잘리며 떨린다 — 여기서 같이 죽인다.
        scroll.velocity = Vector2.zero;
    }

    // 목록 전체를 한 배율로 줄인다.
    // 셀 높이만 줄이면 안 되는 이유: 행 프리팹 안의 아이콘·글자는 앵커로 고정돼 있어
    // 셀이 낮아져도 그대로 남아 아래 행과 겹친다. 스케일이면 내용까지 같이 작아진다.
    // 셀 폭은 배율의 역수로 키워, 줄인 뒤의 폭이 창을 정확히 채우게 한다
    // (안 하면 배율만큼 오른쪽에 빈 띠가 남는다).
    private void ApplyFitScale(float scale)
    {
        fitScale = scale;
        RectTransform content = scroll.content;
        if (!Mathf.Approximately(content.localScale.x, scale))
        {
            content.localScale = new Vector3(scale, scale, 1f);
            rowsDirty = true;
        }

        // 셀 폭 보정은 축소를 쓸 때만 한다. 일반 ScrollView에서 이걸 켜두면 셀 폭이 뷰포트 폭으로
        // 덮여 항상 1열이 되고, Grid의 열 수·셀 크기를 인스펙터에서 잡아둔 게 무시된다.
        if (!fitAllRows || rowGrid == null) return;
        var cell = new Vector2(scroll.viewport.rect.width / scale, rowHeight);
        if ((rowGrid.cellSize - cell).sqrMagnitude > 0.01f)
        {
            rowGrid.cellSize = cell;
            rowsDirty = true;
        }
    }

    // 새 스테이지 정보를 그리기 시작. 떠 있던 행/툴팁을 먼저 정리한다.
    // 팝업 오브젝트는 풀에서 재사용되므로(Despawn해도 자식이 남는다) 매번 반드시 초기화해야 한다.
    public void Begin()
    {
        used = 0;
        rowsDirty = true;
        ResetHover();
        HideAllRows();
        // 새 스테이지는 늘 맨 위부터. 풀 재사용이라 안 되돌리면 지난번 스크롤 위치에서 시작한다.
        // verticalNormalizedPosition이 아니라 anchoredPosition을 쓰는 이유는 ClampContentPosition 주석 참고
        // (축소한 Content에서는 ScrollRect의 정규화 좌표가 실제 위치와 어긋난다).
        if (scroll != null && scroll.content != null)
        {
            scroll.content.anchoredPosition = Vector2.zero;
            scroll.velocity = Vector2.zero;
        }
    }

    // 적 한 줄 추가. badgeKey는 "Ui_Boss"(보스), 일반 웨이브는 null.
    // 증원은 배지를 쓰지 않는다 — WaveSpawner가 같은 몹의 마릿수에 합쳐 일반 줄로 넘긴다.
    public void AddRow(EnemyTable.Data data, int count, string badgeKey = null)
    {
        if (data == null) return;
        if (rowPrefab == null || rowContainer == null)
        {
            // 이게 비면 팝업이 통째로 빈 채로 뜬다 — 조용히 실패하지 않게 알린다.
            Debug.LogWarning("StageInfoView: rowPrefab / rowContainer 미할당 — 적 목록이 표시되지 않습니다.", this);
            return;
        }

        StageEnemyRow row = GetRow(used++);
        row.gameObject.SetActive(true);
        row.Set(data, count, badgeKey, OnRowHover, OnRowExit, OnRowClick);
        rowsDirty = true;   // 높이가 바뀌었다 — 다음 LateUpdate에서 스크롤 잠금을 다시 판정한다
    }

    // 팝업을 비운다(밤 전환/다른 칸 클릭 등).
    public void Clear()
    {
        used = 0;
        rowsDirty = true;
        ResetHover();
        HideAllRows();
    }

    // 행 재사용: 부족할 때만 새로 만들고, 남는 행은 끈다(풀링과 같은 이유로 Destroy 안 함).
    private StageEnemyRow GetRow(int index)
    {
        while (rows.Count <= index)
        {
            StageEnemyRow created = Instantiate(rowPrefab, rowContainer);
            created.name = $"StageEnemyRow_{rows.Count}";
            rows.Add(created);
        }
        return rows[index];
    }

    private void HideAllRows()
    {
        for (int i = used; i < rows.Count; i++)
            if (rows[i] != null) rows[i].gameObject.SetActive(false);
    }

    // 언어가 바뀌면 각 행 문구를 다시 만들고, 떠 있던 툴팁도 새 언어로 교체한다.
    private void Relocalize()
    {
        for (int i = 0; i < used; i++)
            if (rows[i] != null) rows[i].Refresh();
        if (shownRow == null) return;
        FillTooltip(shownRow);
        PlaceTooltip(shownRow, true);   // 언어가 바뀌면 글자 길이가 달라져 위치도 다시 잡아야 한다
    }

    private void OnRowHover(StageEnemyRow row)
    {
        CancelHide();                        // 돌아왔으면 예약된 닫기를 취소(같은 행이어도)
        if (hoverRow == row) return;
        hoverRow = row;
        hoverTimer = 0f;
        if (shownRow != null && shownRow != row) HideTooltip();   // 다른 행으로 옮기면 갈아끼운다
    }

    private void OnRowExit(StageEnemyRow row)
    {
        if (hoverRow != row) return;
        hoverRow = null;
        hoverTimer = 0f;
        BeginHide();                         // 즉시 닫지 않는다 — 도감 버튼까지 갈 시간을 준다
    }

    // 클릭은 hoverDelay 없이 즉시. 같은 행을 다시 누르면 닫는다.
    private void OnRowClick(StageEnemyRow row)
    {
        if (shownRow == row) { HideTooltip(); return; }
        ShowTooltip(row);
    }

    private void ShowTooltip(StageEnemyRow row)
    {
        if (row == null || row.Data == null) return;
        FillTooltip(row);
        if (tooltip != null)
        {
            tooltip.SetActive(true);   // 크기를 재려면 먼저 켜야 한다(레이아웃이 계산됨)
            PlaceTooltip(row, true);
        }
        shownRow = row;
    }

    // 항상 인스펙터 offset이 가리키는 쪽(기본은 행의 왼쪽)에 띄운다.
    // 화면을 벗어나는 만큼만 안으로 밀어 가장자리에 붙인다 — 반대쪽으로 튀지 않는다.
    // 오버레이 캔버스라 position·GetWorldCorners가 전부 화면 픽셀 단위다.
    private void PlaceTooltip(StageEnemyRow row, bool rebuildLayout)
    {
        if (tooltip == null || row == null) return;

        // 글자가 바뀌면 rect가 아직 예전 크기다. 갱신을 먼저 흘려보낸 뒤에 재야 정확하다.
        if (rebuildLayout) Canvas.ForceUpdateCanvases();

        Transform t = tooltip.transform;
        float maxX = Screen.width - screenMargin;
        float maxY = Screen.height - screenMargin;

        t.position = row.transform.position + tooltipOffset * zoomScale;
        if (!MeasureTooltip()) return;   // 잴 게 없으면 그대로 둔다

        // 삐져나간 만큼만 이동. 가장자리에 닿을 때까지는 offset 위치 그대로 따라간다.
        float dx = 0f, dy = 0f;
        if (bLeft < screenMargin) dx = screenMargin - bLeft;
        else if (bRight > maxX) dx = maxX - bRight;
        if (bBottom < screenMargin) dy = screenMargin - bBottom;
        else if (bTop > maxY) dy = maxY - bTop;

        if (dx != 0f || dy != 0f) t.position += new Vector3(dx, dy, 0f);
    }

    private readonly Vector3[] cornerBuf = new Vector3[4];
    private readonly List<RectTransform> tooltipRects = new();
    private float bLeft, bRight, bBottom, bTop;

    // 툴팁 루트는 RectTransform이 아닐 수도 있다(빈 GameObject로 만든 경우).
    // 그래서 루트 rect가 아니라 '보이는 요소들'(Image·TMP)의 rect를 모아 경계를 잡는다.
    // 루트 크기와 실제 내용 크기가 다를 때도 이쪽이 더 정확하다.
    private void CacheTooltipRects()
    {
        tooltipRects.Clear();
        if (tooltip == null) return;
        foreach (var g in tooltip.GetComponentsInChildren<Graphic>(true))
            tooltipRects.Add(g.rectTransform);
    }

    // 지금 위치에서 툴팁이 차지하는 화면 범위. 잴 게 하나도 없으면 false.
    private bool MeasureTooltip()
    {
        bLeft = bBottom = float.MaxValue;
        bRight = bTop = float.MinValue;
        bool any = false;

        for (int i = 0; i < tooltipRects.Count; i++)
        {
            RectTransform r = tooltipRects[i];
            if (r == null || !r.gameObject.activeInHierarchy) continue;

            r.GetWorldCorners(cornerBuf);   // 오버레이 캔버스라 결과가 화면 픽셀
            for (int c = 0; c < 4; c++)
            {
                bLeft = Mathf.Min(bLeft, cornerBuf[c].x);
                bRight = Mathf.Max(bRight, cornerBuf[c].x);
                bBottom = Mathf.Min(bBottom, cornerBuf[c].y);
                bTop = Mathf.Max(bTop, cornerBuf[c].y);
            }
            any = true;
        }
        return any;
    }

    // 이름 + 간단한 설명만. 도감(EnemyInfo)은 페이지/화살표까지 있는 상세 패널이라 여기선 쓰지 않는다.
    private void FillTooltip(StageEnemyRow row)
    {
        var st = DataTableManager.StringTable;
        EnemyTable.Data data = row.Data;
        bool unlocked = EnemyArchiveData.IsUnlocked(data.Name);
        bool masked = maskUnknownEnemy && !unlocked;

        // 아이콘은 행이 이미 띄운 것을 그대로 재사용한다(Resources를 두 번 뒤지지 않게).
        // 아이콘 PNG가 없는 적, 그리고 가려야 하는 적은 오브젝트째 끈다 —
        // Image만 끄면 rect가 남아 MeasureTooltip이 빈 공간까지 툴팁 크기로 잡는다.
        if (tooltipIcon != null)
        {
            Sprite sprite = masked ? null : row.IconSprite;
            if (sprite != null) tooltipIcon.sprite = sprite;
            tooltipIcon.gameObject.SetActive(sprite != null);
        }

        if (tooltipNameText != null)
            tooltipNameText.text = masked ? unknownName : st.Get(data.Name);
        // 설명 문장 대신 스탯 블록. 가려야 하는 적은 수치까지 새어 나가지 않게 통째로 ???.
        if (tooltipDescText != null)
            tooltipDescText.text = masked ? unknownDesc : BuildStatBlock(data);

        // 도감은 미해금 적을 ???로 띄우고 잠금 팝업까지 낸다(EnemyInfo.ShowLocked).
        // 눌러도 볼 게 없으니 버튼을 숨긴다 — '더 보기'가 덜 보여주는 상황을 막는다.
        if (archiveButton != null) archiveButton.gameObject.SetActive(unlocked);
    }

    // 체력 / 공격력 / 방어력 / 특성 4줄.
    // 특성 항목은 EnemyAttributeText가 <link>로 감싸주므로, 이 TMP를 보는 AttributeTooltip을
    // 툴팁에 붙여두면 그 단어에 마우스를 올릴 때 특성 설명이 뜬다(도감과 같은 배선).
    // 색은 입히지 않는다 — 좁은 툴팁이라 색보다 읽히는 게 중요하고, 도감 쪽 색 설정과 갈리지 않게.
    private string BuildStatBlock(EnemyTable.Data data)
    {
        var st = DataTableManager.StringTable;
        // 실제로 스폰될 때와 같은 식(EnemyStatScaling)을 쓴다 — 표시용으로 따로 계산하지 않는다.
        var scaled = EnemyStatScaling.Compute(data, data.Class, dayCount);
        // 실제 스탯은 float이지만 툴팁은 정수로 보여준다(1234.5 같은 표기 방지).
        return $"{st.Get("Stat_Health")} : {Mathf.RoundToInt(scaled.Hp)}\n" +
               $"{st.Get("Stat_Attack")} : {Mathf.RoundToInt(scaled.Attack)}\n" +
               $"{st.Get("Stat_Defense")} : {Mathf.RoundToInt(scaled.Defense)}\n" +
               $"{st.Get(attributeLabelKey)} : {EnemyAttributeText.Build(data.Attribute, context: this)}";
    }
    private void ResetHover()
    {
        hoverRow = null;
        hoverTimer = 0f;
        CancelHide();
        HideTooltip();
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.SetActive(false);
        shownRow = null;
        // 커서 밑에서 비활성화되면 OnPointerExit가 오지 않는다 → 여기서 직접 내려야 플래그가 안 굳는다.
        pointerOverTooltip = false;
    }
}
