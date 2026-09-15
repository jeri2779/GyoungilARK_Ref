using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 개발 중 세이브 기능을 켜고 끄거나 현재 슬롯을 초기화하는 에디터 전용 메뉴다.
public static class SaveTools
{
    private const string EnablePath = "Tools/SaveLoad/세이브 ON";
    private const string DisablePath = "Tools/SaveLoad/세이브 OFF";
    private const string ClearPath = "Tools/SaveLoad/슬롯 01 초기화";
    private const string TitleSceneName = "TempTitle";

    // 개발자 세이브 기능을 활성화한다.
    [MenuItem(EnablePath)]
    public static void EnableSave()
    {
        PlayerPrefs.SetInt(SaveManager.ToolKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[SaveLoad Tools] 세이브 기능을 켰습니다.");
    }

    // 개발자 세이브 기능을 비활성화한다.
    [MenuItem(DisablePath)]
    public static void DisableSave()
    {
        PlayerPrefs.SetInt(SaveManager.ToolKey, 0);
        PlayerPrefs.Save();
        Debug.Log("[SaveLoad Tools] 세이브 기능을 껐습니다.");
    }

    // 확인을 받은 뒤 현재 개발 슬롯의 저장 파일만 초기화한다.
    [MenuItem(ClearPath)]
    public static void ClearSave()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "세이브 초기화",
            "Slot_01의 저장 파일을 모두 삭제합니다.",
            "초기화",
            "취소");

        if (!confirmed)
        {
            return;
        }

        SaveSlot.ClearSlot(1);
        Debug.Log("[SaveLoad Tools] Slot_01을 초기화했습니다.");
    }

    // 타이틀 없이 다른 씬을 바로 플레이해도 이어하기로 취급해 로드가 되게 한다 (에디터 전용).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoContinueOutsideTitle()
    {
        if (SceneManager.GetActiveScene().name == TitleSceneName) return;

        SelectedSaveSlot.SetLoad(1);
    }
}
