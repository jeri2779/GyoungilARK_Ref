using System;
using System.Collections.Generic;
using UnityEngine;

public class StringTable : DataTable
{
    public static readonly string UnKnown = "키 없음";

    public class Data
    {
        public string ID { get; set; }
        public string Kr { get; set; }
        public string En { get; set; }
        public string Jp { get; set; }
    }
    // public static Language CurrentLanguage = Language.Kr;
    private const string LanguagePrefsKey = "Language";
    private readonly Dictionary<string, Data> table = new Dictionary<string, Data>();
    private static Language? current;
    public static Language CurrentLanguage
    {
        get
        {
            if (current == null)
            {
                int saved = PlayerPrefs.GetInt(LanguagePrefsKey, (int)Language.Kr);
                // 저장값이 깨졌거나 enum에서 빠진 언어면 한국어로 되돌린다(Get이 UnKnown만 뱉는 걸 막는다).
                current = Enum.IsDefined(typeof(Language), saved) ? (Language)saved : Language.Kr;
            }
            return current.Value;
        }
        set
        {
            if (current == value) return;
            current = value;
            PlayerPrefs.SetInt(LanguagePrefsKey, (int)value);
            PlayerPrefs.Save();
        }
    }
    public override void Load(string filename)
    {
        table.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        var list = LoadCsv<Data>(textAsset.text);
        foreach (var data in list)
        {
            if (string.IsNullOrEmpty(data.ID)) continue;
            if (!table.ContainsKey(data.ID))
            {
                table.Add(data.ID, data);
            }
            else
            {
                Debug.LogWarning($"키 중복'{data.ID} - {filename}'");
            }
        }
    }

    public string Get(string key)
    {
        if (!table.ContainsKey(key))
        {
            return UnKnown;
        }
        var data = table[key];
        return CurrentLanguage switch
        {
            Language.Kr => data.Kr,
            Language.En => data.En,
            Language.Jp => data.Jp,
            _ => UnKnown
        };
    }
}
