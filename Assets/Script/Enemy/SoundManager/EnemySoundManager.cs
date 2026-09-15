using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class EnemySoundManager : MonoBehaviour
{
    public static EnemySoundManager Instance { get; private set; }
    [SerializeField] private EnemySoundDataBase db;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource systemSource;

    // 볼륨 값은 SettingUI(설정창)가 믹서에 직접 쓴다. 여기선 값을 읽기만 하고 절대 쓰지 않는다.
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string sfxParam = "SfxVolume";
    [SerializeField] private string bgmParam = "BgmVolume";
    [SerializeField] private string systemParam = "System";

    // 각 소스를 어느 믹서 그룹으로 내보낼지. 비워두면 아래 이름으로 믹서에서 찾아 붙인다.
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup systemGroup;

    [Header("중요 사운드(Warning 등) — 다른 소리에 묻히지 않게 한다")]
    [Tooltip("PlayImportant로 낸 소리를 SFX가 아니라 System 그룹으로 내보낸다 — 효과음 무리와 채널이 분리되고 SFX 슬라이더에도 안 깎인다.")]
    [SerializeField] private bool importantUsesSystemGroup = true;
    [Tooltip("중요 사운드를 낼 때 이미 울리고 있던 효과음 잔향을 끊는다. 보스 연출은 timeScale=0이라 새 효과음은 안 나고 직전 프레임 잔향만 남아 덮는다.")]
    [SerializeField] private bool importantCutsSfxTails = true;
    [Tooltip("중요 사운드가 나는 동안 BGM을 이 배율로 줄인다(덕킹). 1=안 줄임, 0=무음.")]
    [Range(0f, 1f)][SerializeField] private float importantBgmDuck = 0.25f;
    [Tooltip("눌리는 데 걸리는 시간(초). 짧을수록 딱 끊기듯 들어간다.")]
    [SerializeField] private float duckFadeIn = 0.08f;
    [Tooltip("원래 볼륨으로 돌아오는 시간(초). 길수록 자연스럽다.")]
    [SerializeField] private float duckRelease = 0.5f;

    // 매번 FindMatchingGroups를 돌지 않도록 캐시. 보이스를 재사용할 때 그룹을 다시 지정해야 해서 자주 쓴다.
    private AudioMixerGroup sfxGroupCached, systemGroupCached;
    private AudioMixerGroup SfxGroup => sfxGroupCached != null ? sfxGroupCached : (sfxGroupCached = ResolveGroup(sfxGroup, "SFX"));
    private AudioMixerGroup SystemGroup => systemGroupCached != null ? systemGroupCached : (systemGroupCached = ResolveGroup(systemGroup, "System"));

    // DB 항목의 SoundType이 실제 출력 경로를 정한다. 이 두 함수 말고 다른 곳에서 그룹/소스를 고르지 않는다.
    // Bgm으로 표시된 항목을 Play로 부르면 효과음 취급이다 — BGM은 곡 교체/페이드가 필요해 PlayBgm이 따로 있다.
    private AudioMixerGroup GroupFor(EnemySoundDataBase.SoundType type)
        => type == EnemySoundDataBase.SoundType.System ? SystemGroup : SfxGroup;

    // System 소스가 인스펙터에 안 꽂혀 있으면 조용히 사라지지 않도록 SFX 소스로 떨어뜨린다.
    private AudioSource SourceFor(EnemySoundDataBase.SoundType type)
        => type == EnemySoundDataBase.SoundType.System && systemSource != null ? systemSource : sfxSource;

    // 덕킹 상태. 믹서의 노출 파라미터(SfxVolume/BgmVolume)는 SettingUI 소유라 절대 건드리지 않는다 —
    // 대신 AudioSource.volume(소스별 배율)만 곱한다. 이건 믹서 볼륨과 독립이라 유저 설정을 덮어쓰지 않는다.
    //
    // bgmSource.volume에 쓰는 곳은 ApplyBgmVolume 하나뿐이다 — 크로스페이드와 덕킹이 둘 다 BGM 볼륨을
    // 건드리므로, 각자 직접 쓰면 서로 덮어써서 페이드 중에 덕킹이 풀리거나 그 반대가 된다.
    // 페이드는 "원래 얼마여야 하는가"(bgmBaseVolume)만 정하고, 덕킹은 거기에 배율만 곱한다.
    private float bgmBaseVolume = 1f;   // 덕킹을 빼고 봤을 때의 BGM 볼륨. 페이드가 이 값을 움직인다.
    private float duckUntil;            // Time.unscaledTime 기준. 이 시각까지 눌러 둔다.
    private float duckLevel;            // 0=원래 볼륨 / 1=완전히 눌림

    // 같은 키 효과음이 너무 짧은 간격으로 중복 재생되는 것만 막는 스로틀.
    // 상태를 SoundDatabase(SO 에셋)에 저장하면 에디터 세션 간에 값이 남아 소리가 안 나므로,
    // 런타임 전용 딕셔너리에 보관한다(세션마다 초기화).
    //
    // 0.05(초당 20번)에서 0.15로 올렸다. 같은 클립이 거의 동시에 겹치면 파형이 상관관계가 있어
    // 2배마다 +6dB로 합쳐지고 미세하게 어긋난 시작점끼리 콤 필터링을 일으킨다 — 소리가 커지는
    // 동시에 뭉개진다. 서로 다른 클립이 겹치는 것(비상관, +3dB)보다 훨씬 나쁘므로 여기부터 막는다.
    private const float PlayThrottle = 0.15f;
    private readonly Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();

    [Header("동시 재생 상한")]
    [Tooltip("한 키의 효과음이 동시에 몇 개까지 울릴 수 있는지. 위 스로틀이 '너무 빨리 다시'를 막는 것과 달리 이건 '동시에 너무 많이'를 막는다. 0 이하면 제한 없음.")]
    [SerializeField] private int maxConcurrentPerKey = 3;
    private int maxConcurrentTotal = 52;

    // 전역 동시 재생 장부. 여러 키가 섞여 길이가 제각각이라 '끝나는 시각'을 담는다(키별 장부는 시작 시각).
    private readonly List<float> activeEndTimes = new List<float>();

    // 키별로 아직 울리고 있다고 보는 재생의 시작 시각. 클립 길이가 지나면 끝난 것으로 간주해 비운다.
    // AudioSource를 세지 않는 이유: soundTime이 없는 효과음은 공용 sfxSource의 PlayOneShot으로 나가서
    // 개별 보이스를 들여다볼 방법이 아예 없다(isPlaying은 "뭐라도 울리는 중"만 알려준다).
    // 클립 길이로 추정하면 PlayOneShot이든 timedVoices든 같은 규칙이 적용된다.
    // lastPlayTime과 같은 이유로 런타임 전용이다 — 남은 항목도 시간이 지나면 스스로 만료된다.
    private readonly Dictionary<string, List<float>> activeStarts = new Dictionary<string, List<float>>();

    [Header("화면 밖 소리 컷")]
    [Tooltip("위치를 넘긴 효과음만 대상. 끄면 전부 재생한다 — 컷 때문에 소리가 빠지는지 A/B로 확인할 때 쓴다.")]
    [SerializeField] private bool cullOffscreen = true;
    [Tooltip("화면 밖 어디까지 허용할지(뷰포트 비율). 0.3이면 가로는 화면 폭의 30%, 세로는 높이의 30%만큼 바깥까지 들린다. 화면 밖 개체가 효과음 무리에 기여하지 않게 해서 혼잡을 줄인다.")]
    [SerializeField] private float offscreenMargin = 0.3f;

    // Camera.main은 MainCamera 태그가 붙은 '활성' 카메라만 찾는다. 이 매니저는 씬을 넘어 살아남으므로
    // 씬 전환 중에는 파괴된 참조(Unity의 가짜 null이라 == null이 참이 되어 다시 찾는다)나 null이 올 수 있다.
    private Camera listenerCam;
    private Camera ListenerCam => listenerCam != null ? listenerCam : (listenerCam = Camera.main);

    // 화면 밖에서 난 소리인지. 카메라가 오빗+줌(CameraRig: distance 5~120, pitch 5~89°)이라
    // 절대 거리로는 판정할 수 없어서 뷰포트로 투영해 본다 — 줌/팬/피치/종횡비가 전부 반영된 좌표가 나온다.
    // 탑다운이라 높낮이를 따로 다루지 않는다: 투영이 알아서 처리한다.
    private bool IsOffscreen(Vector3 worldPos)
    {
        if (!cullOffscreen) return false;
        Camera cam = ListenerCam;
        // 카메라를 못 찾으면 컷하지 않는다 — 판정이 안 되는 상황에서 조용해지는 것보다 한 번 더 울리는 게 낫다.
        if (cam == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return true;   // 카메라 뒤
        return vp.x < -offscreenMargin || vp.x > 1f + offscreenMargin
            || vp.y < -offscreenMargin || vp.y > 1f + offscreenMargin;
    }

    // soundTime이 들어있는 클립 전용 재생 슬롯.
    // 공용 sfxSource의 PlayOneShot으로는 중간에 못 끊는다 — 끊으려면 sfxSource.Stop()인데
    // 그러면 그 순간 재생 중인 다른 효과음까지 전부 죽는다. 그래서 이 클립들만
    // 각자 AudioSource를 하나씩 물고 재생하고, 시간이 되면 그 소스만 멈춘다.
    private class TimedVoice
    {
        public AudioSource source;
        public float endTime;   // Time.unscaledTime 기준
        public bool manualStop; // true인 동안은 Update()의 자동 종료 스캔에서 제외 — 외부가 직접 Stop()할 때까지 유지되는 루프 전용
    }
    private readonly List<TimedVoice> timedVoices = new List<TimedVoice>();

    // BGM 크로스페이드 상태. 코루틴이 아니라 Update로 도는 이유는 위 timedVoices와 같다
    // (씬 전환/오브젝트 비활성화 시에도 안전하게 끊기도록).
    private enum BgmFadeState { None, FadeOut, FadeIn }
    private BgmFadeState fadeState = BgmFadeState.None;
    private float fadeTimer;
    private float fadeDuration;
    private float fadeStartVolume;
    private EnemySoundDataBase.Entry pendingBgmEntry;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (sfxSource != null) sfxSource.playOnAwake = false;
        if (bgmSource != null) { bgmSource.playOnAwake = false; bgmSource.loop = true; }
        // 프리팹의 세 소스 모두 Play On Awake가 켜져 있다. 지금은 클립이 비어 있어 조용하지만,
        // System 소스를 실제로 쓰기 시작했으니 여기서도 꺼둔다(나중에 클립을 꽂으면 씬 시작하자마자 울린다).
        if (systemSource != null) systemSource.playOnAwake = false;

        RouteToMixer();
    }

    // 소스를 믹서 그룹에 연결한다. 연결돼 있어야 설정창에서 바꾼 믹서 볼륨이 실제 출력에 반영된다.
    private void RouteToMixer()
    {
        if (sfxSource != null) sfxSource.outputAudioMixerGroup = ResolveGroup(sfxGroup, "SFX");
        if (bgmSource != null) bgmSource.outputAudioMixerGroup = ResolveGroup(bgmGroup, "BGM");
        if (systemSource != null) systemSource.outputAudioMixerGroup = ResolveGroup(systemGroup, "System");
    }

    private AudioMixerGroup ResolveGroup(AudioMixerGroup assigned, string groupName)
    {
        if (assigned != null) return assigned;
        if (mixer == null) return null;

        // FindMatchingGroups는 경로 부분 일치라 이름이 정확히 같은 그룹을 우선 고른다
        var groups = mixer.FindMatchingGroups(groupName);
        foreach (var g in groups)
            if (g.name == groupName) return g;
        return groups.Length > 0 ? groups[0] : null;
    }

    // 현재 믹서에 설정된 볼륨(0~1) 조회용. 출력 감쇠는 믹서가 하므로 재생 코드에서 곱하지 않는다.
    public float GetMasterVolume() => GetMixerVolume(masterParam);
    public float GetSfxVolume() => GetMixerVolume(sfxParam);
    public float GetBgmVolume() => GetMixerVolume(bgmParam);
    public float GetSystemVolume() => GetMixerVolume(systemParam);

    private float GetMixerVolume(string param)
    {
        if (mixer == null || !mixer.GetFloat(param, out float db)) return 1f;
        return DbToLinear(db);
    }

    // 데시벨 → 선형 0~1 변환 (믹서는 dB로 동작)
    private static float DbToLinear(float db)
    {
        if (db <= -80f) return 0f;
        return Mathf.Pow(10f, db / 20f);
    }

    // 효과음 재생 (중복 재생 허용)
    // soundTime이 0 이하면 클립을 끝까지 재생한다(기존 동작 그대로).
    // soundTime이 들어있으면 그 초가 지날 때 잘라낸다 — 긴 클립을 연출 길이에 맞춰 쓸 때.
    // ignoreThrottle: 한 발의 공격이 짧은 시간 안에 같은 키를 여러 번 의도적으로 재생해야 하는 경우
    // (예: 라인 관통 화살이 적을 연속으로 맞힐 때) 아래 스로틀을 우회한다 — 스로틀은 서로 무관한
    // 소스가 우연히 겹치는 걸 막기 위한 것이라, 한 번의 공격 안에서 일부러 반복 재생하는 경우까지
    // 막으면 안 된다.
    // at: 소리가 난 월드 좌표. 넘기면 화면 밖일 때 재생하지 않는다(offscreenMargin 참조).
    // 안 넘기면 지금까지와 동일하게 위치 판정 없이 재생한다 — UI/책장 넘김처럼 월드 위치가 없는 소리용.
    public static void Play(string key, bool ignoreThrottle = false, Vector3? at = null)
    {
        if (Instance == null || Instance.db == null) return;
        var e = Instance.db.Get(key);
        if (e == null) { Debug.LogWarning($"SoundDatabase에 '{key}' 키 없음"); return; }

        // 화면 밖 컷은 스로틀/동시 상한보다 먼저 본다. 어차피 안 낼 소리가 스로틀 기록이나 슬롯을 먹으면
        // 화면 안에 있는 다른 개체가 같은 키를 내려 할 때 엉뚱하게 막힌다.
        //
        // 컷은 Sfx에만 적용한다. System(버튼음·알림)은 위치를 넘겨도 절대 컷하지 않는다 — UI는 카메라가
        // 어디를 보고 있든 들려야 하고, 애초에 화면 밖이라는 개념이 없다. Bgm도 같은 이유로 제외.
        // 호출부가 위치를 넘기든 말든 이 규칙이 지켜지도록 판정을 여기 한 곳에 둔다.
        if (at.HasValue
            && e.type == EnemySoundDataBase.SoundType.Sfx
            && Instance.IsOffscreen(at.Value)) return;

        // 같은 키 중복 재생 스로틀: 런타임 딕셔너리 + 언스케일드 타임(일시정지 timeScale=0 영향 없음)
        float now = Time.unscaledTime;
        if (
            Instance.lastPlayTime.TryGetValue(key, out float last)
            && now - last < PlayThrottle) return;

        // 동시 상한은 스로틀을 통과한 뒤에 본다 — 스로틀에 막힌 호출은 위에서 이미 돌아갔으니 슬롯을 먹지 않는다.
        if (!Instance.TryReserveVoice(e, now)) return;


        Instance.lastPlayTime[key] = now;

        // 출력 경로는 DB 항목의 type이 정한다 — System으로 표시한 항목(버튼음 등)은 System 그룹으로 나가
        // 설정창의 System 슬라이더를 따르고, 효과음 무리와 채널이 분리된다.
        if (e.soundTime > 0f) { Instance.PlayTimed(e, now, Instance.GroupFor(e.type)); return; }

        AudioSource src = Instance.SourceFor(e.type);
        if (src == null) { Debug.LogWarning($"'{e.key}'를 낼 AudioSource 미할당(type={e.type})"); return; }
        // 카테고리 볼륨은 믹서가 담당. 여기선 클립별 상대 볼륨만 적용
        src.PlayOneShot(e.clip, e.volume);
    }

    // 이 키가 이미 상한만큼 울리고 있으면 false(=이번 재생은 버린다). 통과하면 시작 시각을 기록한다.
    //
    // ignoreThrottle이 켜져 있어도 이 상한은 적용한다 — 그 플래그는 "한 발의 공격 안에서 일부러 빠르게
    // 반복"을 허용하려는 것이고(관통 화살), 여기서 막는 건 "동시에 몇 개가 울리는가"라 다른 층이다.
    // 오히려 관통 화살이 적 여럿을 연속으로 스치는 순간이 같은 키 스택이 가장 심한 경우여서,
    // 둘 다 우회시키면 정작 제일 뭉개지는 상황만 그대로 남는다.
    private bool TryReserveVoice(EnemySoundDataBase.Entry e, float now)
    {
        if (e.clip == null) return true;   // 셀 기준이 없으니 여기서 판단하지 않고 아래 재생부로 넘긴다

        // 실제로 들리는 길이. soundTime이 있으면 그 시각에 잘리는데, 루프면 그 초 동안 반복하므로
        // 클립 길이가 아니라 soundTime이 곧 들리는 길이가 된다.
        float length;
        if (e.soundTime > 0f) length = e.loop ? e.soundTime : Mathf.Min(e.soundTime, e.clip.length);
        else length = e.clip.length;
        if (length <= 0f) return true;

        // 만료 정리와 상한 확인을 먼저 다 하고, 통과했을 때만 두 장부에 함께 기록한다 —
        // 한쪽만 먼저 적으면 다른 쪽에서 거부됐을 때 울리지도 않은 소리가 장부에 남는다.
        PruneExpired(now);
        if (maxConcurrentTotal > 0 && CountActiveVoices() >= maxConcurrentTotal) return false;

        List<float> starts = null;
        if (maxConcurrentPerKey > 0)
        {
            if (!activeStarts.TryGetValue(e.key, out starts))
            {
                starts = new List<float>(maxConcurrentPerKey);
                activeStarts[e.key] = starts;
            }

            // 끝난 것부터 걷어낸다. 리스트가 상한 크기(기본 3)라 매번 훑어도 비용이 없다.
            for (int i = starts.Count - 1; i >= 0; i--)
                if (now - starts[i] >= length) starts.RemoveAt(i);

            if (starts.Count >= maxConcurrentPerKey) return false;
        }

        starts?.Add(now);
        activeEndTimes.Add(now + length);
        return true;
    }

    // 전역 장부에서 이미 끝난 항목을 지운다. 키별 장부와 달리 여러 키가 섞여 길이가 제각각이라
    // 시작 시각이 아니라 '끝나는 시각'을 담아 두고 그걸로 만료를 잰다.
    private void PruneExpired(float now)
    {
        for (int i = activeEndTimes.Count - 1; i >= 0; i--)
            if (now >= activeEndTimes[i]) activeEndTimes.RemoveAt(i);
    }

    // 지금 울리고 있는 보이스 수. Play로 낸 원샷은 개별 보이스를 볼 방법이 없어 클립 길이로 추정하지만,
    // BGM과 PlayLoop 루프는 전용 AudioSource를 물고 있어 isPlaying으로 실측할 수 있다 —
    // 이 둘은 Play를 거치지 않아 추정 장부에 없으므로, 여기서 더해야 총량이 실제와 맞는다.
    // (PlayTimed로 난 소리는 Play를 거쳐 이미 장부에 있으니 manualStop 루프만 센다 — 이중 계수 방지)
    private int CountActiveVoices()
    {
        int n = activeEndTimes.Count;
        if (bgmSource != null && bgmSource.isPlaying) n++;
        for (int i = 0; i < timedVoices.Count; i++)
        {
            TimedVoice v = timedVoices[i];
            if (v.manualStop && v.source != null && v.source.isPlaying) n++;
        }
        return n;
    }

    /// <summary>
    /// 반드시 들려야 하는 소리(보스 Warning 등). Play와 달리 세 가지를 같이 한다:
    ///   ① System 그룹으로 내보내 효과음 무리와 채널을 분리하고,
    ///   ② 이미 울리고 있던 효과음 잔향을 끊고,
    ///   ③ 재생하는 동안 BGM을 눌러(덕킹) 자리를 비운다.
    /// AudioSource.priority로는 해결되지 않는다 — 그건 보이스 한계를 넘길 때만 쓰이는 값이고,
    /// 효과음은 전부 sfxSource 하나로 나가서 우선순위가 애초에 동일하다.
    ///
    /// Play를 거치지 않으므로 스로틀과 동시 상한이 둘 다 적용되지 않는다 — 우연이 아니라 의도다.
    /// "반드시 들려야 하는 소리"가 효과음 혼잡 때문에 걸러지면 이 함수의 존재 이유가 사라진다.
    /// </summary>
    public static void PlayImportant(string key)
    {
        if (Instance == null || Instance.db == null) return;
        var e = Instance.db.Get(key);
        if (e == null) { Debug.LogWarning($"SoundDatabase에 '{key}' 키 없음"); return; }
        Instance.PlayImportantInternal(e);
    }

    private void PlayImportantInternal(EnemySoundDataBase.Entry e)
    {
        // sfxSource.Stop()은 그 소스에서 울리던 원샷을 전부 죽인다 — 평소엔 단점이지만 여기선 목적 그대로다.
        // timedVoices는 각자 다른 AudioSource라 여기 안 걸린다(진행 중인 PlayLoop는 계속 울린다).
        if (importantCutsSfxTails && sfxSource != null) sfxSource.Stop();

        float now = Time.unscaledTime;
        // soundTime으로 잘리는 클립이면 그 길이, 아니면 클립 전체 길이만큼 눌러 둔다.
        float length = e.soundTime > 0f ? e.soundTime : (e.clip != null ? e.clip.length : 0f);
        duckUntil = Mathf.Max(duckUntil, now + length);   // 연달아 불려도 더 늦은 쪽을 남긴다

        // 이 플래그가 켜져 있으면 DB의 type을 무시하고 System으로 밀어올린다(중요 사운드의 존재 이유).
        // 꺼두면 평소 Play와 같은 규칙 — 항목에 적힌 type을 그대로 따른다.
        AudioMixerGroup group = importantUsesSystemGroup ? SystemGroup : GroupFor(e.type);
        if (e.soundTime > 0f) { PlayTimed(e, now, group); return; }

        // soundTime이 없으면 중간에 끊을 일이 없으니 전용 슬롯이 필요 없다 — 그룹만 맞춰 원샷.
        AudioSource src = importantUsesSystemGroup
            ? SourceFor(EnemySoundDataBase.SoundType.System)
            : SourceFor(e.type);
        if (src == null) { Debug.LogWarning("중요 사운드를 낼 AudioSource가 없음(systemSource/sfxSource 미할당)"); return; }
        src.PlayOneShot(e.clip, e.volume);
    }

    // 명시적으로 멈출 때까지 유지되는 루프 사운드. PlayThrottle(중복 재생 스로틀)과 키별 동시 상한이
    // 둘 다 적용되지 않는다 — 같은 키를 쓰는 여러 인스턴스(예: 장판 여러 개)가 동시에 각자 독립적으로
    // 재생/정지 되어야 하기 때문. 상한에 걸려 재생이 버려지면 호출부는 null을 받고도 그 사실을 모른 채
    // 영영 안 끊기는 루프를 기대하게 된다. 반환된 AudioSource를 호출부가 들고 있다가 직접 Stop()해야 한다.
    public static AudioSource PlayLoop(string key)
    {
        if (Instance == null || Instance.db == null) return null;
        var e = Instance.db.Get(key);
        if (e == null) { Debug.LogWarning($"SoundDatabase에 '{key}' 키 없음"); return null; }

        TimedVoice voice = Instance.GetFreeVoice();
        voice.manualStop = true;
        AudioSource src = voice.source;
        // 슬롯은 재사용된다 — 직전 재생이 다른 그룹을 썼을 수 있으므로 매번 이 항목의 type으로 다시 지정한다.
        src.outputAudioMixerGroup = Instance.GroupFor(e.type);
        src.clip = e.clip;
        src.volume = e.volume;
        src.loop = true; // DB entry의 loop 값과 무관하게 강제 루프
        src.Play();
        return src;
    }

    // soundTime이 있는 클립을 전용 소스로 재생하고 마감 시각을 예약한다.
    // 마감 시각을 unscaledTime으로 잡는 게 핵심 — 이 기능을 처음 쓰는 곳이 보스 연출인데
    // 거기는 Time.timeScale=0으로 얼려놓고 돌아간다(SpawnerManager.BossOpeningDirecting).
    // 스케일드 시간으로 재면 얼어있는 동안 시계가 안 흘러 영영 안 끊긴다.
    private void PlayTimed(EnemySoundDataBase.Entry e, float now, AudioMixerGroup group)
    {
        TimedVoice voice = GetFreeVoice();
        voice.manualStop = false; // 이 슬롯이 과거에 PlayLoop로 쓰였던 경우에도 정상적으로 자동 종료되도록 리셋
        AudioSource src = voice.source;
        // 슬롯 재사용 때문에 그룹은 매번 명시한다 — 안 하면 직전 재생의 그룹이 그대로 남는다.
        src.outputAudioMixerGroup = group;
        src.clip = e.clip;
        src.volume = e.volume;
        src.loop = e.loop;      // 루프 + soundTime = "이 초 동안만 반복"
        src.Play();
        voice.endTime = now + e.soundTime;
    }

    // 놀고 있는 슬롯을 재사용하고, 없으면 하나 더 만든다.
    // 새로 만든 소스도 반드시 SFX 믹서 그룹에 물려야 설정창 볼륨이 먹는다(RouteToMixer와 같은 이유).
    private TimedVoice GetFreeVoice()
    {
        for (int i = 0; i < timedVoices.Count; i++)
        {
            TimedVoice v = timedVoices[i];
            if (v.source != null && !v.source.isPlaying) return v;
        }

        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.outputAudioMixerGroup = ResolveGroup(sfxGroup, "SFX");
        // 인스펙터에서 잡아둔 sfxSource와 같은 톤으로 들리게 맞춘다.
        // 특히 spatialBlend — 새 AudioSource는 기본이 2D라, sfxSource가 3D면 이것만 소리가 다르게 난다.
        if (sfxSource != null)
        {
            src.spatialBlend = sfxSource.spatialBlend;
            src.rolloffMode = sfxSource.rolloffMode;
            src.minDistance = sfxSource.minDistance;
            src.maxDistance = sfxSource.maxDistance;
            src.priority = sfxSource.priority;
            src.bypassEffects = sfxSource.bypassEffects;
            src.bypassListenerEffects = sfxSource.bypassListenerEffects;
        }

        var created = new TimedVoice { source = src };
        timedVoices.Add(created);
        return created;
    }

    // 예약된 마감 시각이 지난 슬롯만 멈춘다. 멈추면 isPlaying이 꺼져 자동으로 재사용 대상이 된다.
    // 코루틴 대신 Update로 도는 이유: 오브젝트가 꺼지거나 씬이 바뀔 때 코루틴만 죽고 소리가 남는 걸 피한다.
    private void Update()
    {
        if (timedVoices.Count > 0)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < timedVoices.Count; i++)
            {
                TimedVoice v = timedVoices[i];
                if (v.source == null || !v.source.isPlaying) continue;
                if (v.manualStop) continue;
                if (now < v.endTime) continue;
                v.source.Stop();
            }
        }

        // 덕킹을 먼저 굴려 duckLevel을 이번 프레임 값으로 만든 뒤 페이드가 최종 볼륨을 쓴다.
        // 둘 다 timedVoices 가드 바깥이어야 한다 — 타임드 보이스가 하나도 없어도 돌아야 하므로.
        TickDuck();
        UpdateBgmFade();
    }

    // 덕킹 레벨을 목표치로 서서히 민다. 시간 소스는 unscaled — 보스 연출은 timeScale=0으로 얼려놓고 돈다.
    private void TickDuck()
    {
        float target = Time.unscaledTime < duckUntil ? 1f : 0f;
        if (duckLevel == target) return;   // 변화 없으면 매 프레임 volume을 다시 쓰지 않는다

        float span = Mathf.Max(0.01f, target > duckLevel ? duckFadeIn : duckRelease);
        duckLevel = Mathf.MoveTowards(duckLevel, target, Time.unscaledDeltaTime / span);
        ApplyBgmVolume();
    }

    // 믹서의 BgmVolume(설정창 소유)이 아니라 소스별 배율만 곱한다 — 유저 설정과 서로 간섭하지 않는다.
    private void ApplyBgmVolume()
    {
        if (bgmSource == null) return;
        bgmSource.volume = bgmBaseVolume * Mathf.Lerp(1f, importantBgmDuck, duckLevel);
    }

    // BGM 재생 (같은 곡이면 무시, 다르면 교체).
    // fadeDuration이 0이면 기존과 동일하게 즉시 교체(보스 등장 등 임팩트가 필요한 전환용).
    // fadeDuration > 0이면 현재 곡을 그만큼 페이드 아웃한 뒤 새 곡을 페이드 인한다.
    public static void PlayBgm(string key, float fadeDuration = 0f)
    {
        if (Instance == null || Instance.db == null) return;
        var e = Instance.db.Get(key);
        if (e == null) { Debug.LogWarning($"SoundDatabase에 '{key}' 키 없음"); return; }
        if (Instance.bgmSource == null) { Debug.LogWarning("bgmSource 미할당"); return; }

        var src = Instance.bgmSource;
        if (src.isPlaying && src.clip == e.clip) return;

        if (fadeDuration <= 0f)
        {
            Instance.fadeState = BgmFadeState.None; // 진행 중이던 페이드가 있으면 취소하고 즉시 전환
            src.clip = e.clip;
            Instance.bgmBaseVolume = e.volume;
            Instance.ApplyBgmVolume();              // 덕킹 중이면 눌린 채로 곡만 갈아끼운다
            src.loop = e.loop;
            src.Play();
            return;
        }

        Instance.StartBgmFade(e, fadeDuration);
    }

    private void StartBgmFade(EnemySoundDataBase.Entry entry, float duration)
    {
        pendingBgmEntry = entry;
        fadeDuration = duration;
        fadeTimer = 0f;

        if (bgmSource.isPlaying)
        {
            // 덕킹이 곱해진 실제 volume이 아니라 "원래 볼륨"에서 출발해야 한다 —
            // 눌린 값을 시작점으로 잡으면 덕킹이 풀릴 때 페이드가 통째로 어긋난다.
            fadeStartVolume = bgmBaseVolume;
            fadeState = BgmFadeState.FadeOut;
        }
        else
        {
            // 최초 재생: 페이드 아웃할 대상이 없으니 바로 새 곡을 볼륨 0으로 깔고 페이드 인만 한다.
            bgmSource.clip = entry.clip;
            bgmSource.loop = entry.loop;
            bgmBaseVolume = 0f;
            ApplyBgmVolume();
            bgmSource.Play();
            fadeState = BgmFadeState.FadeIn;
        }
    }

    private void UpdateBgmFade()
    {
        if (fadeState == BgmFadeState.None) return;

        fadeTimer += Time.unscaledDeltaTime;
        float t = fadeDuration > 0f ? Mathf.Clamp01(fadeTimer / fadeDuration) : 1f;

        if (fadeState == BgmFadeState.FadeOut)
        {
            bgmBaseVolume = Mathf.Lerp(fadeStartVolume, 0f, t);
            ApplyBgmVolume();
            if (t >= 1f)
            {
                bgmSource.clip = pendingBgmEntry.clip;
                bgmSource.loop = pendingBgmEntry.loop;
                bgmSource.Play();
                fadeState = BgmFadeState.FadeIn;
                fadeTimer = 0f;
            }
        }
        else // FadeIn
        {
            bgmBaseVolume = Mathf.Lerp(0f, pendingBgmEntry.volume, t);
            if (t >= 1f)
            {
                bgmBaseVolume = pendingBgmEntry.volume;
                fadeState = BgmFadeState.None;
            }
            ApplyBgmVolume();
        }
    }

    public static void StopBgm()
    {
        if (Instance == null || Instance.bgmSource == null) return;
        Instance.fadeState = BgmFadeState.None;
        Instance.bgmSource.Stop();
    }

    // 후보 키 중 하나를 무작위로 골라 PlayBgm으로 재생한다. 낮/밤처럼 곡을 여러 개 돌려쓰고 싶을 때 사용.
    public static void PlayRandomBgm(string[] keys, float fadeDuration = 0f)
    {
        if (keys == null || keys.Length == 0) return;
        PlayBgm(keys[UnityEngine.Random.Range(0, keys.Length)], fadeDuration);
    }
}
