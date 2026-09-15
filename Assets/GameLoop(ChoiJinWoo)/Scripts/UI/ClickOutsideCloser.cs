using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 열린 바로 그 프레임의 클릭은 무시하고, 그 다음부터 자기(또는 자기 자식, alsoSelf로 지정한 것들)가
// 아닌 곳을 클릭하면 true를 돌려준다.
// 사각형 범위(RectTransformUtility.RectangleContainsScreenPoint)로 판정하면 Canvas 렌더 모드나
// 레이아웃 구조에 따라 자기 자신의 버튼 클릭까지 "바깥"으로 오판할 수 있어서, 실제 UI 레이캐스트
// 결과가 자기 자신 하위 트리에 속하는지로 판정한다.
// alsoSelf: 패널을 여는 버튼처럼 하이러키상 자식은 아니지만 "내 클릭"으로 취급해야 하는 것들.
// 안 넣으면 그 버튼 클릭이 "바깥 클릭"으로 잡혀 Close()가 먼저 불리고, 같은 클릭의 onClick(Toggle 등)이
// 그 뒤에 다시 열어버리는 깜빡임 버그가 생긴다.
public class ClickOutsideCloser
{
    private readonly Transform root;
    private readonly Transform[] alsoSelf;
    private int openedFrame;
    private static readonly List<RaycastResult> raycastResults = new();

    public ClickOutsideCloser(Transform root, params Transform[] alsoSelf)
    {
        this.root = root;
        this.alsoSelf = alsoSelf;
    }

    public void MarkOpened()
    {
        openedFrame = Time.frameCount;
    }

    // ESC 키 하나로 모든 패널이 똑같이 반응하도록 - 여기서도 ClickedOutside()와 동일하게
    // 튜토리얼 강제 진행 중이면 막는다.
    public bool EscapePressed()
    {
        if (TutorialInputGate.BlockEscapeClose) return false;
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    // "닫기 = ESC 또는 바깥 클릭"을 한 번에 묻는 창구. 모든 패널이 이거 하나만 쓰면 통일된다.
    public bool ShouldClose() => EscapePressed() || ClickedOutside();

    public bool ClickedOutside()
    {
        // 튜토리얼이 강제 진행 중일 땐(ESC로 못 닫는 것과 같은 이유로) 바깥 클릭으로도 못 닫게 막는다 -
        // 이 클래스를 쓰는 모든 패널(RegionOverviewPanel, BuildingPanel 등)에 공통으로 적용된다.
        if (TutorialInputGate.BlockEscapeClose) return false;
        if (Time.frameCount == openedFrame) return false;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return false;
        if (EventSystem.current == null) return true;

        var pointerData = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        foreach (var result in raycastResults)
        {
            var hit = result.gameObject.transform;
            if (hit.IsChildOf(root)) return false; // 자기 자신(자식 포함) 클릭이면 바깥이 아니다

            if (alsoSelf != null)
            {
                foreach (var extra in alsoSelf)
                {
                    if (extra != null && hit.IsChildOf(extra)) return false;
                }
            }
        }

        return true;
    }
}
