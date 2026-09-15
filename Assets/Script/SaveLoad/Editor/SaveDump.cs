using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// 선택된 슬롯의 정식 저장 파일을 서명 검사·복호화까지 거쳐 평문 JSON으로 콘솔에 출력하는 에디터 전용 메뉴.
public static class SaveDump
{
    // 메뉴에서 선택된 슬롯의 저장 파일을 평문으로 출력한다
    [MenuItem("Tools/SaveLoad/현재 슬롯 평문 보기")]
    public static void DumpSelectedSlot()
    {
        int slotId = SelectedSaveSlot.SlotId;
        SaveFile saveFile = ReadSelectedSlot(slotId);

        if (saveFile == null)
        {
            Debug.Log($"[SaveDump] Slot_{slotId:D2}에 읽을 수 있는 저장 파일이 없습니다.");
            return;
        }

        string json = ToPrettyJson(saveFile);
        SaveDumpWindow.ShowJson($"Slot_{slotId:D2} 평문", json);
    }

    // 선택된 슬롯의 정식 저장 파일을 서명 검사·복호화까지 거쳐 읽는다
    private static SaveFile ReadSelectedSlot(int slotId)
    {
        SaveCheck saveCheck = new SaveCheck();
        SaveKey saveKey = new SaveKey();
        SaveCipher saveCipher = new SaveCipher(saveKey);
        SaveIO saveIO = new SaveIO(saveCheck, saveCipher);
        SaveSlot saveSlot = new SaveSlot(saveIO, saveCheck);

        if (!saveSlot.TryReadLatest(slotId, out SaveFile saveFile)) return null;
        return saveFile;
    }

    // 저장 파일을 보기 좋은 JSON 문자열로 바꾼다
    private static string ToPrettyJson(SaveFile saveFile)
    {
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings();
        jsonSettings.Converters.Add(new CellConverter());
        return JsonConvert.SerializeObject(saveFile, Formatting.Indented, jsonSettings);
    }
}
