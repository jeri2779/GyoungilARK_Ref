using UnityEditor;
using UnityEngine;

// 장판형 스킬용 "회전하는 먼지" 파티클 프리팹을 생성하는 에디터 유틸.
// 상단 메뉴 Tools > Effects > Create GroundDust Prefab 실행 → EnemyEffectPrefab 폴더에 프리팹(+머티리얼) 생성.
// 참고용 베이스라 값은 인스펙터에서 자유롭게 조정하면 됨. 핵심은 Velocity over Lifetime > Orbital(궤도 회전).
public static class GroundDustPrefabCreator
{
    private const string FolderPath = "Assets/Resources/EnemyEffectPrefab";
    private const string PrefabPath = FolderPath + "/GroundDust.prefab";
    private const string MatPath = FolderPath + "/GroundDust.mat";

    [MenuItem("Tools/Effects/Create GroundDust Prefab")]
    public static void Create()
    {
        // 1) 파티클 오브젝트 생성 — 회전은 기본(0,0,0). 눕히지 않아야 Y축이 위, Orbital Y가 바닥 평면 회전이 됨.
        var go = new GameObject("GroundDust");
        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        // 2) Main — 제자리에서 떠 있는 작은 먼지.
        var main = ps.main;
        main.duration = 3f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = 0f;                                   // 바깥으로 뿜지 않음 → Orbital로만 돌린다
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startColor = new Color(0.85f, 0.78f, 0.62f, 1f);  // 흙먼지색(원하는 색으로 변경)
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 장판이 이동하면 World, 고정이면 Local로
        main.maxParticles = 400;

        // 3) Emission — 촘촘하게.
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 60f;

        // 4) Shape — 원형으로 뿌림. Radius를 장판 반지름에 맞추면 됨.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 3f;
        shape.radiusThickness = 1f;   // 0으로 하면 가장자리(링)에서만 방출
        shape.arc = 360f;

        // 5) Velocity over Lifetime — ★회전의 핵심★. Orbital Y = 중심축 기준 공전.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(1.5f);   // 회전 속도(부호로 방향). 클수록 빠름
        vel.radial = new ParticleSystem.MinMaxCurve(-0.2f);    // 살짝 안쪽으로 → 소용돌이 느낌
        vel.y = new ParticleSystem.MinMaxCurve(0.2f);          // 먼지가 살짝 떠오르게

        // 6) Color over Lifetime — 나타났다 사라지게(뚝 끊김 방지).
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // 7) Size over Lifetime — 커졌다 작아지게(선택).
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.2f)));

        // 8) Rotation over Lifetime — 개별 먼지 알갱이 자체 회전(선택).
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

        // 9) Renderer — 빌보드 + URP 파티클 머티리얼.
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;

        if (!AssetDatabase.IsValidFolder(FolderPath))
            System.IO.Directory.CreateDirectory(FolderPath);

        var mat = CreateParticleMaterial();
        if (mat != null) renderer.sharedMaterial = mat;

        // 10) 프리팹 저장 후 임시 오브젝트 제거.
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[GroundDust] 프리팹 생성 완료: {PrefabPath}");
    }

    // URP 파티클 언릿 머티리얼 생성(가산 블렌딩, 소프트 파티클 텍스처는 기본). 없으면 null 반환.
    private static Material CreateParticleMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[GroundDust] URP Particles/Unlit 셰이더를 못 찾음 — 렌더러 머티리얼을 직접 지정하세요.");
            return null;
        }

        var mat = new Material(shader) { name = "GroundDust" };
        // 가산 블렌딩(발광 느낌)으로 세팅 — 장판 셰이더와 톤 맞추려면 조정.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);       // Additive
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);

        AssetDatabase.CreateAsset(mat, MatPath);
        return mat;
    }
}
