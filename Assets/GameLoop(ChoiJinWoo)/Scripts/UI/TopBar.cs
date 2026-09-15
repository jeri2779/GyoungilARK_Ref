using TMPro;
using UnityEngine;
using VContainer;

public class TopBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI woodText;
    [SerializeField] private TextMeshProUGUI stoneText;
    [SerializeField] private TextMeshProUGUI ironText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI foodText;
    [SerializeField] private TextMeshProUGUI specialText;
    [SerializeField] private TextMeshProUGUI citizenText;
    [SerializeField] private TextMeshProUGUI maxCitizenText;
    [SerializeField] private TextMeshProUGUI idleCitizenText;
    [SerializeField] private TextMeshProUGUI heroCitizenText;
    [SerializeField] private TextMeshProUGUI LifeText;
    [SerializeField] private GameObject heroCitizen;
    private ResourcesManager resourcesManager;
    private CitizenManager citizenManager;
    private GameManager gameManager;

    [Inject]
    private void Construct(ResourcesManager resourcesManager, CitizenManager citizenManager, GameManager gameManager)
    {
        this.resourcesManager = resourcesManager;
        this.citizenManager = citizenManager;
        this.gameManager = gameManager;
    }

    // 프리팹으로 컨테이너가 직접 Instantiate하면 Awake가 주입보다 먼저 실행돼(Unity의 Instantiate는
    // 컴포넌트 생성 즉시 동기로 Awake를 호출하고, VContainer의 주입은 그 Instantiate 호출이 끝난 뒤에야
    // 이어서 처리된다) resourcesManager 등이 아직 null이다. Start는 그 다음 업데이트에서 호출되므로
    // 그때는 주입이 이미 끝나있어 안전하다.
    private void Start()
    {
        resourcesManager.ProductUpdate += UpdateResourcesUI;
        citizenManager.CitizenChanged += UpdateCitizenUi;
        gameManager.HpChanged += UpdateLife;
        UpdateResourcesUI();
        UpdateCitizenUi();
        UpdateLife();
    }

    private void OnDestroy()
    {
        resourcesManager.ProductUpdate -= UpdateResourcesUI;
        citizenManager.CitizenChanged -= UpdateCitizenUi;
        gameManager.HpChanged -= UpdateLife;
    }

    private void UpdateResourcesUI()
    {
        woodText.text = $"{resourcesManager.Wood}";
        stoneText.text = $"{resourcesManager.Stone}";
        ironText.text = $"{resourcesManager.Iron}";
        goldText.text = $"{resourcesManager.Gold}";
        foodText.text = $"{resourcesManager.Food}";
        specialText.text = $"{resourcesManager.Special}";
    }

    private void UpdateLife()
    {
        if (LifeText == null) return;
        LifeText.text = $"{gameManager.Hp}";
    }

    private void UpdateCitizenUi()
    {
        if (citizenText != null) citizenText.text = $"{citizenManager.CurrentCitizen}";
        if (maxCitizenText != null) maxCitizenText.text = $"{citizenManager.MaxCitizen}";
        if (idleCitizenText != null) idleCitizenText.text = $"{citizenManager.CanUseCitizen}";
        if (heroCitizenText != null) heroCitizenText.text = $"{citizenManager.HeroUsedCitizen}";
    }
}
