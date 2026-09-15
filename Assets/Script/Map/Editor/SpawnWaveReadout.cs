using System.Collections.Generic;

/// <summary>
/// 한 지역·라운드에 나오는 적을 모은다. 표시 전용 — 아무것도 쓰지 않는다.
///
/// 기본 웨이브 말고도 두 겹이 더 스폰된다(WaveSpawner.SpawnWave) — 따로 모아야 뒤섞이지 않는다.
/// 증원·보스 겹치기는 둘 다 라운드 스케일이 안 붙는다(원본 마릿수 그대로 스폰).
/// </summary>
public static class SpawnWaveReadout
{
    // 라운드 스케일 계산을 건너뛴다는 표시. 증원·보스는 WaveSpawner가 wave.Count를 그대로 쓴다.
    private const int NoScale = -1;
    // 보스 겹치기가 재활용하는 웨이브 ID(WaveSpawner.cs의 GetWave(1, 10)와 같다).
    private const int BossWaveId = 10;
    // 훑어볼 증원 단계 상한. 표 편집 중 단계가 늘어도 따라가되, 빈 행을 만나면 그 앞에서 멈춘다.
    private const int MaxReinforceTiers = 16;

    /// <summary>웨이브 한 줄(적 한 종류)과 화면에 쓸 값들.</summary>
    public class Entry
    {
        public WaveTable.Data Wave;
        public EnemyTable.Data Enemy;
        public int Count;
    }

    /// <summary>이 지역·라운드의 웨이브를 성격별로 나눈 것.</summary>
    public class Groups
    {
        public int Region;
        public int Round;
        public List<Entry> Base = new();
        public List<Entry> Reinforce = new();
        public List<Entry> Boss = new();

        public int Total => Base.Count + Reinforce.Count + Boss.Count;

        /// <summary>화면에 처음 보여줄 카드. 기본 → 증원 → 보스 순으로 있는 것부터.</summary>
        public Entry First
        {
            get
            {
                if (Base.Count > 0)
                {
                    return Base[0];
                }

                if (Reinforce.Count > 0)
                {
                    return Reinforce[0];
                }

                if (Boss.Count > 0)
                {
                    return Boss[0];
                }

                return null;
            }
        }

        // 이 표 행에 해당하는 카드를 찾는다. Collect가 프레임마다 Entry를 새로 찍어내므로
        // 고른 상태는 Entry가 아니라 이 안정적인 WaveTable.Data로 들고 있어야 다음 프레임에도 유지된다.
        public Entry Find(WaveTable.Data wave)
        {
            Entry found = FindIn(Base, wave);
            if (found != null)
            {
                return found;
            }

            found = FindIn(Reinforce, wave);
            if (found != null)
            {
                return found;
            }

            return FindIn(Boss, wave);
        }

        private static Entry FindIn(List<Entry> entries, WaveTable.Data wave)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Wave == wave)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }

    /// <summary>이 지역·라운드에 나오는 적들을 기본/증원/보스로 나눠 모은다.</summary>
    public static Groups Collect(int region, int round)
    {
        var groups = new Groups { Region = region, Round = round };
        WaveTable waveTable = DataTableManager.WaveTable;
        if (waveTable == null)
        {
            return groups;
        }

        int lookupId = WaveSpawner.GetStageLookupId(round);
        groups.Base = ToEntries(waveTable.GetWave(region, lookupId), round);

        // 에디터엔 "지금 몇 개 지역이 해금됐나"가 없다 — 표에 적힌 증원 단계(9001, 9002 …)를 전부 보여준다.
        // 실제로는 해금 지역 수에 따라 앞에서부터 몇 단계까지만 나온다(WaveSpawner.ReinforceTiers).
        for (int tier = 0; tier < MaxReinforceTiers; tier++)
        {
            List<WaveTable.Data> waves = waveTable.GetWave(region, WaveSpawner.ReinforceBaseId + tier);
            if (waves.Count == 0)
            {
                break;
            }

            groups.Reinforce.AddRange(ToEntries(waves, NoScale));
        }

        if (IsBossRound(region, round))
        {
            groups.Boss = ToEntries(waveTable.GetWave(1, BossWaveId), NoScale);
        }

        return groups;
    }

    // 지역 1의 10라운드마다(11, 21, 31 ...) 일반 웨이브 위에 보스가 겹쳐 등장한다(WaveSpawner.cs 그대로).
    private static bool IsBossRound(int region, int round)
    {
        return region == 1 && round > 10 && round % 10 == 0;
    }

    private static List<Entry> ToEntries(List<WaveTable.Data> waves, int round)
    {
        var entries = new List<Entry>();

        for (int i = 0; i < waves.Count; i++)
        {
            entries.Add(ToEntry(waves[i], round));
        }

        entries.Sort(BySpawnTime);
        return entries;
    }

    private static Entry ToEntry(WaveTable.Data wave, int round)
    {
        return new Entry
        {
            Wave = wave,
            Enemy = DataTableManager.EnemyTable?.Get(wave.MonsterName),
            Count = ScaledCount(wave.Count, round)
        };
    }

    private static int ScaledCount(int baseCount, int round)
    {
        if (round == NoScale)
        {
            return baseCount;
        }

        return WaveSpawner.GetScaleCount(baseCount, round);
    }

    private static int BySpawnTime(Entry a, Entry b)
    {
        return a.Wave.SpawnTime.CompareTo(b.Wave.SpawnTime);
    }
}
