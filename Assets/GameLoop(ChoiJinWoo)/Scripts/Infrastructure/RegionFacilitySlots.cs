using System;
using System.Collections.Generic;
using UnityEngine;

// 지역 하나가 보유한 건설 슬롯들. 모듈 프리팹(ModuleLogic)에는 손대지 않고, moduleId 번호로만
// 엮는다. GameObject가 필요 없는 순수 데이터라 Placeable/BuildableFacility처럼 일반 클래스로 두고
// RegionOverviewPanel의 인스펙터 리스트에 인라인으로 값을 채운다.
// 슬롯 좌표는 없다 - 기반시설 UI에서만 관리되는 추상 슬롯(§2단계: 지역 클릭 -> 슬롯 그리드).
[Serializable]
public class RegionFacilitySlots
{
    [Tooltip("이 지역이 대응하는 ModuleLogic의 moduleId. 모듈 프리팹은 안 건드리고 번호만 맞춰서 엮는다.")]
    [SerializeField] private int moduleId;

    [Tooltip("이 지역의 건설 슬롯 개수. 밸런스 테스트하며 자유롭게 조정.")]
    [SerializeField, Min(1)] private int slotCount = 4;

    private List<RegionFacilitySlot> slots;

    public int ModuleId => moduleId;
    // 지역 이름(패널 헤더에 표시) - 모든 지역이 "{moduleId} 지역" 형태라 별도 필드 없이 StringTable
    // 포맷 키에 moduleId만 끼워 넣는다. 언어를 바꿔도 즉시 새 표기로 나온다.
    public string RegionName => string.Format(DataTableManager.StringTable.Get("Ui_RegionName"), moduleId);
    public IReadOnlyList<RegionFacilitySlot> Slots => EnsureInitialized();

    public event Action OnSlotsChanged;

    // 일반 클래스라 Awake가 없다 - 슬롯 리스트를 처음 쓰는 시점에 한 번만 만든다.
    private List<RegionFacilitySlot> EnsureInitialized()
    {
        if (slots == null)
        {
            slots = new List<RegionFacilitySlot>(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                slots.Add(new RegionFacilitySlot());
            }
        }
        return slots;
    }

    public bool TryAssign(int index, object occupant, Sprite icon)
    {
        var list = EnsureInitialized();
        if (index < 0 || index >= list.Count || !list[index].IsEmpty) return false;

        list[index].Assign(occupant, icon);
        OnSlotsChanged?.Invoke();
        return true;
    }

    public bool TryClear(int index)
    {
        var list = EnsureInitialized();
        if (index < 0 || index >= list.Count || list[index].IsEmpty) return false;

        list[index].Clear();
        OnSlotsChanged?.Invoke();
        return true;
    }

    // 인구 로우가 읽을 이 지역의 총 배치 인력(생산 시설에만 있는 개념 - House는 셈하지 않는다).
    public int TotalWorkers()
    {
        int total = 0;
        foreach (var slot in EnsureInitialized())
        {
            if (slot.Occupant is ProductionFacility facility) total += facility.WorkerAmount;
        }
        return total;
    }
}
