using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 디버프 수치 표. SkillTable과 같은 구조지만 <b>키 중복이 정상</b>이라는 점이 다르다 —
/// 탈진처럼 스탯 여러 개를 깎는 디버프는 DebuffId가 같은 줄을 여러 개 쓰고, 줄 하나가 스탯 하나가 된다.
/// 그래서 Get은 Data 하나가 아니라 그 DebuffId에 속한 줄 목록을 돌려준다.
///
/// Category/Type/Duration은 그룹에서 처음 채워진 값을 쓴다(2번째 줄부터는 스탯 칸만 채우면 된다).
/// 실제 해석은 DebuffTableImporter가 하고, 런타임은 임포터가 만든 SO만 본다.
/// </summary>
public class DebuffTable : DataTable
{
    public class Data
    {
        public string DebuffId { get; set; }
        public string Category { get; set; }   // Stat / Dot / Stun — 어떤 DebuffSO 파생형을 만들지
        public string Type { get; set; }       // DebuffType 이름 (Slow, Exhaust, Poison, Stun ...)
        public float? Duration { get; set; }

        // Category=Stat 전용. 줄 하나가 effects 한 칸이 된다.
        public string StatType { get; set; }
        public string ModifierType { get; set; }
        public float? Value { get; set; }
        public int? MaxStacks { get; set; }

        // Category=Dot 전용.
        // 틱 피해는 고정값이 아니라 대상 최대 체력의 비율(%)이다 — 0.5면 한 틱에 최대 체력의 0.5%.
        // 고정값이면 체력 100 잡몹과 5000 보스에 같은 숫자가 들어가고, 적 체력이 UpHealthScale로
        // 불어나는 동안 지속 피해만 제자리다. 비율로 두면 표를 안 고쳐도 같이 따라간다.
        public float? PercentPerTick { get; set; }
        public float? Interval { get; set; }
        // 틱마다 추가로 더할 피해 — 시전자 공격력의 몇 %인가(20 = 공격력의 20%). PercentPerTick과 같은 % 단위다.
        // 비우면 0 = 최대 체력 비율 피해만 들어간다(기존 거동).
        // 최대 체력 비율만으로는 "누가 걸었든 같은 독"이라 공격력을 올린 보람이 없어서, 때린 쪽 몫을 여기에 더한다.
        // 곱할 공격력은 걸리는 순간의 값을 찍어 둔다 — 시전자가 죽거나 풀에 반납돼도 남은 독은 계속 굴러야 하므로.
        public float? AtkPercent { get; set; }
        // true면 틱 피해에 방어력이 안 먹는다. 비우면 false = 방어력이 적용된 피해로 들어간다.
        // bool?로 두는 이유 — bool이면 빈 칸에서 CsvHelper가 형변환 실패로 던지고, 그러면 이 표 전체가 안 읽힌다.
        // Category=Dot에서만 의미가 있다(스탯·상태이상은 피해를 안 넣는다). 임포터가 그걸 검사해 알려 준다.
        public bool? IgnoreGuard { get; set; }
    }

    private readonly Dictionary<string, List<Data>> table = new();

    public override void Load(string filename)
    {
        table.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogWarning($"DebuffTable: '{path}' 로드 실패");
            return;
        }

        // CsvHelper는 헤더 불일치·형변환 실패에 예외를 던진다. DataTableManager가 정적 생성자에서 Load를 부르므로
        // 여기서 새어 나가면 TypeInitializationException이 되어 이 표뿐 아니라 모든 테이블 접근이 죽는다.
        List<Data> list;
        try
        {
            list = LoadCsv<Data>(textAsset.text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DebuffTable: '{path}' 파싱 실패 — 헤더나 셀 형식을 확인하세요. {e.Message}");
            return;
        }

        foreach (var data in list)
        {
            var id = (data.DebuffId ?? "").Trim();
            if (string.IsNullOrEmpty(id)) continue;

            // 중복 경고를 내지 않는다 — 여기선 같은 키가 여러 줄인 게 설계다(SkillTable과 반대).
            if (!table.TryGetValue(id, out var rows))
            {
                rows = new List<Data>();
                table.Add(id, rows);
            }
            rows.Add(data);
        }
    }

    public IReadOnlyList<Data> Get(string key)
    {
        if (string.IsNullOrEmpty(key) || !table.ContainsKey(key)) return null;
        return table[key];
    }

    public IReadOnlyDictionary<string, List<Data>> GetAll()
    {
        return table;
    }
}
