using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LocalizeText : MonoBehaviour
{
    [SerializeField] private string key;
    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizeTextManager.OnLanguageChanged +=Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= Refresh;
    }
    public void OnDropdownChanged(int index)
    {
        LocalizeTextManager.SetLanguage((Language)index);
    }

    public void SetKey(string newKey)
    {
        key = newKey;
        Refresh();
    }
    public void Refresh()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(key)) return;

        var stringTable = DataTableManager.Get<StringTable>(DataTableIds.String);
        if (stringTable == null)
        {
            Debug.LogWarning("LocalizeText: StringTable을 찾을 수 없음");
            return;
        }

        text.text = stringTable.Get(key);
    }
}
