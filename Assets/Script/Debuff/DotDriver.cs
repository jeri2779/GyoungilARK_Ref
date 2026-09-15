using UnityEngine;

/// <summary>
/// DotRegistry를 매 프레임 굴리는 구동체. static 클래스에는 Update가 없으므로 최소한의 MonoBehaviour 하나만 둔다.
/// DotRegistry.Apply가 첫 호출 시 자동 생성하므로 씬 배치나 VContainer 등록이 필요 없다(PoisonDriver와 동일).
/// </summary>
public class DotDriver : MonoBehaviour
{
    private static DotDriver instance;

    internal static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("[DotDriver]");
        instance = go.AddComponent<DotDriver>();
        DontDestroyOnLoad(go);
    }

    internal static void ResetInstance() => instance = null;

    private void Update() => DotRegistry.Tick();
}
