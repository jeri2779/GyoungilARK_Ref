using UnityEngine;
using UnityEngine.InputSystem;

// 맵에 배치된 히어로 위에서 Shift를 누른 채 마우스를 올리면 로스터와 같은 스탯 툴팁을 띄운다.
public class HeroFieldStatTooltipTrigger : MonoBehaviour
{
    [SerializeField] private MapView mapView;
    private const float HoverDelay = 0.2f;
    private Hero hoveredHero;
    private float hoverTimer;
    private bool shown;

    private void Update()
    {
        bool shiftHeld = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        Hero hero = shiftHeld ? mapView.HoverTile?.OccupantHero : null;

        if (hero != hoveredHero)
        {
            hoveredHero = hero;
            hoverTimer = 0f;
            HideIfShown();
        }

        if (hoveredHero == null) return;

        hoverTimer += Time.unscaledDeltaTime;
        if (!shown && hoverTimer >= HoverDelay)
        {
            HeroRosterEntry entry = hoveredHero.GetComponent<HeroRosterLink>()?.Entry;
            if (entry == null) return;
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
