using System;

// 파일 식별자·버전·슬롯ID·저장순서 + SaveData를 감싸는 봉투
[Serializable]
public class SaveFile
{
    public string fileTag;      // 우리 게임의 저장 파일이 맞는지 확인하는 표식
    public int saveVersion;     // 이 저장 구조의 버전
    public int slotId;          // 몇 번 슬롯의 파일인지
    public int saveOrder;       // 저장할 때마다 늘어나는 순번 (최신 파일 판별용)
    public SaveData saveData;   // 실제 게임 저장 내용
}
