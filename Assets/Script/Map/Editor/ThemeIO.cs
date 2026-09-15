using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 테마 에셋을 모으고, 열어 둔 모듈이 쓰고 있는 프리팹으로 새 테마를 뽑는 에디터 전용 도구.
///
/// 뽑기가 필요한 이유: 모듈은 이미 저작돼 있는데 무슨 프리팹을 쓰는지가 어디에도 적혀 있지 않다.
/// 손으로 슬롯을 채우면 실제로 쓰는 것과 어긋나고, 어긋난 세트로 교체하면
/// 테마가 맞던 타일이 엉뚱한 큐브로 바뀐다.
///
/// 에셋은 스크립트 폴더가 아니라 Assets/Map/Themes에 둔다 — 저작 설정이라 팀이 같은 것을 봐야 한다.
/// </summary>
public static class ThemeIO
{
    private const string RootPath = "Assets/Map";
    private const string FolderPath = "Assets/Map/Themes";

    // 지문을 대조할 프리팹이 있는 곳. 에셋은 저장소가 달라 없을 수도 있어 있는 폴더만 본다.
    private static readonly string[] Packs =
    {
        "Assets/Imported/KUBIKOS - World/URP Support/Prefabs URP",
        "Assets/Imported/KUBIKOS - 3D Cube Village And Farm/URP Support/Prefabs URP"
    };

    /// <summary>프로젝트의 모든 테마. 이름순으로 정렬해 탭 순서가 매번 같게 한다.</summary>
    public static List<TileTheme> LoadAll()
    {
        var themes = new List<TileTheme>();
        foreach (string guid in AssetDatabase.FindAssets("t:TileTheme"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var theme = AssetDatabase.LoadAssetAtPath<TileTheme>(path);
            if (theme != null)
            {
                themes.Add(theme);
            }
        }

        themes.Sort((a, b) => string.Compare(a.Title, b.Title, System.StringComparison.Ordinal));
        return themes;
    }

    /// <summary>이 모듈이 기본으로 쓰는 테마. 가리키는 테마가 없으면 null.</summary>
    public static TileTheme Best(List<TileTheme> themes, Grid module)
    {
        GameObject source = ModuleScan.SourcePrefab(module);
        if (source == null)
        {
            return null;
        }

        foreach (TileTheme theme in themes)
        {
            if (theme.Owns(source))
            {
                return theme;
            }
        }

        return null;
    }

    /// <summary>슬롯이 빈 테마를 하나 만든다.</summary>
    public static TileTheme CreateEmpty(string label)
    {
        var theme = ScriptableObject.CreateInstance<TileTheme>();
        theme.Label = label;
        Save(theme, label);
        return theme;
    }

    // 뽑기(모듈 전체 재추출)까지 안 가고 프리팹 하나만 즉시 등록한다 — 아직 맵에 안 쓴 프리팹도 바로 붓으로 쓰게 한다.
    public static void RegisterPrefab(TileTheme theme, MapBrush brush, GameObject prefab)
    {
        TileTheme.Slot slot = EnsureSlot(theme, brush);
        AppendPrefab(slot, prefab);

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
    }

    private static TileTheme.Slot EnsureSlot(TileTheme theme, MapBrush brush)
    {
        for (int i = 0; i < theme.Slots.Length; i++)
        {
            if (theme.Slots[i].Brush == brush)
            {
                return theme.Slots[i];
            }
        }

        return AppendSlot(theme, brush);
    }

    private static TileTheme.Slot AppendSlot(TileTheme theme, MapBrush brush)
    {
        var slot = new TileTheme.Slot();
        slot.Brush = brush;
        slot.Prefabs = new GameObject[0];

        var slots = new List<TileTheme.Slot>(theme.Slots);
        slots.Add(slot);
        theme.Slots = slots.ToArray();

        return slot;
    }

    private static void AppendPrefab(TileTheme.Slot slot, GameObject prefab)
    {
        var prefabs = new List<GameObject>(slot.Prefabs);
        prefabs.Add(prefab);
        slot.Prefabs = prefabs.ToArray();
    }

    /// <summary>
    /// 이 모듈이 지금 쓰고 있는 프리팹을 붓별로 모아 테마로 만든다.
    /// 프리팹 링크가 지워진 타일은 지문(모양+색)으로 원본을 되찾는다.
    /// </summary>
    public static TileTheme Extract(Grid module, string label)
    {
        var found = new Dictionary<MapBrush, List<GameObject>>();
        var tally = new Tally();
        Index index = BuildIndex();

        GameObject source = ModuleScan.SourcePrefab(module);
        string modulePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;

        List<Tile> tiles = ModuleScan.CollectTiles(module);
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile tile = tiles[i];
            if (Take(found, index, tile.gameObject, BrushOf(tile.State.Terrain), modulePath, tally))
            {
                continue;
            }

            tally.Lost++;
        }

        Transform decor = DecorPlace.Find(module);
        if (decor != null)
        {
            for (int i = 0; i < decor.childCount; i++)
            {
                TakeDeep(found, index, decor.GetChild(i), modulePath, tally);
            }
        }

        var theme = ScriptableObject.CreateInstance<TileTheme>();
        theme.Label = label;
        theme.Slots = Build(found);

        if (source != null)
        {
            theme.Modules = new[] { source };
        }

        Save(theme, label);
        Report(theme, tally);
        return theme;
    }

