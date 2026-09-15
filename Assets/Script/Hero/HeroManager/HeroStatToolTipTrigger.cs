using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HeroStatToolTipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverDelay = 0.2f;
    private HeroRosterEntry entry;
    private bool isHovering;
    private float hoverTimer;
    private bool shown;

    public void SetData(HeroRosterEntry entry)
    {
        this.entry = entry;
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovering = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        HideIfShown();
    }

    private void OnDisable()
    {
        isHovering = false;
        HideIfShown();
    }

    private void Update()
    {
        bool shiftHeld = isHovering && Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        if (!shiftHeld)
        {
            hoverTimer = 0f;
            HideIfShown();
            return;
        }

        hoverTimer += Time.unscaledDeltaTime;
        if (!shown && hoverTimer >= HoverDelay)
        {
            HeroStatToolTipUI.Instance.Show(entry, Mouse.current.position.ReadValue());
            shown = true;
        }
    }

    private void HideIfShown()
    {
        if (!shown) return;
        HeroStatToolTipUI.Instance.Hide();
        shown = false;
    }
}
