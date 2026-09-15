// 저장 파일의 식별자·버전·슬롯·순번을 검사한다.
public class SaveCheck
{
    public const string FileTag = "GYARK_SAVE";
    public const int SaveVersion = 3;

    // 저장 파일의 식별자·버전·슬롯을 검사한다 (순번은 보지 않음 — 읽을 때는 기대 순번이 없음).
    public bool IsValidSave(SaveFile saveFile, int slotId)
    {
        if (IsMissing(saveFile))
        {
            return false;
        }

        if (HasWrongTag(saveFile))
        {
            return false;
        }

        if (HasWrongVersion(saveFile))
        {
            return false;
        }

        return HasWrongSlot(saveFile, slotId) == false;
    }

    // 저장 파일의 식별자·버전·슬롯·순번을 검사한다 (쓰기 직후 자기검증 전용 — 방금 쓰려던 순번과 비교).
    public bool IsValidSave(SaveFile saveFile, int slotId, int saveOrder)
    {
        if (!IsValidSave(saveFile, slotId))
        {
            return false;
        }

        return HasWrongOrder(saveFile, saveOrder) == false;
    }

    // 저장 파일이 비어 있는지 검사한다.
    private bool IsMissing(SaveFile saveFile)
    {
        return saveFile == null;
    }

    // 저장 파일 식별자가 다른지 검사한다.
    private bool HasWrongTag(SaveFile saveFile)
    {
        return saveFile.fileTag != FileTag;
    }

    // 저장 구조 버전이 다른지 검사한다.
    private bool HasWrongVersion(SaveFile saveFile)
    {
        return saveFile.saveVersion > SaveVersion;
    }

    // 요청한 슬롯 번호와 다른지 검사한다.
    private bool HasWrongSlot(SaveFile saveFile, int slotId)
    {
        return saveFile.slotId != slotId;
    }

    // 요청한 저장 순번과 다른지 검사한다.
    private bool HasWrongOrder(SaveFile saveFile, int saveOrder)
    {
        return saveFile.saveOrder != saveOrder;
    }
}
