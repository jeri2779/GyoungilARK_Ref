using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 구조물 그리드의 칸 하나. 빈 칸/지어진 칸을 오브젝트로 나누지 않고 아이콘 스프라이트 + 텍스트만
// 바꿔서 표현한다. 배치 인력(workers)은 생산 시설에만 있는 개념이라 House는 빈 문자열로 넘어온다.
public class FacilitySlotView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI emptyText;
    [SerializeField] private Button button;

    private int index;
    public int Index => index;

    public void SetIndex(int value)
    {
        index = value;
    }

    public void ShowEmpty()
    {
        if (icon != null) icon.gameObject.SetActive(false);
        if (levelText != null) levelText.text = string.Empty;
        if (emptyText != null) emptyText.text = DataTableManager.StringTable.Get("Ui_EmptySlot");
        if (nameText != null) nameText.text = string.Empty;
    }

    public void ShowBuilt(Sprite facilityIcon, string label, string level, string workers)
    {
        if (icon != null)
        {
            icon.sprite = facilityIcon;
            icon.gameObject.SetActive(true);
        }
        if (levelText != null) levelText.text = $"{level}";
        if (nameText != null) nameText.text = $"{label}\n{workers}".Trim();
        if (emptyText != null) emptyText.text = string.Empty;
    }

    public void BindClick(Action<int> onClick)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(index));
    }
}
