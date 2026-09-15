using UnityEngine;
using UnityEngine.InputSystem;

// 스킬 버튼 하나에 대응하는 데이터. 히어로처럼 SO 카탈로그를 쓰지 않고 Hero.auraZonePrefabs와 같이
// 직렬화된 리스트로 인스펙터에서 구성한다.
[System.Serializable]
public class PlayerSkillSlot
{
    public string skillDescKey;
    [Tooltip("PlayerGroundZoneEffect 컴포넌트가 붙은 프리팹 (PlayerBuffZone/PlayerDebuffZone/PlayerHealZone 등)")]
    public GameObject zonePrefab;
    public float manaCost = 10f;
}
