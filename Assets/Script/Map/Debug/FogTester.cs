using UnityEngine;
using UnityEngine.InputSystem;

// 테스트용: G 키를 누르면 안개 셰이더를 껐다 켠다. 플레이 중 맵 전체를 확인할 때 사용. 정식 빌드에서 제거.
public class FogTester : MonoBehaviour
{
    private static readonly int DensityId = Shader.PropertyToID("_FogDensity");

    private float _cachedDensity;
    private bool _hidden;

    // G 키 입력을 감지해 안개 표시 상태를 뒤집는다.
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }
        if (keyboard.gKey.wasPressedThisFrame)
        {
            Toggle();
        }
    }

    // 안개 진하기를 0과 원래 값 사이로 전환한다.
    private void Toggle()
    {
        if (_hidden)
        {
            Shader.SetGlobalFloat(DensityId, _cachedDensity);
        }
        else
        {
            _cachedDensity = Shader.GetGlobalFloat(DensityId);
            Shader.SetGlobalFloat(DensityId, 0f);
        }
        _hidden = !_hidden;

        // Debug.Log(_hidden ? "[FogTester] 안개 끔" : "[FogTester] 안개 켬");
    }
}
