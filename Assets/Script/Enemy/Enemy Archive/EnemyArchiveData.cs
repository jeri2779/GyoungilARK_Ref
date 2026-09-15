using System;
using System.Collections.Generic;
using UnityEngine;

// 도감 해금 데이터. 적이 처음 등장하면 그 enemyKey를 여기에 등록한다.
// (도감 창 열고/닫는 UI는 EnemyArchiveManager가 담당 — 여긴 "누가 등장했나" 상태만 보관)
// PlayerPrefs에 저장되어 다음 실행에도 유지된다.
public static class EnemyArchiveData
{
    private static readonly HashSet<string> unlocked = new();

    public static event Action<string> OnUnlocked;

    private const string PrefsKey = "EnemyArchive.Unlocked_Test1";
    private const char Separator = ';';

    // 게임 시작부터 열려 있는 적. 도감을 처음 열었을 때 보여줄 게 하나는 있어야 한다.
    // 저장본을 불러올 때(RestoreAll)와 초기화(ResetAll) 뒤에도 다시 넣어준다 —
    // 이 기능이 생기기 전 세이브에는 목록에 없으므로 그대로 두면 잠긴 채로 뜬다.
    public static readonly string[] DefaultUnlocked = { "Chicken" };

    static EnemyArchiveData() => Load();

    // 기본 해금을 집합에 직접 넣는다. Unlock()을 거치지 않는 이유:
    // 그쪽은 "새로 발견했다"는 뜻이라 로그를 찍고 OnUnlocked 이벤트까지 쏜다 — 기본값은 발견이 아니다.
    private static bool SeedDefaults()
    {
        bool added = false;
        for (int i = 0; i < DefaultUnlocked.Length; i++)
            added |= unlocked.Add(DefaultUnlocked[i]);
        return added;
    }

    public static void Unlock(string enemyKey)
    {
        if (string.IsNullOrEmpty(enemyKey)) return;
        if (!unlocked.Add(enemyKey)) return;   // 이미 있으면 false → 조기 반환

        // Debug.Log($"[도감] 새 적 해금: {enemyKey} (총 {unlocked.Count}종)");
        Save();
        OnUnlocked?.Invoke(enemyKey);
    }

    public static bool IsUnlocked(string enemyKey)
        => !string.IsNullOrEmpty(enemyKey) && unlocked.Contains(enemyKey);

    public static IReadOnlyCollection<string> All => unlocked;

    // 세이브 데이터로 도감 전체를 교체한다 (슬롯 JSON이 원본, 로드 복원 전용)
    public static void RestoreAll(IReadOnlyList<string> savedKeys)
    {
        unlocked.Clear();
        for (int i = 0; i < savedKeys.Count; i++)
        {
            if (!string.IsNullOrEmpty(savedKeys[i])) unlocked.Add(savedKeys[i]);
        }
        SeedDefaults();   // 기본 해금이 없던 시절의 세이브도 구제한다
        Save();
    }

    // 디버그/테스트용 — 도감 전체 초기화(기본 해금만 남는다)
    public static void ResetAll()
    {
        unlocked.Clear();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        SeedDefaults();
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, string.Join(Separator.ToString(), unlocked));
        PlayerPrefs.Save();
    }

    private static void Load()
    {
        unlocked.Clear();
        var raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var k in raw.Split(Separator))
                if (!string.IsNullOrEmpty(k)) unlocked.Add(k);
        }

        // 시드는 조기 return 안쪽이 아니라 여기서 무조건 돌려야 한다 —
        // 이미 PlayerPrefs가 쌓인 사람은 위 파싱 경로를 타므로 그쪽에만 넣으면 영영 안 열린다.
        if (SeedDefaults()) Save();
    }
}
