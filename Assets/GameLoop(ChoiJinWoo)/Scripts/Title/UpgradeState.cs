using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UpgradeSaveData
{
    public List<string> unlockedIds = new();
    public int points;
}

// 해금 상태를 들고 있는 서비스. 지금은 PlayerPrefs에 임시로 저장하지만,
// 나중에 진짜 세이브 시스템이 생기면 Capture()/Restore()만 그쪽에서 호출하도록 바꾸면 된다.
public class UpgradeState
{
    private const string SaveKey = "UpgradeSaveData"; // 임시 저장 위치

    private UpgradeSaveData data;

    public UpgradeState()
    {
        Load();
    }

    public bool IsUnlocked(string id) => data.unlockedIds.Contains(id);

    public int Points => data.points;

    public bool CanAfford(int cost) => data.points >= cost;

    public void AddPoints(int amount)
    {
        if (amount <= 0) return;
        data.points += amount;
        Save();
    }

    public bool TrySpendPoints(int cost)
    {
        if (!CanAfford(cost)) return false;
        data.points -= cost;
        Save();
        return true;
    }

    // 해당 계열(branch)에서 해금된 단계들의 effectAmount를 전부 더한 값
    public float GetTotalEffect(IEnumerable<BaseUpgradeData> branch)
    {
        float total = 0f;
        foreach (var upgrade in branch)
        {
            if (IsUnlocked(upgrade.id))
                total += upgrade.effectAmount;
        }
        return total;
    }

    public void Unlock(string id)
    {
        if (data.unlockedIds.Contains(id)) return;

        data.unlockedIds.Add(id);
        Save();
    }

    // 해금된 업그레이드들의 cost 합계를 포인트로 환불하고, 해금 상태를 전부 리셋한다(리스펙).
    public void ResetAll(IEnumerable<BaseUpgradeData> allUpgrades)
    {
        int refund = 0;
        foreach (var upgrade in allUpgrades)
        {
            if (IsUnlocked(upgrade.id))
                refund += upgrade.cost;
        }

        data.unlockedIds.Clear();
        data.points += refund;
        Save();
    }

    // 디버그용 — 자원 환불 없이 해금 상태만 초기화
    public void DebugResetWithoutRefund()
    {
        data.unlockedIds.Clear();
        Save();
    }

    // 나중에 세이브 시스템이 이 두 개만 호출하면 됨
    public UpgradeSaveData Capture() => data;

    public void Restore(UpgradeSaveData savedData)
    {
        data = savedData ?? new UpgradeSaveData();
    }

    private void Load()
    {
        var json = PlayerPrefs.GetString(SaveKey, "");
        data = string.IsNullOrEmpty(json) ? new UpgradeSaveData() : JsonUtility.FromJson<UpgradeSaveData>(json);
    }

    private void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
