using UnityEngine;
using UnityEngine.InputSystem;

// 테스트용: U 키로 다음 모듈 해금 + 낮 이벤트 재현, N 키로 밤 이벤트 강제 재현. 확장·지대 기믹 확인용. 정식 빌드에서 제거.
public class ExpandTester : MonoBehaviour
{
    [SerializeField] private MapRegistry registry;
    [SerializeField] private MapGame mapGame;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }
        if (keyboard.uKey.wasPressedThisFrame)
        {
            EnterDay();
        }
        if (keyboard.nKey.wasPressedThisFrame)
        {
            EnterNight();
        }
    }

    // 다음 모듈을 해금하고, 실제 흐름과 같은 순서(해금→낮 벨)로 낮 이벤트를 재현한다.
    private void EnterDay()
    {
        bool opened = registry.UnlockNextModule();
        // Debug.Log(opened ? "[ExpandTester] 다음 모듈 해금" : "[ExpandTester] 더 해금할 모듈 없음");
    
    }

    // 밤 이벤트를 강제로 재현한다(지대 디버프 확인용).
    private void EnterNight()
    {
        
        // Debug.Log("[ExpandTester] 밤 강제 진입");
    }
}
