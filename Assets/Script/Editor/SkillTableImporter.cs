#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;


public static class SkillTableImporter
{
    private const string OutputFolder = "Assets/Resources/Skills";

    [MenuItem("Tools/Skill/Import SkillTable")]
    public static void Import()
    {
        var table = new SkillTable();
        table.Load(DataTableIds.Skill);
        var all = table.GetAll();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning("SkillTableImporter: SkillTable 데이터가 없습니다.");
            return;
        }

        EnsureFolder();

        int created = 0, updated = 0, skipped = 0;
        foreach (var kv in all)
        {
            var data = kv.Value;
            var soType = ResolveType(data);
            if (soType == null)
            {
                Debug.LogWarning($"SkillTableImporter: '{data.SkillId}' 처리 불가 (Category='{data.Category}', Type='{data.Type}')");
                skipped++;
                continue;
            }

            string path = $"{OutputFolder}/{data.SkillId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);

            if (existing != null && existing.GetType() != soType)
            {
                AssetDatabase.DeleteAsset(path);
                existing = null;
            }

            bool isNew = existing == null;
            var so = isNew ? (SkillDataSO)ScriptableObject.CreateInstance(soType) : existing;

            ApplyFields(so, data);

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
        Debug.Log($"SkillTableImporter 완료 → 생성 {created}, 갱신 {updated}, 건너뜀 {skipped}");
    }

    private static Type ResolveType(SkillTable.Data d)
    {
        var category = (d.Category ?? "").Trim();
        var type = (d.Type ?? "").Trim();

        if (category == "Attack")
        {
            switch (type)
            {
                case "DamageZone" : return typeof(DamageZoneSO);
                // 화염 오라. Damage/TickInterval 칸은 Ignite_Basic을 못 읽었을 때의 폴백으로만 쓰인다.
                case "FireZone" : return typeof(FireZoneSO);
                case "Explosived" : return typeof(ExplosiveSkillSO);
                case "RushAttack" : return typeof(RushAttackSkillSO);
                // 팬텀 블리츠. Value=분신 마릿수, ValueScale=5일마다 늘어나는 마릿수, TickInterval=타격 간격.
                case "PhantomBlitz" : return typeof(PhantomBlitz);
            }
        } 
        if (category == "Utility")
        {
            switch (type)
            {
                case "Dash": return typeof(DashSkillDataSO);
                case "Summon": return typeof(SummonSkillDataSO);
                case "Heal": return typeof(HealSkillDataSO); // 나중에 스킬 추가
                case "Shield": return typeof(ShieldSkillDataSO);
                case "Split": return typeof(SplitSkillDataSO);
                // 눈의 정령. 수치는 Range(빙결을 뿌릴 반경)와 Cooldown만 쓴다 —
                // 빙결 자체의 세기·지속시간은 DebuffTable의 Frost_Basic이 들고 있다.
                case "SpiritofSnow": return typeof(SpiritofSnow);
                // 내려찍기 스턴. 수치는 Range(스턴을 뿌릴 반경)와 Cooldown만 쓴다 —
                // 스턴 지속시간은 DebuffTable의 Stun_Basic이 들고 있다.
                case "StompStun": return typeof(StompStunSO);

            }
        }
        return null;
    }
    
    private static void ApplyFields(SkillDataSO so, SkillTable.Data d)
    {
        so.skillName = d.NameKey;
        so.cooldown = d.Cooldown;
        so.duration = d.Duration;
        so.range = d.Range;

        switch (so)
        {
            // DamageZoneSO 등 AttackSkillDataSO 파생형도 여기서 처리(damage/tickInterval 공통).
            // 파생 전용 필드가 생기면 이 case '위에' 구체 타입 case를 추가할 것(구체 타입이 먼저 매칭돼야 함).
            case AttackSkillDataSO attack:
                attack.damage = d.Damage ?? 0f;
                attack.tickInterval = d.TickInterval ?? 0f;
                switch (attack)
                {
                    case RushAttackSkillSO rushAttack :
                        rushAttack.value = Mathf.RoundToInt(d.Value ?? 0f);
                        rushAttack.valueScale = Mathf.RoundToInt(d.ValueScale ?? 0f);
                        break;

                    // value = 분신 마릿수. 분신 프리팹·이펙트는 표에 담을 수 없으므로
                    // 에셋(Resources/Skills/PhantomBlitz)의 인스펙터에서 꽂는다 — 임포터가 덮어쓰지 않는다.
                    case PhantomBlitz phantom :
                        phantom.value = Mathf.RoundToInt(d.Value ?? 0f);
                        phantom.valueScale = Mathf.RoundToInt(d.ValueScale ?? 0f);
                        break;
                }
                break;

            case DashSkillDataSO dash:
                dash.value = d.Value ?? 0f;
                dash.tickInterval = d.TickInterval ?? 0f;
                dash.distance = d.Distance ?? 0f;
                break;

            case SummonSkillDataSO summon:
                summon.value = d.Value ?? 0f;          // value = 소환 수
                summon.tickInterval = d.TickInterval ?? 0f;
                break;

            case HealSkillDataSO heal:
                heal.value = d.Value ?? 0f;            // value = 틱당 기본 힐량
                heal.valueScale = d.ValueScale ?? 0f;  // 스테이지당 증가
                heal.tickInterval = d.TickInterval ?? 0f;
                break;
            case ShieldSkillDataSO shield:
                shield.value = d.Value ?? 0f;
                break;
            case SplitSkillDataSO split:
                split.value = d.Value ?? 0f;
                break;
        }
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder)) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateFolder("Assets/Resources", "Skills");
    }
}
#endif
