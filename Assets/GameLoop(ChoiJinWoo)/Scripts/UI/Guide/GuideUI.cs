using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class GuideUI : MonoBehaviour, IExclusiveUiPanel
{
    private const int ContentLeaveDelayMs = 80;

    [SerializeField] private List<SpecificGuide> gamePlayGuides;
    [SerializeField] private List<SpecificGuide> heroGuides;
    [SerializeField] private List<SpecificGuide> baseGuides;
    [SerializeField] private List<SpecificGuide> enemyGuides;
    [SerializeField] private List<SpecificGuide> gimmickGuides;
    [SerializeField] private Image guideImage;
    [SerializeField] private TextMeshProUGUI guideText;
    [SerializeField] private Animator guideCopyAnimator;
    [SerializeField] private Animator guidePictureAnimator;
    [SerializeField] private PanelReveal panelReveal;

    private GimmickTileData gimmickTileData;
    private List<SpecificGuide> activatedButtons = new();

    // 기믹 기록장을 컨테이너에서 받아 둔다(이 패널은 프리팹이라 씬 오브젝트를 직접 참조할 수 없다).
    [Inject]
    private void Construct(GimmickTileData gimmickTileData)
    {
        this.gimmickTileData = gimmickTileData;
    }

    [SerializeField] private Button openButton;

    private ClickOutsideCloser outsideCloser;

    private void Awake()
    {
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, openButton != null ? openButton.transform : null);
    }

    private void OnEnable()
    {
        ExclusiveUiCoordinator.NotifyOpened(this);
        OnGamePlayGuide(true);
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;
    }

    private void OnDisable()
    {
        ExclusiveUiCoordinator.NotifyClosed(this);
        DisableButtons();
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;
    }

    private void HandleCloseCheck()
    {
        if (outsideCloser.ShouldClose()) OnCloseButton();
    }

    // 축소 연출로 닫는다 (연출 부품이 없는 씬에서는 예전처럼 바로 끈다)
    public void OnCloseButton()
    {
        if (panelReveal == null)
        {
            gameObject.SetActive(false);
            return;
        }
        panelReveal.Hide();
    }

    public void RequestClose() => OnCloseButton();

    public void OnGamePlayGuide(bool isInitialOpen = false)
    {
        DisableButtons();

        foreach (var guide in gamePlayGuides)
        {
            guide.gameObject.SetActive(true);
            guide.GuideButton.onClick.AddListener(() => ShowSpecificGuide(guide));
            activatedButtons.Add(guide);
        }

        ShowSpecificGuide(activatedButtons[0], !isInitialOpen);
    }

    public void OnHeroGuide()
    {
        DisableButtons();

        foreach (var guide in heroGuides)
        {
            guide.gameObject.SetActive(true);
            guide.GuideButton.onClick.AddListener(() => ShowSpecificGuide(guide));
            activatedButtons.Add(guide);
        }

        ShowSpecificGuide(activatedButtons[0]);
    }

    public void OnBaseGuide()
    {
        DisableButtons();

        foreach (var guide in baseGuides)
        {
            guide.gameObject.SetActive(true);
            guide.GuideButton.onClick.AddListener(() => ShowSpecificGuide(guide));
            activatedButtons.Add(guide);
        }

        ShowSpecificGuide(activatedButtons[0]);
    }

    public void OnEnemyGuide()
    {
        DisableButtons();

        foreach (var guide in enemyGuides)
        {
            guide.gameObject.SetActive(true);
            guide.GuideButton.onClick.AddListener(() => ShowSpecificGuide(guide));
            activatedButtons.Add(guide);
        }

        ShowSpecificGuide(activatedButtons[0]);
    }

    public void OnGimmickGuide()
    {
        DisableButtons();

        foreach (var guide in gimmickGuides)
        {
            AddWhenRevealed(guide);
        }

        ShowFirstOrEmpty();
    }

    // 그 지역을 이미 봤을 때만 항목을 켠다.
    private void AddWhenRevealed(SpecificGuide guide)
    {
        if (!gimmickTileData.WasShown(guide.GetComponent<GimmickGuideTag>().ModuleId))
        {
            return;
        }
        guide.gameObject.SetActive(true);
        guide.GuideButton.onClick.AddListener(() => ShowSpecificGuide(guide));
        activatedButtons.Add(guide);
    }

    // 보여줄 항목이 없으면 본문을 비우고, 있으면 첫 항목을 연다.
    private void ShowFirstOrEmpty()
    {
        if (activatedButtons.Count == 0)
        {
            guideImage.gameObject.SetActive(false);
            guideText.text = string.Empty;
            return;
        }
        ShowSpecificGuide(activatedButtons[0]);
    }

    private void DisableButtons()
    {
        foreach (var button in activatedButtons)
        {
            button.GuideButton.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }

        activatedButtons.Clear();
    }

    private void ShowSpecificGuide(SpecificGuide guide, bool playLeave = true)
    {
        UpdateItemSelection(guide);
        PlayContentChangeAsync(guide, playLeave).Forget();
    }

    // 현재 카테고리 안에서 클릭한 항목만 선택 상태로 표시한다 (클릭 이펙트는 전역 ClickEffect가 담당)
    private void UpdateItemSelection(SpecificGuide guide)
    {
        for (int i = 0; i < activatedButtons.Count; i++)
        {
            var target = activatedButtons[i];
            target.GetComponent<GuideItemHighlight>().SetSelected(target == guide);
        }
    }

    // 기존 내용을 퇴장시키고, 내용을 바꾼 뒤 새 내용을 등장시킨다 (처음 열 때는 퇴장 단계를 건너뛴다)
    private async UniTaskVoid PlayContentChangeAsync(SpecificGuide guide, bool playLeave)
    {
        if (playLeave)
        {
            if (guideCopyAnimator != null) guideCopyAnimator.Play("Leave", -1, 0f);
            if (guidePictureAnimator != null) guidePictureAnimator.Play("Leave", -1, 0f);

            await UniTask.Delay(ContentLeaveDelayMs);
        }

        ApplyGuideContent(guide);

        if (guideCopyAnimator != null) guideCopyAnimator.Play("Arrive", -1, 0f);
        if (guidePictureAnimator != null) guidePictureAnimator.Play("Arrive", -1, 0f);
    }

    // 가이드 이미지와 본문 텍스트를 실제로 교체한다
    private void ApplyGuideContent(SpecificGuide guide)
    {
        if (guide.GuideImage == null)
            guideImage.gameObject.SetActive(false);
        else
        {
            guideImage.sprite = guide.GuideImage;
            guideImage.gameObject.SetActive(true);
        }
        guideText.text = DataTableManager.StringTable.Get(guide.GuideInfo);
    }
}
