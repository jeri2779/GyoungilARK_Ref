using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

// 슬롯의 저장 경로를 관리한다. 매번 Current를 Backup으로 밀어내고 새 저장본을 Current로 승격해 손상에 대비한다.
public class SaveSlot
{
    private const string RootName = "Saves";
    private const string SlotPrefix = "Slot_";
    private const string SlotFormat = "D2";
    private const string CurrentName = "Save_Current";
    private const string BackupName = "Save_Backup";
    private const string SaveExt = ".sav";
    private const string TempExt = ".tmp";
    private const string DayPrefix = "Save_Day_";
    private const string DayFormat = "D2";

    private readonly SaveIO saveIO;
    private readonly SaveCheck saveCheck;

    // 파일 처리 담당자와 저장 파일 검사기를 받는다.
    public SaveSlot(SaveIO saveIO, SaveCheck saveCheck)
    {
        this.saveIO = saveIO;
        this.saveCheck = saveCheck;
    }

    // 새 저장본을 임시 파일에 쓰고 검증한 뒤, 기존 정식 파일을 백업으로 밀어내고 정식 파일로 승격한다.
    public bool TryWrite(int slotId, SaveFile saveFile)
    {
        string folder = SlotPath(slotId);

        try
        {
            Directory.CreateDirectory(folder);

            string currentPath = NamedPath(folder, CurrentName, SaveExt);
            string backupPath = NamedPath(folder, BackupName, SaveExt);
            string tempPath = NamedPath(folder, CurrentName, TempExt);

            int newOrder = NextOrder(currentPath);
            SaveFile output = CreateFile(slotId, newOrder, saveFile);

            if (!saveIO.TryWriteTemp(tempPath, output)) return false;

            PromoteToCurrent(currentPath, backupPath, tempPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // 정식 파일을 먼저 읽고, 없거나 검증에 실패하면 백업 파일을 읽는다.
    public bool TryReadLatest(int slotId, out SaveFile saveFile)
    {
        string folder = SlotPath(slotId);
        string currentPath = NamedPath(folder, CurrentName, SaveExt);
        string backupPath = NamedPath(folder, BackupName, SaveExt);

        if (TryReadValid(currentPath, slotId, out saveFile)) return true;
        return TryReadValid(backupPath, slotId, out saveFile);
    }

    // 지정한 경로의 파일을 읽고 검증까지 통과하는지 본다.
    private bool TryReadValid(string filePath, int slotId, out SaveFile saveFile)
    {
        if (!saveIO.TryRead(filePath, out saveFile)) return false;
        return saveCheck.IsValidSave(saveFile, slotId);
    }

    // 정식 파일을 지정한 일차 전용 파일로 복사한다.
    public bool TryWriteDayArchive(int slotId, int dayCount)
    {
        string folder = SlotPath(slotId);

        try
        {
            saveIO.CopyFileToPath(NamedPath(folder, CurrentName, SaveExt), DayPath(folder, dayCount));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    // 지정한 일차 파일을 읽고 검증까지 통과하는지 본다.
    public bool TryReadDay(int slotId, int dayCount, out SaveFile saveFile)
    {
        return TryReadValid(DayPath(SlotPath(slotId), dayCount), slotId, out saveFile);
    }

    // 일차 번호에 맞는 일차 전용 파일 경로를 만든다.
    private string DayPath(string folder, int dayCount)
    {
        return NamedPath(folder, DayPrefix + dayCount.ToString(DayFormat), SaveExt);
    }

    // 검증된 임시 파일을 정식 파일로 승격한다. 기존 정식 파일이 있으면 먼저 백업으로 밀어낸다.
    private void PromoteToCurrent(string currentPath, string backupPath, string tempPath)
    {
        if (File.Exists(currentPath))
        {
            saveIO.MoveFileToPath(currentPath, backupPath);
        }

        saveIO.MoveFileToPath(tempPath, currentPath);
    }

    // 전달받은 값에 슬롯 정보를 붙인다.
    private SaveFile CreateFile(int slotId, int saveOrder, SaveFile saveFile)
    {
        return new SaveFile
        {
            fileTag = saveFile.fileTag,
            saveVersion = saveFile.saveVersion,
            slotId = slotId,
            saveOrder = saveOrder,
            saveData = saveFile.saveData
        };
    }

    // 정식 파일의 저장 순번보다 1 큰 다음 순번을 구한다 (정식 파일이 없으면 1부터).
    private int NextOrder(string currentPath)
    {
        if (!saveIO.TryRead(currentPath, out SaveFile current)) return 1;
        return current.saveOrder + 1;
    }

    // 슬롯 번호에 맞는 폴더 경로를 만든다.
    private static string SlotPath(int slotId)
    {
        string root = Path.Combine(Application.persistentDataPath, RootName);
        string slotName = SlotPrefix + slotId.ToString(SlotFormat);
        return Path.Combine(root, slotName);
    }

    // 이름과 확장자로 슬롯 폴더 안 파일 경로를 만든다.
    private string NamedPath(string folder, string name, string extension)
    {
        return Path.Combine(folder, name + extension);
    }

    #region Save Tools

    // 개발자 Tools 메뉴에서 호출하여 지정 슬롯의 저장 파일을 모두 삭제한다.
    public static void ClearSlot(int slotId)
    {
        string folder = SlotPath(slotId);
        if (!Directory.Exists(folder))
        {
            return;
        }

        Directory.Delete(folder, true);
    }

    #endregion
}
