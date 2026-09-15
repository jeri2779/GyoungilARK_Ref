using System;
using System.IO;

// 지정한 세이브 슬롯의 파일을 안전하게 삭제한다.
public class SlotDelete
{
    // 슬롯 폴더를 삭제하고 성공 여부를 반환한다.
    public bool TryDelete(int slotId)
    {
        try
        {
            SaveSlot.ClearSlot(slotId);
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
}
