#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 도감 아이콘(Resources/EnemyIcons/&lt;이름&gt;.png)을 적 프리팹에서 자동으로 굽는 창(Tools/Enemy/Enemy Icon Baker).
///
/// 지금까지는 Scene 뷰를 스샷 찍어 손으로 잘라 넣었다 — 그래서 스카이박스와 격자 바닥이 배경에 남고
/// 크기도 파일마다 제각각이다(2013×1254, 2291×1254 …). 이 창은 같은 각도·같은 해상도·투명 배경으로
/// 한 번에 굽는다.
///
/// 저장 경로와 파일명은 런타임이 읽는 규칙 그대로다 — EnemyArchiveButton/EnemyInfo가
/// Resources.Load&lt;Sprite&gt;($"EnemyIcons/{data.Name}")로 찾으므로 프리팹 이름과 같아야 한다.
///
/// 배경 투명 처리는 검정 배경·흰 배경으로 두 번 찍어 알파를 역산한다. 렌더 파이프라인(URP)이
/// 알파 채널에 무엇을 쓰든 결과가 같아지기 때문이다 — 파이프라인 설정에 기대지 않는다.
/// </summary>
public class EnemyIconBaker : EditorWindow
{
    private const string PrefabFolder = "Assets/Resources/EnemyUnitPrefabs";
    private const string IconFolder = "Assets/Resources/EnemyIcons";

    // 한 줄 = 적 하나. 프리팹과 현재 아이콘 유무를 같이 들고 있어야 "없는 것만 굽기"를 판단할 수 있다.
    private class Row
    {
        public string Name;
        public GameObject Prefab;
        public bool HasIcon;
        public bool Selected;
    }

    private readonly List<Row> _rows = new();

    private int _size = 512;
    private float _padding = 0.12f;   // 실루엣 주변 여백(반지름 대비 비율)
    // 이 프로젝트의 적 모델은 -Z를 보고 서 있어서 180도가 "얼굴이 보이는" 각도다.
    // 0으로 두면 뒤통수만 찍힌다 — 다른 에셋을 들여오면 이 값을 바꿔야 할 수 있다.
    private float _yaw = 180f;        // 좌우 회전
    private float _pitch = 8f;        // 위에서 내려다보는 각도
    private float _sampleTime;        // 애니메이션 클립에서 뽑을 시각(초). 0이면 클립 첫 프레임.
    // 미리보기 장면에는 씬 조명·스카이박스가 없어 기본값 그대로면 게임 화면보다 한참 어둡게 나온다.
    private float _lightIntensity = 6f;                 // 주광 세기
    private float _ambient = 1.8f;                      // 환경광 — 그늘진 면이 새까맣게 죽는 걸 막는다
    private bool _overwrite;          // 끄면 이미 있는 아이콘은 건너뛴다(손으로 만든 것을 지우지 않게)

    private Vector2 _scroll;
    private Texture2D _preview;
    private string _previewName;

    [MenuItem("Tools/Enemy/Enemy Icon Baker")]
    private static void Open()
    {
        GetWindow<EnemyIconBaker>("Enemy Icon").minSize = new Vector2(420, 420);
    }

