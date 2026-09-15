using System;
using UnityEngine;

// 기반시설 UI에서 지을 수 있는 건물 하나(생산 시설 또는 집). 맵 배치용 Placeable과 달리
// 좌표/프리팹이 없다 - facilityValue/houseConfig 둘 중 하나만 채워서 kind로 구분한다.
[Serializable]
public class BuildableFacility
{
    public string label = "건물";
    public Sprite icon;
    public OccupantKind kind = OccupantKind.Resource;
    [Tooltip("kind == Resource일 때 채움.")]
    public ProductionValue facilityValue;
    [Tooltip("kind == Building일 때 채움.")]
    public HouseConfig houseConfig;

    // 슬롯에 표시할 이름은 SO 자체의 이름을 그대로 쓴다(label을 따로 또 입력하지 않도록).
    public string DisplayName => kind == OccupantKind.Resource
        ? facilityValue != null ? facilityValue.FacilityDisplayName : label
        : houseConfig != null ? houseConfig.HouseDisplayName : label;
}
