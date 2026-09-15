using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HeroDescItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI descText;
    private AttackDescription desc = default;
    private bool isSet = false;
    private void OnEnable()
    {
        LocalizeTextManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= Refresh;
    }
    public void SetUp(AttackDescription desc)
    {
        this.desc = desc;
        isSet = true;
        icon.sprite = desc.icon;
        descText.text = DataTableManager.StringTable.Get(desc.attackDescriptionKey);
    }

    private void Refresh()
    {
        if (!isSet) return;
        descText.text = DataTableManager.StringTable.Get(this.desc.attackDescriptionKey);
    }
}
