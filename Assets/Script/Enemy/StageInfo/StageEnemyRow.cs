using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 스테이지 정보 팝업의 한 줄. [아이콘] x 마릿수(증원/보스)
// StageInfoView가 프리팹을 인스턴스화한 뒤 Set()으로 값을 채운다.
//  - 아이콘은 도감과 같은 규칙: Resources/EnemyIcons/{Name}
//  - 아이콘 PNG가 없으면 이름 텍스트로 폴백한다(빈 사각형 방지 = 기존 표시와 동일).
//  - 마우스 올리거나 클릭하면 StageInfoView에 알려 툴팁을 띄운다.
// 프리팹 하이어라키 권장 구조:
//   Row (Image[투명, Raycast Target 켬] + 이 스크립트 + HorizontalLayoutGroup)
//    ├─ Border (Image)   ← 등급 색 테두리 (선택)
//    ├─ Icon   (Image)   ← 적 아이콘
//    ├─ Name   (TMP)     ← 아이콘 없을 때만 보임
//    └─ Count  (TMP)     ← "x 8" / "x 7(증원)"
public class StageEnemyRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Image border;      // 등급 색 테두리 (없어도 동작)
    [SerializeField] private TMP_Text nameText; // 아이콘 없을 때 폴백
    [SerializeField] private TMP_Text countText;

    [Header("등급별 테두리 색")]
    [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color eliteColor = new Color(0.95f, 0.75f, 0.2f);
    [SerializeField] private Color bossColor = new Color(0.9f, 0.2f, 0.2f);

    private EnemyTable.Data data;
    private int count;
    private string badgeKey;                    // "Ui_Add"(증원) / "Ui_Boss"(보스) / null
    private Action<StageEnemyRow> onHover;
    private Action<StageEnemyRow> onExit;
    private Action<StageEnemyRow> onClick;

    public EnemyTable.Data Data => data;

    // 이 행이 실제로 그리고 있는 아이콘. 아이콘 PNG가 없어 이름으로 폴백한 행은 null.
    // 툴팁이 같은 그림을 다시 Resources에서 찾지 않고 이걸 그대로 쓴다.
    public Sprite IconSprite => icon != null && icon.enabled ? icon.sprite : null;

    public void Set(EnemyTable.Data data, int count, string badgeKey,
                    Action<StageEnemyRow> onHover, Action<StageEnemyRow> onExit, Action<StageEnemyRow> onClick)
    {
        this.data = data;
        this.count = count;
        this.badgeKey = badgeKey;
        this.onHover = onHover;
        this.onExit = onExit;
        this.onClick = onClick;
        Refresh();
    }

    // 아이콘/문구를 현재 언어로 다시 만든다. 언어 전환 시 StageInfoView가 다시 호출.
    public void Refresh()
    {
        if (data == null) return;
        var st = DataTableManager.StringTable;

        Sprite sprite = Resources.Load<Sprite>($"EnemyIcons/{data.Name}");
        bool hasIcon = sprite != null;

        if (icon != null)
        {
            if (hasIcon) icon.sprite = sprite;
            icon.enabled = hasIcon;
        }
        if (border != null)
        {
            border.enabled = hasIcon;                        // 아이콘 없으면 테두리만 남는 걸 방지
            if (hasIcon) border.color = ClassColor(data.Class);
        }

        // 아이콘이 없는 적은 기존처럼 이름으로 표시 (아이콘 PNG가 다 채워지면 자동으로 그림으로 바뀜)
        if (nameText != null)
        {
            nameText.text = hasIcon ? string.Empty : st.Get(data.Name);
            nameText.gameObject.SetActive(!hasIcon);
        }

        if (countText != null)
        {
            string badge = string.IsNullOrEmpty(badgeKey) ? string.Empty : $"({st.Get(badgeKey)})";
            countText.text = $"x {count}{badge}";
        }
    }

    private Color ClassColor(string cls)
    {
        if (Enum.TryParse(cls, true, out EnemyClass c))
        {
            switch (c)
            {
                case EnemyClass.Boss: return bossColor;
                case EnemyClass.Elite: return eliteColor;
            }
        }
        return normalColor;   // Normal 또는 파싱 실패
    }

    public void OnPointerEnter(PointerEventData eventData) => onHover?.Invoke(this);
    public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke(this);
    public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke(this);
}
