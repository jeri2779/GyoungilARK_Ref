using System.Collections.Generic;
using UnityEngine;

// 지역별·라운드별로 이번에 열릴 포탈(레인) 개수를 정의하는 테이블.
// CSV 헤더: Region,Id,Count
//  - Region : 지역(레인) 번호. WaveSpawner.Region과 매핑.
//  - Id     : 라운드/일차. WaveTable의 ID와 같은 체계(10일차 초과는 1001~1005로 순환).
//  - Count  : 그 라운드에 활성화할 포탈(레인) 수. 실제 스폰 타일 수보다 크면 있는 만큼만 켜진다.
public class PortalTable : DataTable
{
    public class Data
    {
        public int Region { get; set; }
        public int Id { get; set; }
        public int Count { get; set; }
    }

    private readonly List<Data> rows = new();

    public override void Load(string filename)
    {
        rows.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogWarning($"PortalTable: '{path}' 로드 실패");
            return;
        }

        rows.AddRange(LoadCsv<Data>(textAsset.text));
    }

    public IReadOnlyList<Data> GetAll() => rows;

    /// <summary>해당 지역·라운드에 열 포탈(레인) 수. 행이 없으면 fallback 반환.</summary>
    public int GetCount(int region, int id, int fallback = 1)
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Region == region && rows[i].Id == id) return rows[i].Count;
        return fallback;
    }
}
