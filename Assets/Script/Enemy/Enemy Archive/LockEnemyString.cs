using TMPro;
using UnityEngine;

public class LockEnemyString : MonoBehaviour
{
    public TMP_Text lockText;
    void OnEnable()
    {
        lockText.text = $"{DataTableManager.StringTable.Get("LockEnemyText")}";
    }
}
