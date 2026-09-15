using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class HeroUpgradeResourcesUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amount;

    public void SetIcon(Sprite icon)
    {
        this.icon.sprite = icon;
    }
    public void SetAmount(int amount)
    {
        this.amount.text = amount.ToString();
    }

    // 영웅 생성 패널에서 이 자원 하나만으로는 유닛 하나도 못 만들 때 빨간색으로 강조하는 용도.
    public void SetAmount(int amount, bool sufficient)
    {
        this.amount.text = amount.ToString();
        this.amount.color = sufficient ? Color.white : Color.red;
    }
}
