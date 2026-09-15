using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeInfoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Image icon;


    public event Action<BaseUpgradeData> ConfirmClicked;
    private BaseUpgradeData current;
    public BaseUpgradeData Current => current;

    private void Awake()
    {
        confirmButton.onClick.AddListener(() => ConfirmClicked?.Invoke(current));
    }

    private void OnEnable()
    {
        if (current == null) return;
        nameText.text = DataTableManager.StringTable.Get(current.displayName);
        descText.text = DataTableManager.StringTable.Get(current.description);
    }

    public void Show(BaseUpgradeData data, bool canUnlockNow)
    {
        current = data;
        nameText.text = DataTableManager.StringTable.Get(data.displayName);
        descText.text = DataTableManager.StringTable.Get(data.description);
        costText.text = data.cost.ToString();
        icon.sprite = data.icon;
        icon.color = data.iconColor;
        confirmButton.gameObject.SetActive(canUnlockNow); // 여기서만 잠금 여부가 반영됨
        gameObject.SetActive(true);
    }

    public void OnReset(bool canUnlock)
    {
        confirmButton.gameObject.SetActive(canUnlock);
    }
}
