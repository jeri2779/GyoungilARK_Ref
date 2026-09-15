using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 씬의 모든 Button에 ButtonSfx를 자동으로 붙여주는 부트스트래퍼. 프리팹을 건드리지 않고 Selectable의
// 전역 등록 목록(Selectable.allSelectablesArray)을 매 프레임 가볍게 훑어서, 동적으로 생성되는 버튼
// (리스트 아이템 등)까지 포함해 전부 커버한다.
public class ButtonSfxAutoAttacher : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var runner = new GameObject(nameof(ButtonSfxAutoAttacher));
        DontDestroyOnLoad(runner);
        runner.AddComponent<ButtonSfxAutoAttacher>();
    }

    private Selectable[] buffer = new Selectable[16];
    private readonly HashSet<Button> processed = new();

    private void Update()
    {
        int count = Selectable.allSelectableCount;
        if (buffer.Length < count) buffer = new Selectable[count];
        int copied = Selectable.AllSelectablesNoAlloc(buffer);

        for (int i = 0; i < copied; i++)
        {
            if (buffer[i] is Button button && processed.Add(button) && !button.TryGetComponent(out ButtonSfx _))
            {
                button.gameObject.AddComponent<ButtonSfx>();
            }
        }
    }
}
