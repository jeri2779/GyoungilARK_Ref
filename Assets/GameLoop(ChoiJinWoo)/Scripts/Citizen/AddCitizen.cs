using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

public class AddCitizen : MonoBehaviour
{
    private CitizenManager citizenManager;
    private ResourcesManager resourcesManager;
    private ResourceIconSet resourceIconSet;
    [SerializeField] private TMP_InputField amountInput;
    [SerializeField] private Image costIcon;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private int costAmount;
    [SerializeField] private RectTransform openButtonRect; // 이 패널을 여닫는 토글 버튼 — 바깥 클릭 판정에서 제외
    [SerializeField] private RectTransform createButtonRect; // "생성" 확인 버튼 - 튜토리얼 스포트라이트용 참조

    public RectTransform CreateButtonRect => createButtonRect;
    private int amount = 0;
    private ClickOutsideCloser outsideCloser;
    private PanelReveal panelReveal;
    private InputAction rightClickAction;

    [Inject]
    private void Construct(CitizenManager citizenManager, ResourcesManager resourcesManager, ResourceIconSet resourceIconSet)
    {
        this.citizenManager = citizenManager;
        this.resourcesManager = resourcesManager;
        this.resourceIconSet = resourceIconSet;
    }

    private void Awake()
    {
        panelReveal = GetComponent<PanelReveal>();
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, openButtonRect);

        // 인스펙터 편집 편의상 활성 상태로 저장돼 있어 스케일이 (1,1,1)로 남아있다 - 여기서
        // 스케일만 0으로 맞춰 첫 Show()가 확대 연출 없이 바로 나타나는 걸 막는다.
        if (panelReveal != null) transform.localScale = Vector3.zero;

        rightClickAction = new InputAction("AddCitizenRightClickClose", InputActionType.Button, "<Mouse>/rightButton");
        rightClickAction.performed += OnRightClickPerformed;
    }

    // 패널이 열려있는 동안만(=이 오브젝트가 활성 상태인 동안만) 반응하도록 이전 Update()와 같은 생명주기로 맞춘다.
    private void OnEnable()
    {
        GlobalUiInputSignals.ClickPerformed += HandleOutsideClick;
        GlobalUiInputSignals.EscapePerformed += HandleEscape;
        rightClickAction.Enable();
    }

    private void OnDisable()
    {
        GlobalUiInputSignals.ClickPerformed -= HandleOutsideClick;
        GlobalUiInputSignals.EscapePerformed -= HandleEscape;
        rightClickAction.Disable();
    }

    private void OnDestroy()
    {
        rightClickAction.performed -= OnRightClickPerformed;
        rightClickAction.Dispose();
    }

    public void OpenPanel()
    {
        if(gameObject.activeSelf)
            Close();
        else
        {
            // CenterHubPanel.Awake()가 이 오브젝트를 자기 Awake보다 먼저 SetActive(false)로 꺼버리면
            // 유니티가 이 컴포넌트의 Awake 자체를 얼마간 미뤄서, panelReveal이 아직 null인 채로 첫
            // OpenPanel()이 불릴 수 있다 - 그래서 캐시를 못 믿고 매번 여기서 다시 확인한다.
            if (panelReveal == null) panelReveal = GetComponent<PanelReveal>();
            if (panelReveal != null) panelReveal.Show();
            else gameObject.SetActive(true);
            outsideCloser.MarkOpened();
            amount = 0;
            UpdatePanel();
        }
    }

    public void Close()
    {
        if (panelReveal == null) panelReveal = GetComponent<PanelReveal>();
        if (panelReveal != null) panelReveal.Hide();
        else gameObject.SetActive(false);
    }

    private void OnRightClickPerformed(InputAction.CallbackContext context)
    {
        if (TutorialInputGate.BlockEscapeClose) return;
        Close();
    }

    private void HandleEscape()
    {
        if (TutorialInputGate.BlockEscapeClose) return;
        Close();
    }

    // ClickOutsideCloser.ClickedOutside()가 TutorialInputGate.BlockEscapeClose를 내부에서 이미 확인한다.
    private void HandleOutsideClick()
    {
        if (outsideCloser.ClickedOutside())
        {
            Close();
        }
    }

    private (ProductionType Type, int Amount)[] GetCost()
    {
        return new (ProductionType Type, int Amount)[] { (ProductionType.Food, amount * -costAmount) };
    }

    private void UpdatePanel()
    {
        amountInput.text = $"{amount}";
        if (costIcon != null) costIcon.sprite = resourceIconSet.GetIcon(ProductionType.Food);
        costText.text = $"{amount * costAmount}";
        costText.color = resourcesManager.CheckResources(GetCost()) ? Color.white : Color.red;
    }

    public void ChangeAmount(string text)
    {
        int max = Mathf.Max(0, citizenManager.MaxCitizen - citizenManager.CurrentCitizen);

        if (string.IsNullOrEmpty(text))
        {
            amount = 0;
        }
        else if (long.TryParse(text, out long parsed))
        {
            amount = (int)Math.Clamp(parsed, 0, max);
        }
        else
        {
            // long 범위조차 넘는 큰 수 입력 시 최대값으로 처리
            amount = max;
        }

        UpdatePanel();
    }

    public void IncreaseAmount()
    {
        if(amount + citizenManager.CurrentCitizen < citizenManager.MaxCitizen)
            amount++;
        UpdatePanel();
        ClearButtonFocus();
    }

    public void DecreaseAmount()
    {
        if(amount > 0)
            amount--;
        UpdatePanel();
        ClearButtonFocus();
    }

    public void IncreaseTen()
    {
        if (amount + citizenManager.CurrentCitizen < citizenManager.MaxCitizen)
            amount = Mathf.Clamp(amount + 10, 0, citizenManager.MaxCitizen - citizenManager.CurrentCitizen);
        UpdatePanel();
        ClearButtonFocus();
    }

    public void DecreaseTen()
    {
        if (amount > 0)
            amount = Mathf.Clamp(amount - 10, 0, citizenManager.MaxCitizen - citizenManager.CurrentCitizen);
        UpdatePanel();
        ClearButtonFocus();
    }

    // Selected 상태가 Pressed와 같은 클립이라, 포커스를 안 풀면 눌린 모양이 계속 남는다
    private void ClearButtonFocus()
    {
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void CreateCitizen()
    {
        var cost = GetCost();

        if (citizenManager.CheckCanIncreaseCitizen(amount) && resourcesManager.CheckResources(cost))
        {
            citizenManager.IncreaseCitizen(amount);
            resourcesManager.ProductChanged(cost);
        }

        Close();
    }
}
