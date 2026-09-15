using System;
using UnityEngine;

// 전용 스크립트가 없는 단순 SetActive 토글 패널의 활성 상태 변화를 이벤트로 알리기 위한 범용 컴포넌트.
// ExclusivePanelPresence.cs와 동일한 기법 - HeldLinkBase 파생 클래스(ButtonHeldLink 등)가 watchedPanel의
// activeSelf를 매 프레임 폴링하는 대신, 이 컴포넌트를 자동으로 붙여(GetComponent-or-AddComponent) 구독한다.
public class PanelActivityNotifier : MonoBehaviour
{
    public event Action<bool> ActiveChanged;

    private void OnEnable() => ActiveChanged?.Invoke(true);
    private void OnDisable() => ActiveChanged?.Invoke(false);
}
