using System;
using UnityEngine;

// TutorialManager가 런타임에 waypoint.target/activationCheck/blockedWhile 오브젝트에 동적으로
// 붙인다(TutorialManager.WireRuntimeWaypoints 참고). SetActive가 어디서 호출되든(버튼 onClick,
// 애니메이션, 다른 스크립트의 코드 등 경로 무관) Unity가 activeInHierarchy가 바뀔 때마다 항상
// OnEnable/OnDisable을 불러주므로, 그 패널들의 소스 코드를 전혀 건드리지 않고도 "이 오브젝트의
// 활성 상태가 바뀌었다"는 신호를 얻을 수 있다 - 조상이 꺼지고 켜질 때도 자식의 OnEnable/OnDisable은
// 정상적으로 불린다.
public class TutorialActivityWatcher : MonoBehaviour
{
    public static event Action Changed;

    private void OnEnable() => Changed?.Invoke();
    private void OnDisable() => Changed?.Invoke();
}
