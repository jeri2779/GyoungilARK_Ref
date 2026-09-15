using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Dropdown))]
public class LocalizeDropdown : MonoBehaviour
{
    // Language enum 이름(Kr/En/jp) 앞에 붙여 StringTable 키를 만든다. 예: "Language" + "Kr" = "LanguageKr"
    [SerializeField] private string keyPrefix = "Language";
    private TMP_Dropdown dropdown;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
    }

    private void OnEnable()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();

        LocalizeTextManager.OnLanguageChanged += Refresh;
        // 값이 바뀌면 스스로 언어를 전환한다. 이게 없으면 인스펙터에서
        // onValueChanged를 LocalizeText.OnDropdownChanged에 손으로 연결해야만 동작한다.
        dropdown.onValueChanged.AddListener(OnValueChanged);
        Refresh();
    }

    private void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= Refresh;
        if (dropdown != null) dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    // 옵션 순서가 Language enum 순서와 같으므로(Refresh가 그렇게 채운다) 인덱스가 곧 언어값이다.
    // 이미 같은 언어면 SetLanguage가 조기 반환하므로, 인스펙터 연결이 남아 있어도 중복 발생하지 않는다.
    private void OnValueChanged(int index)
    {
        LocalizeTextManager.SetLanguage((Language)index);
    }

    public void Refresh()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();

        var stringTable = DataTableManager.Get<StringTable>(DataTableIds.String);
        if (stringTable == null)
        {
            Debug.LogWarning("LocalizeDropdown: StringTable을 찾을 수 없음");
            return;
        }

        // Language enum을 선언(값) 순서대로 옵션에 배치 → 인덱스가 (Language)index와 일치
        var languages = (Language[])Enum.GetValues(typeof(Language));
        var options = new List<TMP_Dropdown.OptionData>(languages.Length);
        foreach (var language in languages)
        {
            var key = keyPrefix + Capitalize(language.ToString());
            options.Add(new TMP_Dropdown.OptionData(stringTable.Get(key)));
        }

        dropdown.options = options;
        // 표시값은 실제 현재 언어에 맞춘다(직전 인덱스를 보존하면 UI와 실제 언어가 어긋날 수 있다).
        // SetValueWithoutNotify라 OnValueChanged가 다시 불리지 않는다 → 무한 루프 없음.
        dropdown.SetValueWithoutNotify(
            Mathf.Clamp((int)StringTable.CurrentLanguage, 0, options.Count - 1));
        dropdown.RefreshShownValue();
    }
    private static string Capitalize(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
