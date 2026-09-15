using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감 상세 패널.
//  - page 0    : 적 이름 + 적 설명 (스킬 텍스트는 비움)
//  - page 1..N : 적 설명은 비우고, 스킬 이름/설명 텍스트에 각 스킬을 표시
// 왼쪽/오른쪽 화살표로 페이지를 이동한다(순환 없음):
//  - 첫 페이지(0)면 왼쪽 화살표 숨김, 마지막 페이지면 오른쪽 화살표 숨김, 가운데면 둘 다 표시.
public class EnemyInfo : MonoBehaviour
{
    public Image e_Image;
    public TMP_Text m_ArchiveText;
    public TMP_Text e_NameText;
    public TMP_Text e_TypeText;
    public TMP_Text e_DescText;     
    public TMP_Text e_Attribute; 
    public TMP_Text e_SkillNameText; 
    public TMP_Text e_SkillDescText;
    public Button leftArrowButton;     
    public Button rightArrowButton;
    [SerializeField] private Image border;  // 등급 색 테두리 (뒤)

    [Header("등급별 테두리 색")]
    [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color eliteColor = new Color(0.95f, 0.75f, 0.2f);
    [SerializeField] private Color bossColor = new Color(0.9f, 0.2f, 0.2f);

    [Header("미해금 처리")]
    [Tooltip("체크 시 미해금 적은 등급 색 대신 회색 테두리(등급 스포일러 방지)")]
    [SerializeField] private bool grayWhenLocked = true;
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("미해금(아직 못 만난 적) 표시")]
    public string lockedName = "???";
    [TextArea] public string lockedMessage = "???";
    public GameObject lockedEnemyText;

    // 미해금일 때 e_Image에 띄울 ? 스프라이트. 목록 버튼(EnemyArchiveButton)과 같은 것을 쓰려고
    // 인스펙터에 또 꽂지 않고 EnemyArchive가 SetLockIcon으로 넘겨 준다 — 출처를 하나로 둔다.
    private Sprite lockIcon;

    /// <summary>목록이 쓰는 ? 스프라이트를 상세 패널에도 알려 준다. EnemyArchive가 적을 띄울 때마다 호출한다.</summary>
    public void SetLockIcon(Sprite icon) => lockIcon = icon;

    [Header("특성 표시")]
    [Tooltip("특성이 여러 개일 때 구분자 (예: \", \" 또는 \" | \")")]
    public string attributeSeparator = ", ";

    [Header("특성별 글자색 (TMP Rich Text 필요)")]
    public Color cloakingColor     = new Color(0.61f, 0.35f, 0.71f); // 은신 - 보라
    public Color flyColor          = new Color(0.20f, 0.60f, 0.86f); // 공중 - 하늘
    public Color unJudgedColor     = new Color(0.90f, 0.49f, 0.13f); // 저지 불가 - 주황
    public Color berserkColor      = new Color(0.91f, 0.30f, 0.24f); // 폭주 - 빨강
    public Color regenerationColor = new Color(0.18f, 0.80f, 0.44f); // 재생 - 초록
    public Color hitsShieldColor   = new Color(0.95f, 0.77f, 0.06f); // 타수 보호막 - 노랑
    public Color burrowColor       = new Color(0.65f, 0.32f, 0.20f); // 잠행 - 적갈색
    public Color FlameColor = new Color(0.96f,0.12f,0.12f);
    public Color SwimColor = new Color(0.16f,0.88f,0.22f);

    [Tooltip("고유 특성으로 표기한 스킬의 글자색. 특성 단어와 눈으로 구분되게 다른 색을 주는 게 좋다.")]
    public Color signatureSkillColor = new Color(0.95f, 0.55f, 0.85f); // 고유 스킬 - 분홍

    [Tooltip("펼침 시작 후 텍스트가 뜨기 시작할 시각(초). 펼침 클립 길이의 8할쯤으로 맞추면 '다 펼쳐질 때쯤' 뜬다. " +
             "클립에 Animation Event로 AnimEvent_BookOpened를 걸어두면 그게 먼저 걸리고 이 값은 안전망(타임아웃)으로만 쓰인다.")]
    [SerializeField] private float revealDelay = 0.5f;
    [Tooltip("서서히 뜨는 데 걸리는 시간(초). 0이면 즉시 표시(연출 없이 기존 거동).")]
    [SerializeField] private float fadeDuration = 0.35f;

