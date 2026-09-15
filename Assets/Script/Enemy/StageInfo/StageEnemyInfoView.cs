using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 구역 클릭 시 뜨는 스테이지 정보 팝업의 표시 담당.
// StageInfoView와 달리 행 옆에 따로 뜨는 플로팅 툴팁이 아니라, 화면에 고정으로 떠 있는
// 옆 패널의 텍스트를 행 클릭에 맞춰 그 자리에서 갈아 끼우는 방식이다 — 위치 계산/화면 밖
// 보정/호버 지연 같은 툴팁 인프라가 필요 없다. 체력/공격력/방어력/특성도 각각 별도
// TMP_Text 필드로 받는다.
//
// 프리팹(TextPrefabs.prefab) 루트에 붙인다. WaveSpawner가 Begin()/AddRow()로 채운다.
public class StageEnemyInfoView : MonoBehaviour
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

    [Header("정보 패널(고정)")]
    [Tooltip("선택된 적 아이콘(선택). 아이콘 PNG가 없는 적이면 자동으로 숨긴다.")]
    [SerializeField] private Image infoIcon;
    [SerializeField] private TMP_Text infoNameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text defenseText;
    [SerializeField] private TMP_Text attributeText;
    [Tooltip("'도감 열기' 버튼. 누르면 도감이 열리고 그 적 페이지가 뜬다. 없어도 동작한다.")]
    [SerializeField] private Button archiveButton;
    [Tooltip("특성 줄의 라벨 StringTable 키. Ui_Type=타입, Ui_Attribute=속성. 값은 EnemyTable.Attribute다.")]
    [SerializeField] private string attributeLabelKey = "Ui_Attribute";

    [Header("미해금 처리")]
    [Tooltip("체크하면 도감에서 아직 만나지 않은 적은 이름/설명을 ???로 가린다. " +
             "스테이지 정보는 '오늘 밤 뭐가 오는지' 보고 대비하는 화면이라 기본값은 끔(그대로 공개).")]
    [SerializeField] private bool maskUnknownEnemy = false;
    [SerializeField] private string unknownName = "???";
    [SerializeField] private string unknownDesc = "???";

    private readonly List<StageEnemyRow> rows = new();
    private int used;                       // 이번 표시에 실제로 쓴 행 수
    private StageEnemyRow selectedRow;       // 정보 패널에 지금 표시 중인 행
    // 스탯 표시용 글로벌 일차. 패널을 켜는 SpawnerManager가 SetDay로 넣어준다.
    // VContainer로 GameManager를 직접 받지 않는 이유: 이 패널은 GameLifeTimeScope에 등록돼 있지 않고
    // (autoInjectGameObjects도 비어 있다) 비활성으로 시작하므로 [Inject]가 아예 호출되지 않는다.
    private int dayCount;

    void Awake()
    {
        // 인스펙터에 안 꽂아도 동작하게 자식에서 찾아둔다(프리팹 구조가 바뀌어도 조용히 죽지 않는다).
        if (scroll == null) scroll = GetComponentInChildren<ScrollRect>(true);
        if (rowGrid == null && rowContainer != null) rowContainer.TryGetComponent(out rowGrid);
        DisableContainerRaycast();

        if (archiveButton != null)
        {
            archiveButton.onClick.RemoveAllListeners();
            archiveButton.onClick.AddListener(OpenArchiveForSelectedRow);
        }
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
        Clear();                       // 켜져 있는 동안 정리해야 선택 상태가 확실히 내려간다
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    // 도감 버튼. 지금 정보 패널에 보이는 적 페이지로 도감을 연다.
    private void OpenArchiveForSelectedRow()
    {
        if (selectedRow == null || selectedRow.Data == null) return;
        EnemyArchiveManager manager = EnemyArchiveManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("StageEnemyInfoView: 씬에 EnemyArchiveManager가 없어 도감을 열 수 없습니다.", this);
            return;
        }
        manager.OpenAt(selectedRow.Data);
        // 도감을 닫아도 고정 패널은 방금 보던 정보를 그대로 유지한다 - 선택 해제하지 않는다.
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

    void OnEnable()
    {
        // 이 팝업 문구는 코드가 직접 채우므로 LocalizeText가 붙지 않는다.
        // 언어를 바꿔도 갱신되지 않으니 여기서 직접 구독한다. (EnemyInfo와 같은 이유)
        LocalizeTextManager.OnLanguageChanged += Relocalize;
        // LateUpdate 하나로는 부족하다 — ScrollRect와 이 스크립트의 LateUpdate 순서는 보장되지 않아
        // 우리 쪽이 먼저 돌면 그 프레임의 밀림이 한 번 그려진다. 위치가 바뀌는 즉시 잘라 준다.
        if (scroll != null) scroll.onValueChanged.AddListener(OnScrollMoved);
        // ESC로 닫기 — 다른 패널들과 같은 공용 신호(GlobalUiInputSignals)를 쓴다.
        GlobalUiInputSignals.EscapePerformed += HandleEscape;
    }

    void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= Relocalize;
        if (scroll != null) scroll.onValueChanged.RemoveListener(OnScrollMoved);
        GlobalUiInputSignals.EscapePerformed -= HandleEscape;
    }

    // 튜토리얼이 강제 진행 중일 땐 다른 패널들과 마찬가지로 ESC로 못 닫게 막는다.
    private void HandleEscape()
    {
        if (TutorialInputGate.BlockEscapeClose) return;
        Hide();
    }

    private void OnScrollMoved(Vector2 _) => ClampContentPosition();

    void LateUpdate()
    {
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

    // 새 스테이지 정보를 그리기 시작. 떠 있던 행/선택 정보를 먼저 정리한다.
    // 팝업 오브젝트는 풀에서 재사용되므로(Despawn해도 자식이 남는다) 매번 반드시 초기화해야 한다.
    public void Begin()
    {
        used = 0;
        rowsDirty = true;
        selectedRow = null;
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
            Debug.LogWarning("StageEnemyInfoView: rowPrefab / rowContainer 미할당 — 적 목록이 표시되지 않습니다.", this);
            return;
        }

        bool isFirstRow = used == 0;
        StageEnemyRow row = GetRow(used++);
        row.gameObject.SetActive(true);
        // 호버로는 정보 패널을 바꾸지 않는다 - 클릭으로만 바꾼다.
        row.Set(data, count, badgeKey, null, null, SelectRow);
        rowsDirty = true;   // 높이가 바뀌었다 — 다음 LateUpdate에서 스크롤 잠금을 다시 판정한다

        if (isFirstRow) SelectRow(row);   // 패널이 빈 채로 뜨지 않게 첫 행을 기본 선택
    }

    // 팝업을 비운다(밤 전환/다른 칸 클릭 등).
    public void Clear()
    {
        used = 0;
        rowsDirty = true;
        selectedRow = null;
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

    // 언어가 바뀌면 각 행 문구를 다시 만들고, 선택돼 있던 정보 패널도 새 언어로 다시 채운다.
    private void Relocalize()
    {
        for (int i = 0; i < used; i++)
            if (rows[i] != null) rows[i].Refresh();
        if (selectedRow != null) FillInfo(selectedRow);
    }

    // 행 클릭 시 정보 패널을 그 행 기준으로 갈아 끼운다.
    private void SelectRow(StageEnemyRow row)
    {
        if (row == null || row.Data == null) return;
        selectedRow = row;
        FillInfo(row);
    }

    // 이름 + 체력/공격력/방어력/특성을 각 필드에 따로 채운다.
    // 도감(EnemyInfo)은 페이지/화살표까지 있는 상세 패널이라 여기선 쓰지 않는다.
    private void FillInfo(StageEnemyRow row)
    {
        var st = DataTableManager.StringTable;
        EnemyTable.Data data = row.Data;
        bool unlocked = EnemyArchiveData.IsUnlocked(data.Name);
        bool masked = maskUnknownEnemy && !unlocked;

        // 아이콘은 행이 이미 띄운 것을 그대로 재사용한다(Resources를 두 번 뒤지지 않게).
        // 아이콘 PNG가 없는 적, 그리고 가려야 하는 적은 오브젝트째 끈다.
        if (infoIcon != null)
        {
            Sprite sprite = masked ? null : row.IconSprite;
            if (sprite != null) infoIcon.sprite = sprite;
            infoIcon.gameObject.SetActive(sprite != null);
        }

        if (infoNameText != null)
            infoNameText.text = masked ? unknownName : st.Get(data.Name);

        // 가려야 하는 적은 수치까지 새어 나가지 않게 4개 필드 전부 ???.
        if (masked)
        {
            if (hpText != null) hpText.text = unknownDesc;
            if (attackText != null) attackText.text = unknownDesc;
            if (defenseText != null) defenseText.text = unknownDesc;
            if (attributeText != null) attributeText.text = unknownDesc;
        }
        else
        {
            // 실제로 스폰될 때와 같은 식(EnemyStatScaling)을 쓴다 — 표시용으로 따로 계산하지 않는다.
            var scaled = EnemyStatScaling.Compute(data, data.Class, dayCount);
            // 실제 스탯은 float이지만 패널은 정수로 보여준다(1234.5 같은 표기 방지).
            // 특성 항목은 EnemyAttributeText가 <link>로 감싸주므로, 이 TMP를 보는 AttributeTooltip을
            // 붙여두면 그 단어에 마우스를 올릴 때 특성 설명이 뜬다(도감과 같은 배선).
            if (hpText != null) hpText.text = $"{st.Get("Stat_Health")} : {Mathf.RoundToInt(scaled.Hp)}";
            if (attackText != null) attackText.text = $"{st.Get("Stat_Attack")} : {Mathf.RoundToInt(scaled.Attack)}";
            if (defenseText != null) defenseText.text = $"{st.Get("Stat_Defense")} : {Mathf.RoundToInt(scaled.Defense)}";
            if (attributeText != null) attributeText.text = $"{st.Get(attributeLabelKey)} : {EnemyAttributeText.Build(data.Attribute, context: this)}";
        }

        // 도감은 미해금 적을 ???로 띄우고 잠금 팝업까지 낸다(EnemyInfo.ShowLocked).
        // 눌러도 볼 게 없으니 버튼을 숨긴다 — '더 보기'가 덜 보여주는 상황을 막는다.
        if (archiveButton != null) archiveButton.gameObject.SetActive(unlocked);
    }
}