    // 지문으로 원본 프리팹을 찾는 표. 겹친 지문은 따로 적어 둔다 — 쪼개서 맞추면 엉뚱한 것이 나온다.
    private class Index
    {
        public readonly Dictionary<string, GameObject> Found = new Dictionary<string, GameObject>();
        public readonly HashSet<string> Twice = new HashSet<string>();
    }

    // 뽑기 도중 센 수.
    private class Tally
    {
        public int Linked;
        public int Traced;
        public int Fuzzy;
        public int Lost;
    }

    // 링크가 있으면 링크로, 없으면 지문으로 원본을 찾아 담는다. 담았으면 true.
    private static bool Take(Dictionary<MapBrush, List<GameObject>> found, Index index,
        GameObject made, MapBrush brush, string modulePath, Tally tally)
    {
        GameObject prefab = Origin(made, modulePath);
        if (prefab != null)
        {
            tally.Linked++;
            Collect(found, brush, prefab);
            return true;
        }

        if (TryTrace(index, made, out GameObject same))
        {
            tally.Traced++;
            Collect(found, brush, same);
            return true;
        }

        return false;
    }

    // 장식은 그룹으로 묶여 있기도 하다 — 통째로 못 찾으면 한 겹 안으로 들어가 다시 본다.
    // 통째로 먼저 보는 것이 중요하다: 나무를 잎과 기둥으로 쪼개면 엉뚱한 프리팹에 맞는다.
    private static void TakeDeep(Dictionary<MapBrush, List<GameObject>> found, Index index,
        Transform node, string modulePath, Tally tally)
    {
        if (Take(found, index, node.gameObject, MapBrush.Decor, modulePath, tally))
        {
            return;
        }

        if (Blocked(index, node.gameObject))
        {
            tally.Fuzzy++;
            return; // 후보가 여럿이다 — 쪼개고 들어가면 더 엉뚱해진다
        }

        if (node.childCount == 0)
        {
            tally.Lost++;
            return;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            TakeDeep(found, index, node.GetChild(i), modulePath, tally);
        }
    }

    // 이 오브젝트의 지문이 여러 프리팹에 걸리는가.
    private static bool Blocked(Index index, GameObject made)
    {
        if (!TryFinger(made, out string finger))
        {
            return false;
        }

        return index.Twice.Contains(finger);
    }

    // 모양+색으로 원본 프리팹을 찾는 표. 링크가 지워진 오브젝트를 되찾는 마지막 수단이다.
    private static Index BuildIndex()
    {
        var index = new Index();

        for (int i = 0; i < Packs.Length; i++)
        {
            if (!AssetDatabase.IsValidFolder(Packs[i]))
            {
                continue; // 에셋은 저장소가 달라 없을 수 있다
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Packs[i] });
            for (int n = 0; n < guids.Length; n++)
            {
                AddPrefab(index, AssetDatabase.GUIDToAssetPath(guids[n]));
            }
        }

