using UnityEngine;
using System.Collections.Generic;

public class HeroTable : DataTable
{
    public class Data
    {
        public int UnitId { get; set; }
        public int Tier { get; set; }
        public string HeroName { get; set; }
        public int HeroType { get; set; }
    }

    private readonly Dictionary<int, Data> table = new();

    public override void Load(string filename)
    {
        table.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogWarning($"HeroTable: '{path}' 로드 실패");
            return;
        }
        var list = LoadCsv<Data>(textAsset.text);
        foreach (var data in list)
        {
            if (!table.ContainsKey(data.UnitId)) table.Add(data.UnitId, data);
            else Debug.LogWarning($"HeroTable 키 중복 '{data.UnitId}'");
        }
    }

    public Data Get(int key) => table.TryGetValue(key, out var data) ? data : null;
    public IReadOnlyDictionary<int, Data> GetAll() => table;
}
