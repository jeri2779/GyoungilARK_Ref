#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class HeroStatTableImporter
{
    private const string OutputFolder = "Assets/HeroData/StatDataSO";

    [MenuItem("Tools/Hero/Import HeroStatTable")]
    public static void Import()
    {
        var table = new HeroStatTable();
        table.Load(DataTableIds.HeroStat);
        var all = table.GetAll();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning("HeroStatTableImporter: HeroStatTable 데이터가 없습니다.");
            return;
        }

        EnsureFolder();

        int created = 0, updated = 0, skipped = 0;
        foreach (var kv in all)
        {
            var data = kv.Value;
            if (string.IsNullOrEmpty(data.HeroName))
            {
                Debug.LogWarning("HeroStatTableImporter: HeroName이 비어있어 건너뜀");
                skipped++;
                continue;
            }

            string path = $"{OutputFolder}/{data.HeroName}StatData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StatDataSO>(path);
            bool isNew = existing == null;
            var so = isNew ? ScriptableObject.CreateInstance<StatDataSO>() : existing;

            so.maxHp = data.MaxHp;
            so.attackPower = data.AttackPower;
            so.defence = data.Defence;
            so.maxSp = data.MaxSp;
            so.attackSpeed = data.AttackSpeed;
            so.spRecover = data.SpRecover;
            so.blockCount = data.BlockCount;
            so.coolDownPer = data.CoolDownPer;
            so.criticalPer = data.CriticalPer;
            so.criticalDmg = data.CriticalDmg;

            if (isNew)
            {
                AssetDatabase.CreateAsset(so, path);
                created++;
            }
            else
            {
                EditorUtility.SetDirty(so);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"HeroStatTableImporter 완료 → 생성 {created}, 갱신 {updated}, 건너뜀 {skipped}");
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder)) return;
        if (!AssetDatabase.IsValidFolder("Assets/HeroData"))
            AssetDatabase.CreateFolder("Assets", "HeroData");
        AssetDatabase.CreateFolder("Assets/HeroData", "StatDataSO");
    }
}
#endif
