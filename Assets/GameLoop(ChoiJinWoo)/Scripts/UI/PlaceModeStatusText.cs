using TMPro;
using UnityEngine;

// 재배치/제거 모드 상태를 텍스트로 보여준다. MapView의 상태 변경 이벤트와 언어 변경 이벤트로만 갱신되고 Update 폴링은 하지 않는다.
public class PlaceModeStatusText : MonoBehaviour
{
    [SerializeField] private MapView view;
    [SerializeField] private TextMeshProUGUI text;

    private void OnEnable()
    {
        view.OnStateChanged += Refresh;
        LocalizeTextManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        view.OnStateChanged -= Refresh;
        LocalizeTextManager.OnLanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        var stringTable = DataTableManager.StringTable;
        if (view.IsRemoving)
        {
            text.text = stringTable.Get("Ui_PlaceModeRemove");
        }
        else if (view.IsReplacing)
        {
            text.text = stringTable.Get(view.IsHolding ? "Ui_PlaceModeReplaceHold" : "Ui_PlaceModeReplace");
        }
        else
        {
            text.text = string.Empty;
        }
    }
}
