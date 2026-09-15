using UnityEngine;

// 전용 스크립트가 없는 단순 SetActive 토글 패널을 ExclusiveUiCoordinator에 등록하기 위한 범용 컴포넌트.
// 패널 루트 오브젝트에 붙여두면, 그 오브젝트를 켜고 끄는 코드가 프로젝트 어디에 몇 군데 흩어져 있든
// OnEnable/OnDisable은 항상 거치므로 호출부를 일일이 찾아 연결할 필요가 없다.
public class ExclusivePanelPresence : MonoBehaviour, IExclusiveUiPanel
{
    private void OnEnable() => ExclusiveUiCoordinator.NotifyOpened(this);
    private void OnDisable() => ExclusiveUiCoordinator.NotifyClosed(this);

    // 붙은 오브젝트에 PanelReveal도 있으면(예: QuitAlert) 그 축소 연출로, 없으면 예전처럼
    // SetActive로 닫는다.
    public void RequestClose()
    {
        PanelReveal reveal = GetComponent<PanelReveal>();
        if (reveal != null) reveal.Hide();
        else gameObject.SetActive(false);
    }
}
