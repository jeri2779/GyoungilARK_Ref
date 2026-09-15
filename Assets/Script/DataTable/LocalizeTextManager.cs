using UnityEngine;

public static class LocalizeTextManager
{
    public static event System.Action OnLanguageChanged;
    public static Language CurrentLanguage => StringTable.CurrentLanguage;
    public static void SetLanguage(Language language)
    {
        if (StringTable.CurrentLanguage == language) return;
        StringTable.CurrentLanguage = language;
        OnLanguageChanged?.Invoke();
    }
}