        return index;
    }

    // 같은 지문을 두 프리팹이 나눠 쓰면 어느 쪽인지 알 수 없다 — 표에서 아예 뺀다.
    // 찍고 나서 엉뚱한 것이 나오는 것보다 안 나오는 편이 낫다.
    private static void AddPrefab(Index index, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null || !TryFinger(prefab, out string finger) || index.Twice.Contains(finger))
        {
            return;
        }

        if (index.Found.ContainsKey(finger))
        {
            index.Found.Remove(finger);
            index.Twice.Add(finger);
            return;
        }

        index.Found[finger] = prefab;
    }

    // 지워진 링크 대신 지문으로 원본을 찾는다.
    private static bool TryTrace(Index index, GameObject made, out GameObject prefab)
    {
        prefab = null;
        if (!TryFinger(made, out string finger))
        {
            return false;
        }

        return index.Found.TryGetValue(finger, out prefab);
    }

    // 이 오브젝트의 지문 — 조각(메시+머티리얼)을 전부 모아 정렬해 붙인 것.
    // 조각 하나만 보면 나무들이 기둥을 나눠 써서 서로 구분되지 않는다.
    private static bool TryFinger(GameObject made, out string finger)
    {
        finger = string.Empty;
        var parts = new List<string>();

        MeshFilter[] meshes = made.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshes.Length; i++)
        {
            AddPart(parts, meshes[i]);
        }

        if (parts.Count == 0)
        {
            return false;
        }

        parts.Sort(); // 계층 순서가 달라도 같은 지문이 나오게 한다
        finger = string.Join(",", parts.ToArray());
        return true;
    }

    private static void AddPart(List<string> parts, MeshFilter mesh)
    {
        if (mesh.sharedMesh == null)
        {
            return;
        }

        Renderer paint = mesh.GetComponent<Renderer>();
        if (paint == null || paint.sharedMaterial == null)
        {
            return;
        }

        parts.Add(mesh.sharedMesh.name + "|" + paint.sharedMaterial.name);
    }

    // 모은 목록을 붓 순서대로 슬롯 배열로 만든다. 비어 있는 붓은 슬롯을 만들지 않는다.
    private static TileTheme.Slot[] Build(Dictionary<MapBrush, List<GameObject>> found)
    {
        var slots = new List<TileTheme.Slot>();
        foreach (MapBrush brush in TileTheme.Painters())
        {
            if (!found.TryGetValue(brush, out List<GameObject> prefabs))
            {
                continue;
            }

            slots.Add(new TileTheme.Slot { Brush = brush, Prefabs = prefabs.ToArray() });
        }

        return slots.ToArray();
    }

    private static void Collect(Dictionary<MapBrush, List<GameObject>> found, MapBrush brush, GameObject prefab)
    {
        if (!found.TryGetValue(brush, out List<GameObject> prefabs))
        {
            prefabs = new List<GameObject>();
            found[brush] = prefabs;
        }

        if (!prefabs.Contains(prefab))
        {
            prefabs.Add(prefab);
        }
    }

    /// <summary>
    /// 이 오브젝트를 만든 원본 프리팹 에셋. 프리팹에서 온 것이 아니면 null.
    ///
    /// 한 단계가 아니라 맨 처음 원본까지 거슬러 올라간다 — 모듈 프리팹 안에 큐브 프리팹이 겹쳐 있어서,
    /// 한 단계만 보면 "MapModule_A 안의 Prod_1_1"이 나온다. 그것은 따로 찍을 수 있는 프리팹이 아니다.
    /// 찍으려면 진짜 원본(Cube_PlowedGround 같은 것)의 루트여야 한다.
    ///
    /// 그래서 모듈 프리팹 자신으로 되돌아오는 오브젝트는 원본이 없는 것으로 본다 —
    /// 그 타일은 모듈 안에 직접 놓인 것이고, 갈아끼울 원본 프리팹을 갖고 있지 않다.
    /// </summary>
    private static GameObject Origin(GameObject made, string modulePath)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(made);
        if (source == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path) || path == modulePath)
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static MapBrush BrushOf(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.High: return MapBrush.High;
            case TerrainType.Core: return MapBrush.Core;
            case TerrainType.Special: return MapBrush.Special;
            case TerrainType.Empty: return MapBrush.Empty;
            default: return MapBrush.Ground;
        }
    }

    private static void Save(TileTheme theme, string label)
    {
        EnsureFolder();
        string name = string.IsNullOrEmpty(label) ? "TileTheme" : label;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{FolderPath}/{name}.asset");

        AssetDatabase.CreateAsset(theme, path);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(FolderPath))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(RootPath))
        {
            AssetDatabase.CreateFolder("Assets", "Map");
        }

        AssetDatabase.CreateFolder(RootPath, "Themes");
    }

    // 뽑은 결과를 알린다. 못 찾은 것이 많으면 그 모듈은 되짚을 원본이 거의 없다는 뜻이다.
    private static void Report(TileTheme theme, Tally tally)
    {
        int kinds = 0;
        foreach (TileTheme.Slot slot in theme.Slots)
        {
            kinds += slot.Prefabs.Length;
        }

        if (kinds == 0)
        {
            Debug.LogWarning("[Map Maker] 뽑을 프리팹이 없습니다 — 링크도 없고 지문으로도 못 찾았습니다" +
                $" (못 찾음 {tally.Lost}개). 다른 모듈에서 뽑은 테마를 쓰거나 교체로 프리팹을 깔아 주세요.", theme);
            return;
        }

        Debug.Log($"[Map Maker] 테마를 뽑았습니다 — 프리팹 {kinds}종 (링크로 {tally.Linked}개, " +
            $"지문으로 {tally.Traced}개, 애매해서 건너뜀 {tally.Fuzzy}개, 못 찾음 {tally.Lost}개)" +
            $" · {AssetDatabase.GetAssetPath(theme)}", theme);
    }
}
