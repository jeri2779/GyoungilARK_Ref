using UnityEngine;

// 얼음 지대 데이터 보유. Get만 제공 — 실제 적용은 IceZoneEffect가 한다
public class IceZone : MonoBehaviour
{
    [SerializeField] private DebuffSO[] debuffs;
    [SerializeField, Min(0)] private int campfireRange = 1;

    private CampfireData campfireData;

    public DebuffSO[] Debuffs => debuffs;
    public int CampfireRange => campfireRange;
    public CampfireData CampfireData => campfireData;

    // 모닥불 보호 범위를 0 이상의 값으로 변경합니다.
    public void SetRange(int range)
    {
        campfireRange = Mathf.Max(0, range);
    }

    // 완성된 모닥불 보호 데이터를 이 얼음 지대에 연결합니다.
    public void SetCampfire(CampfireData data)
    {
        campfireData = data;
    }
}
