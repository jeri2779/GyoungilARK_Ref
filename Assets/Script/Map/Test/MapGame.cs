using UnityEngine;
using VContainer;

// VContainer 주입을 받아 배치 담당을 조립한다. 맵 로직은 하나도 갖지 않는다.
public class MapGame : MonoBehaviour
{
    private readonly PlacedUnitData unitList = new();
    private UnitPlacer unitPlacer;
    private UiManager uiManager;
    private GameManager gameManager;
    private ResourcesManager resourcesManager;
    private CitizenManager citizenManager;
    private EnviromentManager enviromentManager;
    private DayNightData dayNightData;
    private HeroRoster heroRoster;
    private BuffManager buffManager;
    private PlayerManaManager playerManaManager;

    public PlacedUnitData Units { get { return unitList; } }
    public UnitPlacer Placer { get { return unitPlacer; } }
    public UiManager Ui { get { return uiManager; } }
    public GameManager Rule { get { return gameManager; } }
    public ResourcesManager ResourcesManager { get { return resourcesManager; } }
    public CitizenManager CitizenManager { get { return citizenManager; } }
    public EnviromentManager EnviromentManager { get { return enviromentManager; } }
    public DayNightData DayNightData { get { return dayNightData; } }
    public HeroRoster HeroRoster { get { return heroRoster; } }
    public BuffManager BuffManager { get { return buffManager; } }
    public PlayerManaManager PlayerManaManager { get { return playerManaManager; } }


    [Inject]
    private void Construct(ResourcesManager resourcesManager, UiManager uiManager, GameManager gameManager, CitizenManager citizenManager, EnviromentManager enviromentManager, DayNightData dayNightData, HeroRoster heroRoster, BuffManager buffManager, PlayerManaManager playerManaManager)
    {
        this.uiManager = uiManager;
        this.gameManager = gameManager;
        this.resourcesManager = resourcesManager;
        this.citizenManager = citizenManager;
        this.enviromentManager = enviromentManager;
        this.dayNightData = dayNightData;
        this.heroRoster = heroRoster;
        this.buffManager = buffManager;
        this.playerManaManager = playerManaManager;
        unitPlacer = new UnitPlacer();
        unitPlacer.unitList = unitList;
    }
}
