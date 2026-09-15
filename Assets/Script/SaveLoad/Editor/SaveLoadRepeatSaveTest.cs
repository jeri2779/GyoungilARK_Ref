using System.IO;
using UnityEditor;
using UnityEngine;

// 세이브 슬롯에 반복 저장했을 때 파일이 깨지지 않고 최신 값을 계속 읽어오는지 확인하는 에디터 테스트 메뉴.
// (Save_0/Save_1 세대 재사용 시 File.Move가 목적지 존재 파일을 못 옮기던 버그의 재발 방지용)
public static class SaveLoadRepeatSaveTest
{
    private const string RootName = "Saves";
    private const string SlotPrefix = "Slot_";
    private const string SlotFormat = "D2";
    private const int TestSlotId = 999;
    private const int RepeatCount = 10;

    // 메뉴에서 반복 저장 테스트를 실행한다
    [MenuItem("Tools/SaveLoad/반복 저장 테스트")]
    public static void RunRepeatSaveTest()
    {
        SaveCheck saveCheck = new SaveCheck();
        SaveKey saveKey = new SaveKey();
        SaveCipher saveCipher = new SaveCipher(saveKey);
        SaveIO saveIO = new SaveIO(saveCheck, saveCipher);
        SaveSlot saveSlot = new SaveSlot(saveIO, saveCheck);

        CleanTestFolder();
        int passCount = CountPasses(saveSlot);
        CleanTestFolder();

        Debug.Log($"[SaveLoad 반복 저장 테스트] {passCount}/{RepeatCount}회 통과");
    }

    // RepeatCount번 저장·확인을 반복하고 통과한 횟수를 센다
    private static int CountPasses(SaveSlot saveSlot)
    {
        int passCount = 0;
        for (int round = 1; round <= RepeatCount; round++)
        {
            if (RunSingleRound(saveSlot, round)) passCount++;
        }
        return passCount;
    }

    // 한 번 저장하고, 곧바로 다시 읽어서 방금 쓴 값이 그대로 나오는지 확인한다
    private static bool RunSingleRound(SaveSlot saveSlot, int round)
    {
        SaveFile written = BuildSaveFile(round);
        if (!saveSlot.TryWrite(TestSlotId, written))
        {
            Debug.LogError($"[SaveLoad 반복 저장 테스트] {round}회차 저장 실패");
            return false;
        }

        if (!saveSlot.TryReadLatest(TestSlotId, out SaveFile read))
        {
            Debug.LogError($"[SaveLoad 반복 저장 테스트] {round}회차 읽기 실패");
            return false;
        }

        if (read.saveData.dayCount != round)
        {
            Debug.LogError($"[SaveLoad 반복 저장 테스트] {round}회차 값 불일치 (읽은 값: {read.saveData.dayCount})");
            return false;
        }

        return true;
    }

    // 회차 번호를 dayCount에 새긴 저장 파일을 만든다
    private static SaveFile BuildSaveFile(int round)
    {
        SaveData data = new SaveData();
        data.dayCount = round;

        SaveFile file = new SaveFile();
        file.fileTag = SaveCheck.FileTag;
        file.saveVersion = SaveCheck.SaveVersion;
        file.saveData = data;
        return file;
    }

    // 테스트로 만든 슬롯 폴더를 지운다
    private static void CleanTestFolder()
    {
        string root = Path.Combine(Application.persistentDataPath, RootName);
        string slotName = SlotPrefix + TestSlotId.ToString(SlotFormat);
        string folder = Path.Combine(root, slotName);

        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
