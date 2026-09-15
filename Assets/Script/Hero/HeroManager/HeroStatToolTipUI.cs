using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class HeroStatToolTipUI : MonoBehaviour
{
    public static HeroStatToolTipUI Instance { get; private set; }
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI hpStatus;
    [SerializeField] private TextMeshProUGUI atkStatus;
    [SerializeField] private TextMeshProUGUI defStatus;
    [SerializeField] private TextMeshProUGUI asStatus;
    [SerializeField] private TextMeshProUGUI blkStatus;
    private CanvasGroup canvasGroup;
    [SerializeField] private Canvas canvas; // 화면 좌표 -> 캔버스 로컬 좌표 변환에 필요
    [SerializeField] private Vector2 offset = new(16f, 16f);
    private RectTransform canvasRect;
    private void Awake()
    {
        Instance = this;
        canvasRect = canvas.transform as RectTransform;
        canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(HeroRosterEntry heroEntry, Vector2 screenPosition)
    {
        if (heroEntry.PlacedUnit != null && heroEntry.PlacedUnit.GetComponent<Hero>() is Hero hero)
        {
            StatContainer stat = hero.SC;
            hpStatus.text = $"{(int)stat[StatType.HP]}";
            atkStatus.text = $"{stat[StatType.ATK]:N1}";
            defStatus.text = $"{stat[StatType.DEF]:N1}";
            asStatus.text = $"{stat[StatType.AS]:N1}";
            blkStatus.text = $"{(int)stat[StatType.BLK]}";
        }
        else
        {
            var stats = HeroStatManager.GetAll(heroEntry.Data);
            hpStatus.text = $"{(int)stats[StatType.HP]}";
            atkStatus.text = $"{stats[StatType.ATK]:N1}";
            defStatus.text = $"{stats[StatType.DEF]:N1}";
            asStatus.text = $"{stats[StatType.AS]:N1}";
            blkStatus.text = $"{(int)stats[StatType.BLK]}";
        }

        SetPosition(screenPosition);
        nameText.text = DataTableManager.StringTable.Get(heroEntry.Data.HeroNameKey);
        
        panel.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
    }
    public void Hide()
    {
        canvasGroup.alpha = 0f;
    }
    private void SetPosition(Vector2 screenPosition)
    {
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out var localPoint);

        bool isRightHalf = localPoint.x > canvasRect.rect.center.x;
        panel.pivot = new Vector2(isRightHalf ? 1f : 0f, 0f);
        float offsetX = isRightHalf ? -offset.x : offset.x;

        panel.anchoredPosition = ClampToCanvas(localPoint + new Vector2(offsetX, offset.y));
    }

    // 패널이 화면(캔버스) 밖으로 잘려나가지 않도록 anchoredPosition을 캔버스 경계 안으로 눌러 담는다.
    // panel의 anchorMin == anchorMax(늘어나지 않는 고정 앵커)인 일반적인 툴팁 설정을 가정한다.
    private Vector2 ClampToCanvas(Vector2 desired)
    {
        var bounds = canvasRect.rect;
        var anchorPoint = bounds.min + Vector2.Scale(bounds.size, panel.anchorMin);
        var pivotOffset = Vector2.Scale(panel.rect.size, panel.pivot);

        var minX = bounds.xMin - anchorPoint.x + pivotOffset.x;
        var maxX = bounds.xMax - anchorPoint.x - (panel.rect.width - pivotOffset.x);
        var minY = bounds.yMin - anchorPoint.y + pivotOffset.y;
        var maxY = bounds.yMax - anchorPoint.y - (panel.rect.height - pivotOffset.y);

        // 패널이 캔버스보다 크면 min > max가 되어버리니, 그런 경우엔 최소값에 고정한다.
        float x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : minX;
        float y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : minY;
        return new Vector2(x, y);
    }
}