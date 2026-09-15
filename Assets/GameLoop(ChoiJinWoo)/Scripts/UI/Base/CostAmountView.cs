using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 비용 목록 한 줄(아이콘 + 수량). 어떤 자원이 몇 개 나올지 매번 달라질 수 있어서(예: 업그레이드 비용),
// ResourceAmountRow와 달리 타입을 미리 못 박아두지 못하고 매번 아이콘까지 코드로 채운다.
public class CostAmountView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;

    public void Show(Sprite sprite, string amount, bool canBuild)
    {
        gameObject.SetActive(true);
        if (icon != null) icon.sprite = sprite;
        if (amountText == null) return;
        amountText.text = amount;
        amountText.color = canBuild ? Color.white : Color.red;
        
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
