using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

// 세이브 파일을 임시 경로에 쓰고 검증하거나, 검증된 파일을 목적지로 옮기거나, 읽는다.
public class SaveIO
{
    private readonly JsonSerializerSettings jsonSettings;
    private readonly SaveCheck saveCheck;
    private readonly SaveCipher saveCipher;

    // JSON 변환 규칙과 저장 파일 검사기, 암호화·서명 담당을 받는다.
    public SaveIO(SaveCheck saveCheck, SaveCipher saveCipher)
    {
        this.saveCheck = saveCheck;
        this.saveCipher = saveCipher;
        jsonSettings = new JsonSerializerSettings();
        jsonSettings.Converters.Add(new CellConverter());
    }

    // 파일을 임시 경로에 쓰고, 다시 읽어 검증까지 통과하는지 확인한다 (아직 정식 파일로 확정하지 않음).
    public bool TryWriteTemp(string tempPath, SaveFile saveFile)
    {
        try
        {
            string json = JsonConvert.SerializeObject(saveFile, Formatting.Indented, jsonSettings);
            byte[] fileBytes = saveCipher.ToFileBytes(json);
            WriteTemp(tempPath, fileBytes);

            SaveFile readBack = LoadFile(tempPath);
            return saveCheck.IsValidSave(readBack, saveFile.slotId, saveFile.saveOrder);
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

    // 지정한 경로의 저장 파일 하나를 읽는다.
    public bool TryRead(string filePath, out SaveFile saveFile)
    {
        try
        {
            saveFile = LoadFile(filePath);
            return true;
        }
        catch (IOException)
        {
            saveFile = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            saveFile = null;
            return false;
        }
        catch (JsonException)
        {
            saveFile = null;
            return false;
        }
    }

    // 파일을 목적지 경로로 옮기며, 이미 있으면 덮어쓴다.
    public void MoveFileToPath(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(sourcePath, destinationPath, null);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }

    // 파일을 목적지 경로로 복사하며, 이미 있으면 덮어쓴다.
    public void CopyFileToPath(string sourcePath, string destinationPath)
    {
        File.Copy(sourcePath, destinationPath, true);
    }

    // 파일 바이트를 임시 파일에 쓰고 디스크까지 반영한다.
    private void WriteTemp(string filePath, byte[] fileBytes)
    {
        using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            stream.Write(fileBytes, 0, fileBytes.Length);
            stream.Flush(true);
        }
    }

    // 저장 파일을 JSON에서 복원한다. 서명이 안 맞으면 예전 평문 파일로 보고 그대로 파싱한다.
    private SaveFile LoadFile(string filePath)
    {
        byte[] fileBytes = File.ReadAllBytes(filePath);
        string json = ToJsonWithLegacyFallback(fileBytes);
        return JsonConvert.DeserializeObject<SaveFile>(json, jsonSettings);
    }

    // 파일 바이트의 서명을 검사하고 복호화한다. 서명이 안 맞으면 암호화 이전의 평문 파일로 보고 UTF-8 그대로 읽는다.
    private string ToJsonWithLegacyFallback(byte[] fileBytes)
    {
        try
        {
            return saveCipher.ToJson(fileBytes);
        }
        catch (CryptographicException)
        {
            return Encoding.UTF8.GetString(fileBytes);
        }
    }
}
