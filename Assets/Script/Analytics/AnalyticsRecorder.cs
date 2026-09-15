using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// 빌드본에서 플레이 데이터를 수집하는 진입점. 게임플레이 코드는 아래 static 메서드만 호출하면 되고,
// 버퍼링/로컬 파일 기록/원격 전송은 전부 이 안에서 처리한다 — DI 등록 불필요(PoolManager.Instance 같은
// 기존 싱글톤 폴백 패턴과 동일하게 RuntimeInitializeOnLoadMethod로 자동 부트스트랩).
public static class AnalyticsRecorder
{
    [Serializable]
    private class HeroDamageRecord
    {
        public string evt = "hero_damage";
        public string session;
        public string wallClockKst;
        public string heroName;
        public int heroUnitId;
        public string enemyType;
        public string enemyClass;
        public int region;
        public int damage;
        public bool isCrit;
        public bool isKill;
    }

    [Serializable]
    private class EnemyLeakedRecord
    {
        public string evt = "enemy_leaked";
        public string session;
        public string wallClockKst;
        public string enemyType;
        public string enemyClass;
        public int region;
        public int dayCount;
        public int hpLost;
        public int hpRemaining;
    }

    [Serializable]
    private class RoundStartRecord
    {
        public string evt = "round_start";
        public string session;
        public string wallClockKst;
        public int region;
        public int dayCount;
        public int enemyCount;
    }

    [Serializable]
    private class RoundEndRecord
    {
        public string evt = "round_end";
        public string session;
        public string wallClockKst;
        public int region;
        public int dayCount;
        public float durationSeconds;
        public int hpLostThisRound;
    }

    [Serializable]
    private class HeroPlacedRecord
    {
        public string evt = "hero_placed";
        public string session;
        public string wallClockKst;
        public string heroName;
        public int heroUnitId;
        public int region;
        public int tileX;
        public int tileY;
    }

    [Serializable]
    private class GameOverRecord
    {
        public string evt = "game_over";
        public string session;
        public string wallClockKst;
        public int dayCount;
        public int finalHp;
    }

    private static readonly string SessionId = Guid.NewGuid().ToString("N");
    // 지역별 "이번 라운드 시작 이후 깎인 체력" 누적 — RoundStart가 리셋, EnemyLeaked가 누적, RoundEnd가 읽는다.
    private static readonly Dictionary<int, int> HpLostSinceRoundStart = new();

    private static readonly List<string> Buffer = new();
    private static Runner runner;
    private static AnalyticsSettings settings = null; // [애널리틱스 비활성화] 로드하지 않으므로 항상 null
    private static string FilePath = null;              // [애널리틱스 비활성화] 로컬 파일 경로 미사용

