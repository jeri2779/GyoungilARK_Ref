using UnityEngine;
using UnityEngine.EventSystems;

// Button의 Sprite Swap Transition은 강조용 이미지를 하나만 지정할 수 있다 - 그것만으로 부족할 때
// 추가로 켜고 끌 이미지를 여기에 연결한다. 버튼과 나란히 붙인다.
// ButtonPressScale/HoverSelects와 같은 컨벤션으로, 키보드 탐색으로 인한 Selected 상태도 마우스
// 호버와 동일하게 취급한다(마우스/키보드 조작 결과가 항상 같아 보이게 한다).
public class ButtonHoverImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private GameObject hoverImage;

    private bool isHovering;
    private bool isSelected;

    private void OnEnable()
    {
        isHovering = false;
        isSelected = false;
        hoverImage.SetActive(false);
    }

    private void OnDisable()
    {
        isHovering = false;
        isSelected = false;
        hoverImage.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        Refresh();
    }

    private void Refresh()
    {
        hoverImage.SetActive(isHovering || isSelected);
    }
}
