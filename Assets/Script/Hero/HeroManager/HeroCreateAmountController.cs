using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 근접/원거리 생성 아이콘을 클릭하면 그 대상으로 갱신되는, 항상 떠있는 수량 선택 섹션.
// 슬라이더<->입력창 동기화와 수량에 비례한 자원 표시, 부족 사유(reasonText) 표시만 담당하고,
// 실제 비용 차감/영웅 생성/인구 처리는 HeroSetPanel이 넘겨준 onBuy 콜백에서 처리한다.
public class HeroCreateAmountController : MonoBehaviour
{
    [SerializeField] private Slider amountSlider;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject resourceContainer;
    [SerializeField] private HeroUpgradeResourcesUI resourceRowPrefab;
    [SerializeField] private TextMeshProUGUI reasonText; // 생성 불가 사유 - 없으면 비활성화

    private readonly List<HeroUpgradeResourcesUI> rows = new();
    private Func<int, (ProductionType Type, int Amount)[]> getCost; // 수량을 넣으면 그만큼(점증 가격 포함) 총 비용을 계산해 준다
    private bool[] sufficient; // getCost(1)과 같은 인덱스 - 해당 자원 하나라도 감당 가능한지
    private List<ResourceIcon> resourceIcons;
    private Action<int> onBuy;
    private int maxAmount;
    private int amount;
    private bool syncing;

    private void Awake()
    {
        amountSlider.wholeNumbers = true;
        amountSlider.onValueChanged.AddListener(OnSliderChanged);
        amountInput.onValueChanged.AddListener(OnInputChanged);
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    public void SetTarget(List<ResourceIcon> resourceIcons, Func<int, (ProductionType Type, int Amount)[]> getCost,
        int maxAmount, bool[] sufficient, bool canUseCitizen, string reasonKey, Action<int> onBuy)
    {
        this.resourceIcons = resourceIcons;
        this.getCost = getCost;
        this.sufficient = sufficient;
        this.onBuy = onBuy;
        amount = 1;

        RefreshMax(maxAmount, canUseCitizen);
        SetReason(reasonKey);
    }

    private void RefreshMax(int maxAmount, bool canUseCitizen)
    {
        this.maxAmount = Mathf.Max(0, maxAmount);
        amountSlider.minValue = this.maxAmount > 0 ? 1 : 0;
        amountSlider.maxValue = this.maxAmount;
        buyButton.interactable = this.maxAmount > 0 && canUseCitizen;
        SetAmount(amount);
    }

    // reasonKey가 없으면(=생성 가능) 숨기고, 있으면 StringTable에서 찾아 표시한다.
    private void SetReason(string reasonKey)
    {
        if (string.IsNullOrEmpty(reasonKey))
        {
            reasonText.gameObject.SetActive(false);
            return;
        }
        reasonText.gameObject.SetActive(true);
        reasonText.text = DataTableManager.StringTable.Get(reasonKey);
    }

    private void OnSliderChanged(float value)
    {
        if (syncing) return;
        SetAmount(Mathf.RoundToInt(value));
    }

    private void OnInputChanged(string text)
    {
        if (syncing) return;
        SetAmount(int.TryParse(text, out int value) ? value : amount);
    }

    private void SetAmount(int value)
    {
        amount = Mathf.Clamp(value, (int)amountSlider.minValue, maxAmount);

        syncing = true;
        amountSlider.value = amount;
        amountInput.text = amount.ToString();
        syncing = false;

        RefreshCostDisplay();
    }

    private void RefreshCostDisplay()
    {
        if (getCost == null) return;

        // 자원이 부족해 maxAmount가 0이 돼도(amount도 0으로 클램프됨) 자원 행엔 "0"이 아니라
        // 1개 만들 때의 실제 금액을 보여준다 - 실제 구매 가능 수량(amount)엔 영향 없음.
        var scaled = getCost(Mathf.Max(amount, 1));
        for (int i = 0; i < scaled.Length; i++)
        {
            HeroUpgradeResourcesUI row = i < rows.Count ? rows[i] : CreateRow();
            row.gameObject.SetActive(true);
            row.SetIcon(FindIcon(resourceIcons, scaled[i].Type));
            bool rowSufficient = sufficient == null || i >= sufficient.Length || sufficient[i];
            row.SetAmount(-scaled[i].Amount, rowSufficient);
        }
        for (int i = scaled.Length; i < rows.Count; i++)
            rows[i].gameObject.SetActive(false);
    }

    private HeroUpgradeResourcesUI CreateRow()
    {
        HeroUpgradeResourcesUI row = Instantiate(resourceRowPrefab, resourceContainer.transform);
        rows.Add(row);
        return row;
    }

    private static Sprite FindIcon(List<ResourceIcon> icons, ProductionType type)
    {
        foreach (ResourceIcon i in icons)
            if (i.type == type) return i.icon;
        return null;
    }

    private void OnBuyClicked()
    {
        int bought = amount;
        onBuy?.Invoke(bought);
    }
}
