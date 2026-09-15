using System;
using TMPro;
using UnityEngine;

// 자원 생산량 표시 한 줄 - 아이콘은 에디터에서 미리 박아두고(자원별로 안 바뀌니), 코드는 수량 텍스트만 갱신한다.
[Serializable]
public class ResourceAmountRow
{
    public ProductionType type;
    public TextMeshProUGUI amountText;
}
