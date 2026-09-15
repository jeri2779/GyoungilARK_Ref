using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 버튼을 "눌린 모양(+색)"으로 유지하는 컴포넌트들의 공통 골격 (구 UIHeldBase 통합 - Animator 기반
// 오브젝트와 ButtonPressScale 기반(Animator 없는) 오브젝트를 모두 지원한다. 둘 다 선택적이다).
// 무엇을 근거로 지금 눌린 상태로 볼지는 파생 클래스가 CheckHeld()로만 결정한다.
// 예전에는 Update()에서 매 프레임 CheckHeld()를 다시 확인했다 - 파생 클래스가 자신이 구독하는
// 이벤트(패널이 열고 닫히는 지점 등)에서 Refresh()를 불러주는 방식으로 대체한다.
public abstract class HeldLinkBase : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerExitHandler
{
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color heldTint = new Color(0.55f, 0.7f, 1f);
    [SerializeField] private Color normalTint = Color.white;

    private const string HeldParameter = "Held";
    private Animator buttonAnimator;
    private ButtonPressScale pressScale;
    private bool lastHeld;

    // 이 버튼의 눌린 모양을 담당하는 애니메이터(또는 ButtonPressScale)를 찾아 둔다. 둘 다 선택적이다 -
    // Animator 없이 ButtonPressScale만으로 쓰는 버튼(애니메이터를 안 쓰는 새 버튼들)을 위한 것이다.
    protected virtual void Awake()
    {
        buttonAnimator = GetComponent<Animator>();
        pressScale = GetComponent<ButtonPressScale>();
    }

    // 활성화될 때마다(재활성 포함) 지금 눌림 상태를 다시 확인해 반영해 둔다.
    protected virtual void OnEnable()
    {
        lastHeld = CheckHeld();
        ApplyHeld(lastHeld);
    }

    protected virtual void OnDisable()
    {
    }

    // 파생 클래스가 구독한 이벤트(감시 대상의 상태 변화)에서 호출한다 - 값이 실제로 바뀌었을 때만 반영한다.
    protected void Refresh()
    {
        bool held = CheckHeld();
        if (held == lastHeld) return;

        lastHeld = held;
        ApplyHeld(held);
    }

    // 지금 눌린 상태로 봐야 하는지는 파생 클래스가 판단한다
    protected abstract bool CheckHeld();

    // 눌린 모양과 색을 함께 반영한다
    private void ApplyHeld(bool held)
    {
        if (buttonAnimator != null)
        {
            buttonAnimator.SetBool(HeldParameter, held);
            ClearOtherTriggers();
        }

        pressScale?.SetHeld(held);
        buttonImage.color = SelectTint(held);
    }

    // 대기 중인 다른 트리거가 있으면 Held 전환을 가로채므로, 초기 반영 시점과 아래 이벤트 콜백에서 비워 둔다.
    private void ClearOtherTriggers()
    {
        if (buttonAnimator == null) return;

        buttonAnimator.ResetTrigger("Normal");
        buttonAnimator.ResetTrigger("Highlighted");
        buttonAnimator.ResetTrigger("Pressed");
        buttonAnimator.ResetTrigger("Selected");
        buttonAnimator.ResetTrigger("Disabled");
    }

    // 다른 버튼 클릭으로 포커스를 빼앗기거나(Deselect) 마우스가 벗어나면(PointerExit) Selectable이
    // 몰래 Normal/Highlighted 트리거를 쏘는데, 그게 Held 상태를 Any State 전이로 풀어버린다 - 예전엔
    // 이걸 막으려고 held인 동안 매 프레임 트리거를 지웠지만, 실제로 트리거가 발생하는 바로 그 시점
    // (Unity가 이미 이벤트로 제공하는 시점)에만 지우면 충분하다.
    public void OnSelect(BaseEventData eventData)
    {
        if (lastHeld) ClearOtherTriggers();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (lastHeld) ClearOtherTriggers();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (lastHeld) ClearOtherTriggers();
    }

    // 눌림 여부에 맞는 색을 고른다
    private Color SelectTint(bool held)
    {
        if (held) return heldTint;
        return normalTint;
    }
}
