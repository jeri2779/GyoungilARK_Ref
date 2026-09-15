#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class HeroTableImporter
{
    private const string OutputFolder = "Assets/HeroData/HeroData";

    [MenuItem("Tools/Hero/Import HeroTable")]
    public static void Import()
    {
        var table = new HeroTable();
        table.Load(DataTableIds.Hero);
        var all = table.GetAll();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning("HeroTableImporter: HeroTable 데이터가 없습니다.");
            return;
        }

        EnsureFolder();

        int created = 0, updated = 0, skipped = 0;
        foreach (var kv in all)
        {
            var data = kv.Value;
            if (string.IsNullOrEmpty(data.HeroName))
            {
                Debug.LogWarning($"HeroTableImporter: UnitId '{data.UnitId}' HeroName이 비어있어 건너뜀");
                skipped++;
                continue;
            }

            string path = $"{OutputFolder}/{data.HeroName}Data.asset";
            var existing = AssetDatabase.LoadAssetAtPath<HeroData>(path);
            bool isNew = existing == null;
            var so = isNew ? ScriptableObject.CreateInstance<HeroData>() : existing;

            so.UnitId = data.UnitId;
            so.Tier = data.Tier;
            so.HeroName = data.HeroName;
            so.HeroType = data.HeroType;
            so.HeroNameKey = $"Hero_{data.HeroName}_Name";
            so.HeroDescriptionKey = $"Hero_{data.HeroName}_Desc";

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
        Debug.Log($"HeroTableImporter 완료 → 생성 {created}, 갱신 {updated}, 건너뜀 {skipped}");
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder)) return;
        if (!AssetDatabase.IsValidFolder("Assets/HeroData"))
            AssetDatabase.CreateFolder("Assets", "HeroData");
        AssetDatabase.CreateFolder("Assets/HeroData", "HeroData");
    }
}
#endif
