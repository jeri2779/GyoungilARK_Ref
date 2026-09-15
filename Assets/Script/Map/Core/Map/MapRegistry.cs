using UnityEngine;
using System.Collections.Generic;


//책임: 모듈 등록/해제 + 동번호로 조회.
public class MapRegistry : MonoBehaviour
{
    // 씬에 놓인 모듈들. 인스펙터에서 수동 연결(자동탐색 금지) — Awake에서 일괄 등록한다.
    [SerializeField] private List<ModuleLogic> _sceneModules = new();

    // 동번호(int) → 모듈(ModuleLogic)
    private readonly Dictionary<int, ModuleLogic> _ModuleLogicId = new();

    private static MapRegistry instance;
    public static MapRegistry Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("MapRegistry 인스턴스가 이미 존재합니다.", this);
        }
        else
        {
            instance = this;
        }

        foreach (ModuleLogic logic in _sceneModules)
        {
            if (logic != null)
                RegisterModuleLogic(logic);
        }
    }
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
    // 등록된 모든 모듈 
    public IReadOnlyDictionary<int, ModuleLogic> AllModules => _ModuleLogicId;

    public int ModuleLogicCount => _ModuleLogicId.Count;

    public void RegisterModuleLogic(ModuleLogic logic)
    {
         
        _ModuleLogicId[logic.ModuleId] = logic;
    }
    
    public void UnregisterModuleLogic(ModuleLogic logic)
    {
        _ModuleLogicId.Remove(logic.ModuleId);
    }
     // 동번호로 모듈 찾기. 없으면 false.
    public bool TryGetModuleLogic(int moduleId, out ModuleLogic logic)
    {
        return _ModuleLogicId.TryGetValue(moduleId, out logic);
    }
    public bool UnlockNextModule()
    {
        foreach (ModuleLogic logic in _sceneModules)
        {
            if (logic == null || logic.IsUnlocked) continue;
            logic.Unlock();
            return true;
        }
        return false;
    }
}