    // 진행 중인 페이드. 다른 적을 연달아 누르면 앞선 페이드를 끊고 처음부터 다시 한다.
    private CancellationTokenSource revealCts;
    // Animation Event가 왔는지. revealDelay는 이벤트를 안 걸었을 때를 위한 안전망이다
    // (EnemyBurrow가 파고들기 이벤트에 타임아웃을 함께 두는 것과 같은 구조).
    private bool bookOpenedEvent;
    // 잠금 문구 팝업을 띄울 차례인지. 이건 풀에서 새로 꺼내는 오브젝트라 미리 alpha를 낮춰 둘 수가 없어
    // (SetRevealAlpha가 닿지 않는다) 스폰 자체를 펼침이 끝나는 시점까지 미룬다.
    private bool pendingLockedPopup;

    /// <summary>책이 펼쳐지고 있는 동안 true(펼침이 끝나 텍스트가 뜨기 시작하면 false).
    /// EnemyArchive가 이걸 보고 클릭을 무시한다 — 펼치는 중에 다른 적으로 갈아끼우면 애니가 끊겨 보인다.</summary>
    public bool IsBookOpening { get; private set; }

    private string enemyName;
    private string enemyDesc;
    private string enemyType;
    private string enemyAttribute;
    private readonly List<string> skillNames = new();
    private readonly List<string> skillDescs = new();
    private int page;
    private int LastPage => skillDescs.Count;

