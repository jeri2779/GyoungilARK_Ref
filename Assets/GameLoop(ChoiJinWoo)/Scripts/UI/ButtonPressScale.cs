using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

// 버튼과 나란히 붙여 호버 확대 · 눌림 축소 피드백을 준다. Animator를 쓰지 않고 직접 스케일을 보간하므로
// Unity Selectable의 Selected 상태가 Animator 트리거를 가로채는 문제 자체가 생기지 않는다.
public class ButtonPressScale : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float animSpeed = 12f;

    private RectTransform cachedRect;
    private bool isHovering;
    private bool isSelected; // 키보드 탐색 등으로 EventSystem이 이 버튼을 선택했을 때(마우스 호버와 같은 강조로 취급)
    private bool isPointerDown;
    private bool isHeld; // 외부(모드/패널 등)가 취소되기 전까지 눌린 모양을 강제하고 싶을 때
    private CancellationTokenSource cts;

    // 부모가 SetActive(true)로 활성화될 때 이 컴포넌트의 Awake보다 부모의 OnEnable이 먼저 불려
    // rect가 아직 null인 채로 SetHeld 등이 호출될 수 있다 - 필요한 시점에 지연 초기화한다.
    private RectTransform rect => cachedRect != null ? cachedRect : (cachedRect = (RectTransform)transform);

    private void OnDisable()
    {
        cts?.Cancel();
        isHovering = false;
        isSelected = false;
        isPointerDown = false;
        isHeld = false;
        rect.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        Animate();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        Animate();
    }

    // 마우스 호버와 동일한 강조로 취급한다 - 마우스/키보드 조작 결과가 항상 같아 보이게 한다.
    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        Animate();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        Animate();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        Animate();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        // 클릭 후에도 EventSystem이 이 버튼을 계속 "선택됨"으로 들고 있으면 Selectable이 스스로
        // Selected 트리거를 쏴서 눌린 모양이 안 풀리는 것처럼 보인다 - 즉시 선택을 비워 막는다.
        EventSystem.current?.SetSelectedGameObject(null);
        Animate();
    }

    // 모드/패널 등 외부 상태가 취소되기 전까지 눌린 모양을 유지시키고 싶을 때 호출.
    public void SetHeld(bool held)
    {
        if (isHeld == held) return;
        isHeld = held;
        Animate();
    }

    private float TargetScale()
    {
        if (isHeld || isPointerDown) return pressScale;
        if (isHovering || isSelected) return hoverScale;
        return 1f;
    }

    private void Animate()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        AnimateTo(TargetScale(), cts.Token).Forget();
    }

    private async UniTaskVoid AnimateTo(float target, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && Mathf.Abs(rect.localScale.x - target) > 0.001f)
            {
                float s = Mathf.Lerp(rect.localScale.x, target, Time.unscaledDeltaTime * animSpeed);
                rect.localScale = new Vector3(s, s, 1f);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (!token.IsCancellationRequested)
            {
                rect.localScale = new Vector3(target, target, 1f);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
