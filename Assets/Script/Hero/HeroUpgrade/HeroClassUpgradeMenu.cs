using UnityEngine;
using VContainer;
using VContainer.Unity;

// 열고 닫는 건 이 컴포넌트가 붙은 GameObject를 그대로 들고 있는 BuildModePanel.classUpgradePanel이
// 전담한다(ESC 우선순위 취소 체인, 바깥 클릭 판정 전부 BuildModePanel 소유) - 여기서 또
// UiPanelStack/ClickOutsideCloser로 독립적으로 열고 닫으면 같은 오브젝트를 두 군데서 따로
// 닫으려 들어 충돌한다. 이 클래스는 목록 내용만 채운다.
public class HeroClassUpgradeMenu : MonoBehaviour
{
    [SerializeField] private HeroClassUpgradeMenuUI upgradeMenuUIPrefab;
    [SerializeField] private GameObject menuContent;
    private HeroClassUpgradeConfig config;
    private IObjectResolver resolver;

    [Inject]
    private void Construct(HeroClassUpgradeConfig config, IObjectResolver resolver)
    {
        this.config = config;
        this.resolver = resolver;
    }

    private void Awake()
    {
        for (int i = 0; i < config.classEntries.Count; i++)
        {
            HeroClassUpgradeMenuUI upgradeMenuUI = resolver.Instantiate(upgradeMenuUIPrefab, menuContent.transform);
            upgradeMenuUI.Set(config.classEntries[i].heroType);
        }
    }
}