    void Awake()
    {
        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.RemoveAllListeners();
            leftArrowButton.onClick.AddListener(PrevPage);
        }
        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.RemoveAllListeners();
            rightArrowButton.onClick.AddListener(NextPage);
        }
        ApplyTitle();
        Clear();
    }

    void OnEnable()
    {
        // 이 패널의 문구는 코드가 직접 채우므로 LocalizeText가 붙지 않는다.
        // 언어를 바꿔도 갱신되지 않으니 여기서 직접 이벤트를 구독한다.
        LocalizeTextManager.OnLanguageChanged += Relocalize;
    }

    void OnDisable()
    {
        LocalizeTextManager.OnLanguageChanged -= Relocalize;
        Clear();
    }

    private void ApplyTitle()
    {
        SetText(m_ArchiveText, DataTableManager.StringTable.Get("Ui_EnemyArchive"));
    }

    // 언어가 바뀌면 캐시해둔 문자열을 버리고 지금 보고 있던 적으로 다시 만든다.
    // Info()가 page를 0으로 되돌리므로 보고 있던 페이지는 따로 보존한다.
    private void Relocalize()
    {
        ApplyTitle();
        if (current == null) return;
        // 잠금 화면은 lockedName/lockedMessage(인스펙터 문자열)라 StringTable과 무관 — 다시 그릴 것이 없다.
        // 여기서 Info()를 부르면 ShowLocked()가 다시 돌아 잠금 팝업만 또 뜬다.
        if (!EnemyArchiveData.IsUnlocked(current.Name)) return;

        int keepPage = page;
        Info(current, playOpenAnimation: false); // 언어만 바뀐 것이라 책을 다시 펼치지 않는다
        page = Mathf.Clamp(keepPage, 0, LastPage);
        Show();
    }

    // 지금 표시 중인 적. 언어 전환 시 이걸로 문자열을 다시 만든다.
    private EnemyTable.Data current;

    // 처음엔 비워둔 상태로 시작
    public void Clear()
    {
        CancelReveal();
        SetRevealAlpha(1f);   // 페이드 도중 닫혀도 다음에 열 때 글자가 투명하게 남아 있지 않게
        current = null;
        enemyName = string.Empty;
        enemyDesc = string.Empty;
        enemyType = string.Empty;
        enemyAttribute = string.Empty;
        skillNames.Clear();
        skillDescs.Clear();
        page = 0;
        SetText(e_NameText, string.Empty);
        SetText(e_DescText, string.Empty);
        SetText(e_TypeText, string.Empty);
        SetText(e_Attribute, string.Empty);
        SetText(e_SkillNameText, string.Empty);
        SetText(e_SkillDescText, string.Empty);
        if (e_Image != null) e_Image.enabled = false;
        SetActive(leftArrowButton, false);
        SetActive(rightArrowButton, false);
    }

    public void Info(EnemyTable.Data data) => Info(data, true);

    /// <summary>playOpenAnimation=false면 책을 다시 펼치지 않고 글자만 갈아끼운다(언어 전환 등).</summary>
    public void Info(EnemyTable.Data data, bool playOpenAnimation)
    {
        if (data == null) { Clear(); return; }

        current = data; // 언어 전환 시 다시 그릴 대상(미해금이어도 기억해둬야 잠금 문구가 갱신된다)

        // 해금 여부와 무관하게 책은 펼쳐진다 — 잠금 문구도 같이 서서히 떠야 자연스럽다.
        if (playOpenAnimation) PlayOpenAndReveal();

        if (!EnemyArchiveData.IsUnlocked(data.Name)) { ShowLocked(); return; }

        var st = DataTableManager.StringTable;

        enemyName = st.Get(data.Name);
        enemyDesc = st.Get(data.Desc);
        enemyType = $"{st.Get("Ui_Type")} : {st.Get(data.Type)}";
        enemyAttribute = $"{st.Get("Ui_Attribute")} : {LocalizeAttributes(data.Attribute)}";
        skillNames.Clear();
        skillDescs.Clear();
        if (!string.IsNullOrEmpty(data.Skills) &&
            !data.Skills.Equals("None", System.StringComparison.OrdinalIgnoreCase))
        {
            var skillTable = DataTableManager.SkillTable;
            foreach (var raw in data.Skills.Split(';'))
            {
                var id = raw.Trim();
                if (string.IsNullOrEmpty(id)) continue;
                var skill = skillTable.Get(id);
                if (skill == null) continue;
                skillNames.Add(st.Get(skill.NameKey));
                skillDescs.Add(st.Get(skill.Desc));
            }
        }

        if (e_Image != null)
        {
            e_Image.sprite = Resources.Load<Sprite>($"EnemyIcons/{data.Name}");
            e_Image.enabled = e_Image.sprite != null;
        }

        bool unlocked = EnemyArchiveData.IsUnlocked(data.Name);

        if (border != null)
            border.color = (!unlocked && grayWhenLocked) ? lockedColor : ClassColor(data.Class);
        page = 0;
        Show();
    }
    private void ShowLocked()
    {
        page = 0;
        skillNames.Clear();
        skillDescs.Clear();
        // 책이 펼쳐지는 중이면 예약만 하고 RevealAfterOpen이 다 펼쳐진 뒤 띄운다 —
        // 여기서 바로 띄우면 다른 텍스트·아이콘이 아직 투명한 동안 이것만 튀어나온다.
        if (IsBookOpening) pendingLockedPopup = true;
        else SpawnLockedPopup();
        SetText(e_NameText, lockedName);
        SetText(e_DescText, lockedMessage);
        SetText(e_TypeText, string.Empty);
        SetText(e_Attribute, string.Empty);
        SetText(e_SkillNameText, string.Empty);
        SetText(e_SkillDescText, string.Empty);
        if (e_Image != null)
        {
            // 목록 버튼과 같은 ? 이미지를 띄운다. 안 꽂혀 있으면(null) 예전처럼 그냥 숨긴다.
            e_Image.sprite = lockIcon;
            e_Image.enabled = lockIcon != null;
        }
        SetActive(leftArrowButton, false);
        SetActive(rightArrowButton, false);
    }

    // 잠금 문구 팝업을 풀에서 꺼내 1초 뒤 되돌린다.
    // PoolManager가 아직 없을 수도 있어(씬 전환 중 등) null을 확인한다 — 없으면 팝업만 생략한다.
    private void SpawnLockedPopup()
    {
        if (lockedEnemyText == null || PoolManager.Instance == null) return;

        GameObject go = PoolManager.Instance.Spawn(lockedEnemyText, transform.position, Quaternion.identity, transform);
        PoolManager.Instance.Despawn(go, 1f,false);
    }

    private void NextPage()
    {
        if (page >= LastPage) return;
        page++;
        Show();
    }

    private void PrevPage()
    {
        if (page <= 0) return;
        page--;
        Show();
    }

    private void Show()
    {
        SetText(e_NameText, enemyName);   // 적 이름은 항상 유지
        SetText(e_TypeText, enemyType);
        SetText(e_Attribute, enemyAttribute);
        if (page == 0)
        {
            SetText(e_DescText, enemyDesc);
            SetText(e_SkillNameText, string.Empty);
            SetText(e_SkillDescText, string.Empty);
        }
        else
        {
            int i = page - 1;
            SetText(e_DescText, string.Empty);          // 기존(적) 설명 비움
            SetText(e_SkillNameText, skillNames[i]);
            SetText(e_SkillDescText, skillDescs[i]);
        }

        // 왼쪽: 뒤로 갈 페이지 있을 때 / 오른쪽: 넘길 페이지 있을 때
        SetActive(leftArrowButton, page > 0);
        SetActive(rightArrowButton, page < LastPage);
    }
    private string LocalizeAttributes(string raw)
        => EnemyAttributeText.Build(raw, AttrColor, signatureSkillColor, attributeSeparator, this);

    // 특성별 글자색
    private Color AttrColor(EnemyAttribute f)
    {
        switch (f)
        {
            case EnemyAttribute.Cloaking:     return cloakingColor;
            case EnemyAttribute.Fly:          return flyColor;
            case EnemyAttribute.UnJudged:     return unJudgedColor;
            case EnemyAttribute.Berserk:      return berserkColor;
            case EnemyAttribute.Regeneration: return regenerationColor;
            case EnemyAttribute.HitsShield:   return hitsShieldColor;
            case EnemyAttribute.Burrow: return burrowColor;
            case EnemyAttribute.Swim: return SwimColor;
            case EnemyAttribute.Flame: return FlameColor;
            default:                          return Color.white;
        }
    }

    public void AnimEvent_BookOpened() => bookOpenedEvent = true;

    // 책을 펼치고, 다 펼쳐질 때쯤 텍스트를 서서히 띄운다.
    private void PlayOpenAndReveal()
    {
        CancelReveal();
        bookOpenedEvent = false;

        //if (bookAnimator != null)
        //{
        //    // 같은 트리거가 큐에 남아 있으면 다음 펼침이 즉시 소비돼 버린다 — 눌러 둔 것을 먼저 지운다.
        //    bookAnimator.ResetTrigger(openTrigger);
        //    bookAnimator.SetTrigger(openTrigger);
        //}

        SetRevealAlpha(0f);   // 펼치는 동안은 텍스트·아이콘을 감춰 둔다
        IsBookOpening = true; // 이 사이엔 EnemyArchive가 적 버튼 클릭을 무시한다
        revealCts = new CancellationTokenSource();
        RevealAfterOpen(revealCts).Forget();
    }

    private void CancelReveal()
    {
        revealCts?.Cancel();
        revealCts?.Dispose();
        revealCts = null;
        IsBookOpening = false;   // 취소로 끝나도 잠금이 남으면 클릭이 영구히 막힌다
        pendingLockedPopup = false;  // 중간에 끊겼으면 예약해 둔 팝업도 버린다(다음 적 위로 뒤늦게 뜨지 않게)
    }

    // own을 그대로 받는 이유 — 취소된 앞선 작업의 finally가 뒤늦게 깨어나
    // 새로 시작한 연출의 IsBookOpening을 꺼버리는 것을 막는다(revealCts와 같은지 확인).
    private async UniTask RevealAfterOpen(CancellationTokenSource own)
    {
        CancellationToken token = own.Token;
        try
        {
            // 도감은 Time.timeScale이 0인 동안에도 열리므로(일시정지 중 확인) 전부 unscaled로 잰다.
            // 같은 이유로 bookAnimator의 Update Mode도 Unscaled Time이어야 펼침 애니가 멈추지 않는다.
            float t = 0f;
            while (!bookOpenedEvent && t < revealDelay)
            {
                t += Time.unscaledDeltaTime;
                await UniTask.Yield(token);
            }

            IsBookOpening = false;   // 펼침 끝 — 여기서부터 다른 적으로 넘어갈 수 있다

            // 미해금 적이면 이제야 잠금 문구를 띄운다. 아래 페이드와 같은 프레임에 시작하므로
            // 이름·설명·? 아이콘이 떠오르는 것과 같은 타이밍이 된다.
            if (pendingLockedPopup)
            {
                pendingLockedPopup = false;
                SpawnLockedPopup();
            }

            t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                SetRevealAlpha(Mathf.Clamp01(t / fadeDuration));
                await UniTask.Yield(token);
            }
            SetRevealAlpha(1f);
        }
        finally
        {
            // 취소(패널 닫힘 등)로 빠져나가도 잠금을 반드시 푼다. 단 지금 살아있는 연출이 내 것일 때만.
            if (revealCts == own) IsBookOpening = false;
        }
    }

    // textGroup이 꽂혀 있으면 그쪽 alpha 하나로, 없으면 텍스트들과 적 아이콘의 alpha를 직접 건드린다.
    // 도감 제목(m_ArchiveText)은 책과 무관하게 항상 보여야 하므로 건드리지 않는다.
    private void SetRevealAlpha(float a)
    {
        // if (textGroup != null) { textGroup.alpha = a; return; }

        SetAlpha(e_NameText, a);
        SetAlpha(e_TypeText, a);
        SetAlpha(e_Attribute, a);
        SetAlpha(e_DescText, a);
        SetAlpha(e_SkillNameText, a);
        SetAlpha(e_SkillDescText, a);
        SetAlpha(e_Image, a);
    }

    private static void SetAlpha(TMP_Text t, float a)
    {
        if (t != null) t.alpha = a;
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    private static void SetText(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }

    private static void SetActive(Button b, bool on)
    {
        if (b != null) b.gameObject.SetActive(on);
    }

    private Color ClassColor(string cls)
    {
        if (Enum.TryParse(cls, true, out EnemyClass c))
        {
            switch (c)
            {
                case EnemyClass.Boss: return bossColor;
                case EnemyClass.Elite: return eliteColor;
            }
        }
        return normalColor;   // Normal 또는 파싱 실패
    }
}
