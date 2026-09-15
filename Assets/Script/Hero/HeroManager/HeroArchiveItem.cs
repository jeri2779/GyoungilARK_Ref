using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroArchiveItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI nameText;
    private Action<HeroData> onClick;
    private HeroData data;

    private void OnEnable()
    {
        LocalizeTextManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= Refresh;
    }

    public void Setup(HeroData data, Action<HeroData> onClick)
    {
        this.data = data;
        this.onClick = onClick;
        icon.sprite = data.Icon;
        Refresh();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => this.onClick(this.data));
    }

    private void Refresh()
    {
        if (data == null) return;
        nameText.text = DataTableManager.StringTable.Get(data.HeroNameKey);
    }
}
