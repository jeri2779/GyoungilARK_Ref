using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuideItemHighlight : MonoBehaviour
{
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI itemText;
    [SerializeField] private Color selectedTint = new Color(0.502f, 0.361f, 0.204f);
    [SerializeField] private Color normalTint = Color.white;
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color normalTextColor = new Color(0.196f, 0.196f, 0.196f);

    private const string HeldParameter = "Held";
    private Animator buttonAnimator;
    private ButtonPressScale pressScale;

    // 이 항목 버튼의 눌린 모양을 담당하는 컴포넌트를 찾아 둔다 (애니메이터와 ButtonPressScale 둘 다 선택적이다)
    private void Awake()
    {
        buttonAnimator = GetComponent<Animator>();
        pressScale = GetComponent<ButtonPressScale>();
    }

    // 이 항목을 선택 상태로 표시하거나 해제한다 (클릭 이펙트는 전역 ClickEffect가 담당)
    public void SetSelected(bool selected)
    {
        if (buttonAnimator != null) buttonAnimator.SetBool(HeldParameter, selected);
        pressScale?.SetHeld(selected);

        if (selected)
        {
            itemImage.color = selectedTint;
            ApplyTextColor(selectedTextColor);
            return;
        }
        itemImage.color = normalTint;
        ApplyTextColor(normalTextColor);
    }

    // itemText가 아직 연결되지 않은 버튼(원본 프리팹 이관 전)에서는 색상 반영을 건너뛴다
    private void ApplyTextColor(Color color)
    {
        if (itemText == null) return;
        itemText.color = color;
    }
}
