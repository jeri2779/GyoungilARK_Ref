using System;
using System.Collections.Generic;

// TempTitle 씬에서 DI 없이 슬롯의 저장된 일차 목록을 직접 읽어 DayPreviewInfo로 바꾼다.
public class DaySelectReader
{
    private readonly SaveSlot saveSlot;

    public DaySelectReader()
    {
        SaveCheck saveCheck = new SaveCheck();
        SaveKey saveKey = new SaveKey();
        SaveCipher saveCipher = new SaveCipher(saveKey);
        SaveIO saveIO = new SaveIO(saveCheck, saveCipher);
        saveSlot = new SaveSlot(saveIO, saveCheck);
    }

    // 슬롯의 현재 타임라인 목록에서 최신 일차(이어하기로 이미 갈 수 있는 일차)를 뺀 과거 일차만 미리보기로 만든다 (손상·누락 일차도 제외).
    public DayPreviewInfo[] ReadDayList(int slotId)
    {
        if (!saveSlot.TryReadLatest(slotId, out SaveFile latestFile))
        {
            return Array.Empty<DayPreviewInfo>();
        }

        int[] savedDayList = latestFile.saveData.savedDayList;
        List<DayPreviewInfo> result = new List<DayPreviewInfo>(savedDayList.Length);
        for (int i = 0; i < savedDayList.Length; i++)
        {
            int dayCount = savedDayList[i];
            if (dayCount == latestFile.saveData.dayCount) continue;
            if (!saveSlot.TryReadDay(slotId, dayCount, out SaveFile dayFile)) continue;

            result.Add(new DayPreviewInfo(dayCount, dayFile.saveData.playTime, dayFile.saveData.saveTime, dayFile.saveData.heroList.Length));
        }
        return result.ToArray();
    }
}
