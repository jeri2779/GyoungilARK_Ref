using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroArchiveUI : MonoBehaviour
{
    [SerializeField] private HeroArchiveItem itemPrefab;
    [SerializeField] private HeroDescItem descItemPrefab;
    [SerializeField] private HeroRegistry registry;
    [SerializeField] private Transform listContent;

    [SerializeField] private Image mainImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Button exitButton;
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private CanvasGroup leftPanel;
    [SerializeField] private CanvasGroup rightPanel;
    [SerializeField] private HeroArchiveButton heroArchiveButton;
    private float fadeDuration = 0.35f;
    private CancellationTokenSource revealCts;
    //private bool isBookOpening = false;
    private readonly Dictionary<HeroData, List<HeroDescItem>> descItemGroups = new();
    private HeroData currentHero;

    private void Awake()
    {
        BuildList();
    }

    //private void OnDisable()
    //{
    //    ClearList();
    //}

    private void OnEnable()
    {
        if (currentHero != null) ApplyHeroText(currentHero);
    }

    private void BuildList()
    {
        //ClearList();
        foreach (HeroData data in registry.AllHeroDatas)
        {
            HeroArchiveItem item = Instantiate(itemPrefab, listContent);
            item.Setup(data, OnHeroArchiveClicked);
        }
        foreach (HeroData data in registry.AllHeroDatas)
            BuildDescItems(data);

        HeroData heroData = registry.AllHeroDatas[0];
        ApplyHeroText(heroData);
        ShowDescItems(heroData);
    }

    private void ApplyHeroText(HeroData data)
    {
        mainImage.sprite = data.Icon;
        nameText.text = DataTableManager.StringTable.Get(data.HeroNameKey);
        descText.text = DataTableManager.StringTable.Get(data.HeroDescriptionKey);
    }

    private void BuildDescItems(HeroData data)
    {
        var items = new List<HeroDescItem>();
        foreach (AttackDescription desc in data.AttackDescriptions)
        {
            HeroDescItem item = Instantiate(descItemPrefab, rightPanel.transform);
            item.SetUp(desc);
            item.gameObject.SetActive(false);
            items.Add(item);
        }
        descItemGroups[data] = items;
    }

    private void ShowDescItems(HeroData data)
    {
        if (currentHero != null && descItemGroups.TryGetValue(currentHero, out var prevItems))
            foreach (HeroDescItem item in prevItems)
                item.gameObject.SetActive(false);

        if (descItemGroups.TryGetValue(data, out var items))
            foreach (HeroDescItem item in items)
                item.gameObject.SetActive(true);

        currentHero = data;
    }

    private void OnHeroArchiveClicked(HeroData picked)
    {
        EnemySoundManager.Play("BookPage");
        PlayOpenAndReveal();
        ApplyHeroText(picked);
        ShowDescItems(picked);
    }

    public void Refresh()
    {
        PlayOpenAndReveal();
        mainImage.sprite = currentHero.Icon;
        nameText.text = DataTableManager.StringTable.Get(currentHero.HeroNameKey);
        descText.text = DataTableManager.StringTable.Get(currentHero.HeroDescriptionKey);
        ShowDescItems(currentHero);
    }

    private void PlayOpenAndReveal()
    {
        CancelReveal();
        if (bookAnimator != null)
        {
            bookAnimator.ResetTrigger("Open");
            bookAnimator.SetTrigger("Open");
        }
        SetRevealAlpha(0f);
        //isBookOpening = true;
        revealCts = new CancellationTokenSource();
        RevealAfterOpen(revealCts).Forget();
    }

    private void CancelReveal()
    {
        revealCts?.Cancel();
        revealCts?.Dispose();
        revealCts = null;
        //isBookOpening = false;
    }

    private void SetRevealAlpha(float a)
    {
        leftPanel.alpha = a;
        rightPanel.alpha = a;
    }

    private async UniTask RevealAfterOpen(CancellationTokenSource own)
    {
        CancellationToken token = own.Token;
        try
        {
            float t = 0f;
            float revealDelay = 0.5f;

            while (t < revealDelay)
            {
                t += Time.unscaledDeltaTime;
                await UniTask.Yield(token);
            }
            //isBookOpening = false;

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
            if (revealCts == own)
            {
                //isBookOpening = false;
            }
        }
    }

    private void ClearList()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);
    }
    public void OnExit()
    {
        if (heroArchiveButton != null) heroArchiveButton.Close();
        else gameObject.SetActive(false);
    }
}