    private static void EnsureInit()
    {
        if (runner != null) return;

        // ───────── [애널리틱스 비활성화] ─────────
        // 설정 에셋 로드(Resources/Test·Build)와 로컬 로그 폴더/파일 준비를 모두 끈다.
        // settings가 null로 남으므로 SendRemote는 그대로 빠져나가고, FlushNow도 아무것도 하지 않는다.
        // 되살리려면 이 블록의 주석을 풀고, EnemyBase/WaveSpawner의 "[애널리틱스 비활성화]" 호출부도 함께 푼다.
        //
        // 에디터 플레이는 Resources/Test, 빌드는 Resources/Build를 쓴다 —
        // 개발 중 테스트 기록이 실제 배포 집계에 섞이지 않게 엔드포인트를 갈라 둔 것이다.
        // #if로 나누면 컴파일 때 한쪽이 아예 사라져 빌드에서 Test를 집는 경로 자체가 없어진다
        // (Test.asset 자체는 Resources에 있으니 빌드에 데이터로는 같이 실린다).
        // (개발 빌드도 Test로 보내려면 아래 조건을 UNITY_EDITOR || DEVELOPMENT_BUILD로 바꾼다.)
// #if UNITY_EDITOR
//         const string settingsName = "Test";
// #else
//         const string settingsName = "Build";
// #endif
//         settings = Resources.Load<AnalyticsSettings>(settingsName);
//         // 에셋을 못 찾으면 원격 전송이 조용히 죽는다(아래 Flush가 null이면 그냥 건너뛴다) —
//         // 어느 쪽을 집었는지 남겨야 "왜 시트에 안 들어오지"를 추적할 수 있다.
//         if (settings == null)
//             Debug.LogWarning($"AnalyticsRecorder: Resources/{settingsName} 에셋이 없습니다 — " +
//                 "원격 전송 없이 로컬 파일에만 기록합니다.");
//         // else
//         //     Debug.Log($"AnalyticsRecorder: 설정 '{settingsName}' 사용");
//
//         string dir = Path.Combine(Application.persistentDataPath, "Analytics");
//         try { Directory.CreateDirectory(dir); }
//         catch (Exception e) { Debug.LogWarning($"AnalyticsRecorder: 로그 폴더 생성 실패 — {e.Message}"); }
//         FilePath = Path.Combine(dir, $"session_{SessionId}.jsonl");

        var go = new GameObject("AnalyticsRecorder");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    public static void RoundStart(int region, int dayCount, int enemyCount)
    {
        EnsureInit();
        HpLostSinceRoundStart[region] = 0;
        Enqueue(new RoundStartRecord
        {
            session = SessionId,
            wallClockKst = NowKst(),
            region = region,
            dayCount = dayCount,
            enemyCount = enemyCount,
        });
    }

    public static void RoundEnd(int region, int dayCount, float durationSeconds)
    {
        EnsureInit();
        HpLostSinceRoundStart.TryGetValue(region, out int hpLost);
        Enqueue(new RoundEndRecord
        {
            session = SessionId,
            wallClockKst = NowKst(),
            region = region,
            dayCount = dayCount,
            durationSeconds = durationSeconds,
            hpLostThisRound = hpLost,
        });
    }

    public static void HeroDamage(string heroName, int heroUnitId, string enemyType, string enemyClass,
        int region, int damage, bool isCrit, bool isKill)
    {
        EnsureInit();
        Enqueue(new HeroDamageRecord
        {
            session = SessionId,
            wallClockKst = NowKst(),
            heroName = heroName,
            heroUnitId = heroUnitId,
            enemyType = enemyType,
            enemyClass = enemyClass,
            region = region,
            damage = damage,
            isCrit = isCrit,
            isKill = isKill,
        });
    }

    public static void EnemyLeaked(string enemyType, string enemyClass, int region, int dayCount, int hpLost, int hpRemaining)
    {
        EnsureInit();
        if (HpLostSinceRoundStart.TryGetValue(region, out int acc))
            HpLostSinceRoundStart[region] = acc + hpLost;
        else
            HpLostSinceRoundStart[region] = hpLost;

        Enqueue(new EnemyLeakedRecord
        {
            session = SessionId,
            wallClockKst = NowKst(),
            enemyType = enemyType,
            enemyClass = enemyClass,
            region = region,
            dayCount = dayCount,
            hpLost = hpLost,
            hpRemaining = hpRemaining,
        });
    }

    public static void HeroPlaced(string heroName, int heroUnitId, int region, int tileX, int tileY)
    {
        EnsureInit();
        Enqueue(new HeroPlacedRecord
        {
            session = SessionId,
            wallClockKst = NowKst(),
            heroName = heroName,
            heroUnitId = heroUnitId,
            region = region,
            tileX = tileX,
            tileY = tileY,
        });
    }

    public static void GameOver(int dayCount, int finalHp)
    {
        EnsureInit();
        Enqueue(new GameOverRecord
        {
            session = SessionId,
            wallClockKst = NowKst(),
            dayCount = dayCount,
            finalHp = finalHp,
        });
        runner.FlushNow(); // 게임오버 직후 씬 전환/종료로 유실되지 않게 즉시 내보낸다
    }

    // 시트에 찍히는 시각. 한국 시간(KST) "yyyy-MM-dd HH:mm" 고정 형식이다.
    // DateTime.Now(기기 시간대)를 쓰지 않는 이유 — 시간대가 잘못 잡힌 PC나 해외 기기에서 찍히면
    // 다른 세션과 시간이 어긋나 시트에서 정렬·비교가 깨진다. KST는 서머타임이 없어 연중 UTC+9로
    // 고정이므로 오프셋을 그대로 더하면 기기 설정과 무관하게 항상 같은 값이 나온다.
    // InvariantCulture를 붙이는 이유 — 문화권에 따라 서기가 아닌 달력(예: 일본 연호)으로 찍힐 수 있다.
    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);
    private static string NowKst() =>
        (DateTime.UtcNow + KstOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static void Enqueue(object record)
    {
        // ───────── [애널리틱스 비활성화] ─────────
        // 여기서 수집 자체를 끊는다 — 버퍼에 아무것도 쌓이지 않으므로 FlushNow는 항상 즉시 빠져나가고,
        // 로컬 파일 기록도 원격(구글시트) 전송도 일어나지 않는다. 이벤트를 쌓아두고 버리는 게 아니라
        // 애초에 만들지 않는 것이라, 나중에 되살릴 때 유실된 구간이 생기지 않는다.
        // 되살리려면 아래 3줄의 주석을 풀고, EnemyBase/WaveSpawner의 "[애널리틱스 비활성화]" 호출부와
        // EnsureInit의 설정 로드 블록도 함께 푼다.
        // Buffer.Add(JsonUtility.ToJson(record));
        // int max = settings != null ? settings.maxBufferedEvents : 50;
        // if (Buffer.Count >= max) runner.FlushNow();
    }

    // 실제 파일 쓰기/네트워크 전송/주기적 flush를 맡는 hidden MonoBehaviour.
    private class Runner : MonoBehaviour
    {
        private float timer;

        private void Update()
        {
            float interval = settings != null ? settings.flushIntervalSeconds : 15f;
            timer += Time.unscaledDeltaTime;
            if (timer >= interval)
            {
                timer = 0f;
                FlushNow();
            }
        }

        public void FlushNow()
        {
            if (Buffer.Count == 0) return;

            var batch = new List<string>(Buffer);
            Buffer.Clear();
            WriteLocal(batch);
            SendRemote(batch);
        }

        private void WriteLocal(List<string> batch)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (string line in batch) sb.Append(line).Append('\n');
                File.AppendAllText(FilePath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AnalyticsRecorder: 로컬 파일 기록 실패 — {e.Message}");
            }
        }

        private void SendRemote(List<string> batch)
        {
            if (settings == null || string.IsNullOrEmpty(settings.remoteEndpointUrl)) return;
            StartCoroutine(PostBatch(settings.remoteEndpointUrl, batch));
        }

        private System.Collections.IEnumerator PostBatch(string url, List<string> batch)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(batch[i]);
            }
            sb.Append(']');

            using UnityWebRequest req = new(url, UnityWebRequest.kHttpVerbPOST);
            byte[] body = Encoding.UTF8.GetBytes(sb.ToString());
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"AnalyticsRecorder: 원격 전송 실패({req.error}) — 로컬 파일에는 남아있음.");
        }

        private void OnApplicationQuit() => FlushNow();
        private void OnApplicationPause(bool paused) { if (paused) FlushNow(); }
    }
}
