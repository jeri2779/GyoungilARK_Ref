using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 지역 오버뷰의 다이아몬드 노드 하나. 잠김/해금 상태를 아이콘 스프라이트 교체로 표현한다
// (FacilitySlotView의 emptyIcon/builtIcon과 같은 방식 - 별도 오버레이 오브젝트 없음).
public class RegionNodeView : MonoBehaviour
{
    [SerializeField] private int moduleId;
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite lockedIcon;
    [SerializeField] private Sprite unlockedIcon;

    public int ModuleId => moduleId;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        GetComponentInParent<RegionOverviewPanel>().OnNodeClicked(this);
    }

    public void SetLocked(bool locked)
    {
        if (icon != null) icon.sprite = locked ? lockedIcon : unlockedIcon;
        if (button != null) button.interactable = !locked;
    }
}