    /// <summary>창을 열지 않고 아이콘이 없는 적만 기본 설정으로 굽는다 —
    /// 적을 새로 추가한 뒤 한 번 눌러 주면 도감에 빈 칸이 남지 않는다.</summary>
    [MenuItem("Tools/Enemy/Bake Missing Enemy Icons")]
    private static void BakeMissing()
    {
        var baker = CreateInstance<EnemyIconBaker>();
        try
        {
            baker.Refresh();

            int count = 0;
            foreach (Row row in baker._rows)
            {
                if (row.HasIcon) continue;
                baker.BakeOne(row);
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[EnemyIconBaker] 아이콘이 없던 {count}종을 {IconFolder}에 구웠다.");
        }
        finally
        {
            DestroyImmediate(baker);
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnFocus()
    {
        Refresh(); // 밖에서 프리팹이나 아이콘을 추가했을 수 있다
    }

    private void OnDisable()
    {
        if (_preview != null) DestroyImmediate(_preview);
    }

    // 프리팹 폴더에서 적(EnemyBase가 붙은 것)만 골라 목록을 다시 만든다.
    // EnemyTable을 읽지 않는 이유 — 표는 런타임에 로드되므로 플레이 전에는 비어 있다.
    // 아이콘 키(data.Name)와 프리팹 이름이 같다는 규칙에 기대면 표 없이도 목록이 나온다.
    private void Refresh()
    {
        var keep = new HashSet<string>();
        foreach (Row row in _rows)
        {
            if (row.Selected) keep.Add(row.Name);
        }

        _rows.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<EnemyBase>() == null) continue; // 체력바 등 적이 아닌 프리팹 제외

            _rows.Add(new Row
            {
                Name = prefab.name,
                Prefab = prefab,
                HasIcon = System.IO.File.Exists($"{IconFolder}/{prefab.name}.png"),
                Selected = keep.Contains(prefab.name),
            });
        }

        _rows.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
    }

    private void OnGUI()
    {
        DrawSettings();
        EditorGUILayout.Space();
        DrawBatchButtons();
        EditorGUILayout.Space();
        DrawList();
        DrawPreview();
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("굽기 설정", EditorStyles.boldLabel);
        _size = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("해상도(px)",
            "정사각 PNG 한 변. 도감 상세 패널이 크게 쓰므로 512 이상을 권한다."), _size), 64, 2048);
        _padding = EditorGUILayout.Slider(new GUIContent("여백",
            "실루엣 주변 여백. 0이면 화면에 꽉 찬다."), _padding, 0f, 0.5f);
        _yaw = EditorGUILayout.Slider(new GUIContent("좌우 각도", "0이면 정면"), _yaw, -180f, 180f);
        _pitch = EditorGUILayout.Slider(new GUIContent("위아래 각도", "양수면 위에서 내려다본다"), _pitch, -60f, 60f);
        _sampleTime = EditorGUILayout.Slider(new GUIContent("애니 시각(초)",
            "Animator의 첫 클립에서 이 시각의 자세로 굽는다. 0이면 클립 첫 프레임 — " +
            "클립이 없으면 바인드 자세(T포즈)로 나온다."), _sampleTime, 0f, 3f);
        _lightIntensity = EditorGUILayout.Slider(new GUIContent("조명 세기",
            "미리보기 장면에는 씬 조명이 안 들어오므로 여기서 정한다. 게임 화면만큼 밝게 맞추면 된다."),
            _lightIntensity, 0.2f, 5f);
        _ambient = EditorGUILayout.Slider(new GUIContent("환경광",
            "그늘진 면이 새까맣게 죽는 걸 막는다. 낮추면 대비가 세진다."), _ambient, 0f, 2f);
        _overwrite = EditorGUILayout.ToggleLeft(new GUIContent("이미 있는 아이콘도 덮어쓰기",
            "꺼두면 아이콘이 없는 적만 굽는다. 손으로 만든 기존 이미지를 실수로 날리지 않게 기본은 꺼짐."), _overwrite);
    }

    private void DrawBatchButtons()
    {
        int missing = 0, total = 0;
        foreach (Row row in _rows)
        {
            total++;
            if (!row.HasIcon) missing++;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"아이콘 없는 것만 굽기 ({missing}종)"))
            {
                BakeRows(row => !row.HasIcon);
            }

            using (new EditorGUI.DisabledScope(!_overwrite))
            {
                if (GUILayout.Button($"전체 다시 굽기 ({total}종)"))
                {
                    if (EditorUtility.DisplayDialog("전체 다시 굽기",
                            $"{total}종의 아이콘을 모두 새로 굽는다. 손으로 만든 기존 PNG도 덮어쓴다.\n계속할까?",
                            "굽기", "취소"))
                    {
                        BakeRows(row => true);
                    }
                }
            }
        }

        if (!_overwrite && total > missing)
        {
            EditorGUILayout.HelpBox(
                "전체 다시 굽기는 위의 '덮어쓰기'를 켜야 눌린다. " +
                "지금 아이콘들은 손으로 찍은 스샷이라 배경(스카이박스·격자)이 들어가 있으니, " +
                "한 번은 전체를 다시 구워 통일하는 편이 낫다.", MessageType.Info);
        }
    }

    private void DrawList()
    {
        EditorGUILayout.LabelField("적 목록", EditorStyles.boldLabel);
        using var scope = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MinHeight(140));
        _scroll = scope.scrollPosition;

        foreach (Row row in _rows)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(18));
                EditorGUILayout.LabelField(row.Name, GUILayout.Width(150));
                EditorGUILayout.LabelField(row.HasIcon ? "아이콘 있음" : "없음",
                    row.HasIcon ? EditorStyles.label : EditorStyles.boldLabel, GUILayout.Width(70));

                if (GUILayout.Button("미리보기", GUILayout.Width(70)))
                {
                    ShowPreview(row);
                }

                using (new EditorGUI.DisabledScope(row.HasIcon && !_overwrite))
                {
                    if (GUILayout.Button("굽기", GUILayout.Width(50)))
                    {
                        BakeOne(row);
                        AssetDatabase.Refresh();
                    }
                }
            }
        }
    }

    private void DrawPreview()
    {
        if (_preview == null) return;

        EditorGUILayout.LabelField($"미리보기 — {_previewName}", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(160, 160, GUILayout.ExpandWidth(false));
        EditorGUI.DrawTextureTransparent(rect, _preview, ScaleMode.ScaleToFit);
    }

    private void ShowPreview(Row row)
    {
        if (_preview != null) DestroyImmediate(_preview);
        _preview = Render(row.Prefab);
        _previewName = row.Name;
    }

    // 조건에 맞는 줄을 전부 굽는다. 체크한 줄이 하나라도 있으면 체크한 것만 대상으로 삼는다.
    private void BakeRows(System.Predicate<Row> match)
    {
        bool anySelected = false;
        foreach (Row row in _rows)
        {
            if (row.Selected) { anySelected = true; break; }
        }

        var targets = new List<Row>();
        foreach (Row row in _rows)
        {
            if (anySelected && !row.Selected) continue;
            if (!match(row)) continue;
            if (row.HasIcon && !_overwrite) continue;
            targets.Add(row);
        }

        if (targets.Count == 0)
        {
            ShowNotification(new GUIContent("구울 대상이 없다"));
            return;
        }

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                EditorUtility.DisplayProgressBar("도감 아이콘 굽기",
                    $"{targets[i].Name} ({i + 1}/{targets.Count})", (i + 1f) / targets.Count);
                BakeOne(targets[i]);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        Refresh();
        Debug.Log($"[EnemyIconBaker] {targets.Count}종의 도감 아이콘을 {IconFolder}에 구웠다.");
    }

    // 한 마리를 굽고 PNG로 저장한 뒤 스프라이트로 임포트되게 설정까지 맞춘다.
    private void BakeOne(Row row)
    {
        Texture2D texture = Render(row.Prefab);
        if (texture == null)
        {
            Debug.LogWarning($"[EnemyIconBaker] {row.Name} — 렌더할 메시를 못 찾아 건너뛴다.", row.Prefab);
            return;
        }

        System.IO.Directory.CreateDirectory(IconFolder);
        string path = $"{IconFolder}/{row.Name}.png";
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ApplyImportSettings(path);
        row.HasIcon = true;
    }

    // 기존 아이콘(Bat.png 등)과 같은 임포트 설정으로 맞춘다 — 다르면 같은 UI에서 크기가 달라 보인다.
    private static void ApplyImportSettings(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    // 프리팹 한 개를 투명 배경 정사각 텍스처로 렌더한다. 메시가 없으면 null.
    private Texture2D Render(GameObject prefab)
    {
        if (prefab == null) return null;

        var preview = new PreviewRenderUtility();
        GameObject instance = null;
        try
        {
            instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            SamplePose(instance);

            if (!TryGetBounds(instance, out Bounds bounds)) return null;

            preview.AddSingleGO(instance);
            SetupCamera(preview, bounds);
            SetupLights(preview);

            // 같은 장면을 배경만 바꿔 두 번 찍는다. 알파는 두 결과의 차이에서 역산한다.
            Texture2D onBlack = Capture(preview, Color.black);
            Texture2D onWhite = Capture(preview, Color.white);
            Texture2D result = Unpremultiply(onBlack, onWhite);

            DestroyImmediate(onBlack);
            DestroyImmediate(onWhite);
            return result;
        }
        finally
        {
            if (instance != null) DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    // Animator의 첫 클립을 지정 시각으로 샘플링해 자세를 잡는다.
    // 안 하면 미리보기 장면에는 Animator가 돌지 않아 바인드 자세(T포즈)로 찍힌다.
    private void SamplePose(GameObject instance)
    {
        var animator = instance.GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0) return;

        AnimationClip clip = PickClip(clips);
        clip.SampleAnimation(instance, Mathf.Min(_sampleTime, clip.length));
    }

    // 대기 자세를 우선한다. 컨트롤러가 주는 순서는 정해져 있지 않아서 그냥 [0]을 쓰면
    // 사망·피격 클립이 걸려 무기가 손에서 떨어져 보이는 등 엉뚱한 자세로 찍힌다.
    private static AnimationClip PickClip(AnimationClip[] clips)
    {
        string[] preferred = { "idle", "walk", "run", "move" };

        foreach (string word in preferred)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip != null && clip.name.ToLowerInvariant().Contains(word)) return clip;
            }
        }

        return clips[0];
    }

    // 화면에 담을 범위. 메시 렌더러만 센다 — 파티클·라인 등은 실루엣을 엉뚱하게 넓힌다.
    //
    // 스킨드 메시는 localBounds를 쓰면 안 된다. 그 값은 애니메이션이 튀는 경우까지 덮도록
    // 실제 메시보다 훨씬 크게 잡혀 있어서, 그걸 기준으로 화면을 맞추면 캐릭터가 한쪽에 쏠리고
    // 나머지가 빈 채로 찍힌다. 지금 자세를 그대로 구워(BakeMesh) 실제 범위를 잰다.
    private static bool TryGetBounds(GameObject instance, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.sharedMesh == null) continue;

            var baked = new Mesh();
            // useScale은 끈다 — 아래 localToWorldMatrix가 이미 스케일을 곱하므로 켜면 두 번 곱해진다.
            smr.BakeMesh(baked);
            Encapsulate(ref bounds, ref any, baked.bounds, smr.transform.localToWorldMatrix);
            DestroyImmediate(baked);
        }

        foreach (MeshRenderer mr in instance.GetComponentsInChildren<MeshRenderer>(false))
        {
            var filter = mr.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Encapsulate(ref bounds, ref any, filter.sharedMesh.bounds, mr.transform.localToWorldMatrix);
        }

        return any;
    }

    // 로컬 AABB를 월드로 옮겨 합친다. 회전이 섞이므로 여덟 꼭짓점을 모두 변환해야 정확하다.
    private static void Encapsulate(ref Bounds total, ref bool any, Bounds local, Matrix4x4 toWorld)
    {
        Vector3 c = local.center;
        Vector3 e = local.extents;

        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                c.x + (((i & 1) == 0) ? -e.x : e.x),
                c.y + (((i & 2) == 0) ? -e.y : e.y),
                c.z + (((i & 4) == 0) ? -e.z : e.z));

            Vector3 world = toWorld.MultiplyPoint3x4(corner);
            if (!any) { total = new Bounds(world, Vector3.zero); any = true; }
            else total.Encapsulate(world);
        }
    }

    // 실루엣이 정사각형 안에 꽉 차되 잘리지 않게 직교 카메라를 맞춘다.
    private void SetupCamera(PreviewRenderUtility preview, Bounds bounds)
    {
        Camera camera = preview.camera;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;

        // 어느 각도에서 봐도 안 잘리도록 대각선 길이를 기준으로 잡는다.
        float radius = Mathf.Max(bounds.extents.magnitude, 0.0001f);
        camera.orthographicSize = radius * (1f + _padding);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = radius * 10f + 10f;

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        camera.transform.rotation = rotation;
        camera.transform.position = bounds.center - rotation * Vector3.forward * (radius * 4f);
    }

    // 미리보기 전용 조명. 씬 조명은 이 장면에 안 들어오므로 여기서 직접 세운다.
    // 주광 하나로만 두면 반대쪽 면이 새까맣게 죽어 실루엣만 남는다 — 보조광과 환경광을 같이 올린다.
    private void SetupLights(PreviewRenderUtility preview)
    {
        preview.lights[0].intensity = _lightIntensity;
        preview.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        preview.lights[1].intensity = _lightIntensity * 0.6f;
        preview.lights[1].transform.rotation = Quaternion.Euler(-25f, 120f, 0f);
        preview.ambientColor = new Color(_ambient, _ambient, _ambient * 1.05f, 1f);
    }

    // 지정한 배경색으로 한 장 찍어 픽셀을 읽어 온다.
    private Texture2D Capture(PreviewRenderUtility preview, Color background)
    {
        var rect = new Rect(0f, 0f, _size, _size);
        preview.BeginPreview(rect, GUIStyle.none);
        preview.camera.backgroundColor = background;
        preview.camera.Render();

        var rendered = preview.EndPreview() as RenderTexture;
        var texture = new Texture2D(_size, _size, TextureFormat.RGBA32, false, true);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rendered;
        texture.ReadPixels(rect, 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        return texture;
    }

    // 검정 배경 결과와 흰 배경 결과를 견줘 알파를 복원한다.
    // 배경이 비쳐 보이는 만큼만 두 장이 달라지므로, 그 차이가 곧 "투명한 정도"다.
    // 알파를 안 나눠주면(프리멀티플라이) 반투명 가장자리가 검게 죽는다.
    private Texture2D Unpremultiply(Texture2D onBlack, Texture2D onWhite)
    {
        Color[] black = onBlack.GetPixels();
        Color[] white = onWhite.GetPixels();
        var output = new Color[black.Length];

        for (int i = 0; i < black.Length; i++)
        {
            Color b = black[i];
            Color w = white[i];

            // 채널마다 조금씩 다르게 나오므로(압축·톤매핑) 가장 크게 벌어진 채널을 기준으로 삼는다.
            float diff = Mathf.Max(w.r - b.r, Mathf.Max(w.g - b.g, w.b - b.b));
            float alpha = Mathf.Clamp01(1f - diff);

            if (alpha <= 0.001f)
            {
                output[i] = Color.clear;
                continue;
            }

            output[i] = new Color(b.r / alpha, b.g / alpha, b.b / alpha, alpha);
        }

        var result = new Texture2D(_size, _size, TextureFormat.RGBA32, false, true);
        result.SetPixels(output);
        result.Apply();
        return result;
    }
}
#endif
