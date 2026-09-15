using System.Collections.Generic;

// 시뮬레이터가 쓸 적 목록을 CSV 표에서 읽어오는 창구. 읽기만 하고 아무것도 계산하지 않는다.
public static class HeroSimEnemySource
{
    // 프로젝트의 모든 적 데이터를 등급·이름 순으로 돌려준다.
    public static List<EnemyTable.Data> CollectEntries()
    {
        var entries = new List<EnemyTable.Data>();
        EnemyTable table = DataTableManager.EnemyTable;
        if (table == null) return entries;

        // 적 개수는 CSV 저작에 따라 늘고 주는 동적 컬렉션이라 한 번 순회한다.
        foreach (EnemyTable.Data data in table.GetAll())
        {
            entries.Add(data);
        }

        entries.Sort(CompareEntry);
        return entries;
    }

    // 표에서 보기 좋도록 등급 → 이름 순으로 정렬한다.
    private static int CompareEntry(EnemyTable.Data left, EnemyTable.Data right)
    {
        int byClass = EnemyStatScaling.ParseClass(left.Class).CompareTo(EnemyStatScaling.ParseClass(right.Class));
        if (byClass != 0) return byClass;

        return string.CompareOrdinal(left.Name, right.Name);
    }
}
