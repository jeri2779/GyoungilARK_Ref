 // TempTitle 씬에서 DI 없이 슬롯 파일을 직접 읽어 SlotPreviewInfo로 바꾼다
public class SlotPreviewReader
{
    private readonly SaveSlot saveSlot;
    private SlotPreviewInfo[] cache;


    public SlotPreviewReader()
    {
        SaveCheck saveCheck = new SaveCheck();
        SaveKey saveKey = new SaveKey();
        SaveCipher saveCipher = new SaveCipher(saveKey);
        SaveIO saveIO = new SaveIO(saveCheck, saveCipher);
        saveSlot = new SaveSlot(saveIO, saveCheck);
    }

    // 지정한 슬롯의 미리보기 정보를 캐시에서 꺼내준다.
    public SlotPreviewInfo ReadSaveSlot(int slotId)
    {
        FillCache();
        return cache[slotId - 1];
    }

    // 슬롯이 1개라도 저장되어 있는지 캐시로 확인한다.
    public bool HasAnySave()
    {
        FillCache();
        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i].HasSave) return true;
        }
        return false;
    }

    // 저장/삭제로 내용이 바뀌면 다음 조회 때 다시 읽도록 캐시를 비운다.
    public void ClearCache()
    {
        cache = null;
    }

    // 캐시가 비어 있을 때만 슬롯 10개를 한 번에 읽어 채운다.
    private void FillCache()
    {
        if (cache != null) return;

        cache = new SlotPreviewInfo[SaveSlotConfig.SlotCount];
        for (int i = 0; i < cache.Length; i++)
        {
            cache[i] = ReadFile(i + 1);
        }
    }

    // 슬롯 하나의 저장 파일을 실제로 읽어 미리보기 정보로 바꾼다.
    private SlotPreviewInfo ReadFile(int slotId)
    {
        if (!saveSlot.TryReadLatest(slotId, out SaveFile file))
        {
            return SlotPreviewInfo.Empty;
        }

        return new SlotPreviewInfo(true, file.saveData.dayCount, file.saveData.playTime, file.saveData.saveTime, file.saveData.heroList.Length, file.saveData.savePhase);
    }
}
