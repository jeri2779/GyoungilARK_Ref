using UnityEngine;
using System.Collections.Generic;

public class HeroStatTable : DataTable
{
    public class Data
    {
        public string HeroName { get; set; }
        public float MaxHp { get; set; }
        public float AttackPower { get; set; }
        public float Defence { get; set; }
        public float MaxSp { get; set; }
        public float AttackSpeed { get; set; }
        public float SpRecover { get; set; }
        public float BlockCount { get; set; }
        public float CoolDownPer { get; set; }
        public float CriticalPer { get; set; }
        public float CriticalDmg { get; set; }
    }

    private readonly Dictionary<string, Data> table = new();

    public override void Load(string filename)
    {
        table.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogWarning($"HeroStatTable: '{path}' 로드 실패");
            return;
        }
        var list = LoadCsv<Data>(textAsset.text);
        foreach (var data in list)
        {
            if (!table.ContainsKey(data.HeroName)) table.Add(data.HeroName, data);
            else Debug.LogWarning($"HeroStatTable 키 중복 '{data.HeroName}'");
        }
    }

    public Data Get(string key) => table.TryGetValue(key, out var data) ? data : null;
    public IReadOnlyDictionary<string, Data> GetAll() => table;
}
