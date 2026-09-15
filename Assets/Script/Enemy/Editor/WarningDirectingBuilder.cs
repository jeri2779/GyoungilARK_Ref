using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 등장 "WARNING!" 연출 빌더.
/// 도트 Image들을 글자 모양으로 배치하고, 화면 바깥에서 날아와 조립되는 AnimationClip을 굽는다.
/// Window > Boss Directing > Warning Builder
/// </summary>
public class WarningDirectingBuilder : EditorWindow
{
    // 맥동(스케일)과 색 틴트를 도트에만 걸기 위한 중간 컨테이너 이름.
    // 루트에 직접 걸면 루트의 다른 자식(화살표 바 등 손으로 붙인 연출)까지 같이 커졌다 작아지고
    // 색이 물든다 — Transform 스케일은 자식이 개별적으로 빠져나갈 방법이 없으므로 계층을 나눈다.
    // CanvasGroup 알파(페이드)와 배경 Image는 루트에 그대로 둬서 다른 자식도 같이 사라진다.
    const string DotsContainerName = "Dots";

    // ── 대상 ──────────────────────────────────────────────
    RectTransform root;
    AnimationClip clip;

    // ── 글자 ──────────────────────────────────────────────
    string text = "WARNING!";
    float cellSize = 26f;   // 도트 간 간격
    float dotSize = 22f;    // 도트 한 변 크기
    Vector2 origin = new Vector2(0f, 60f);
    Sprite dotSprite;       // 비워두면 흰 사각형

    // ── 회전 ──────────────────────────────────────────────
    float textTilt = -4f;      // 글자 전체 기울기(도). 도트 자체도 같이 돌아간다
    float spinAmount = 160f;   // 날아오는 동안 도트가 도는 양(도)
    bool spinRandomDir = true; // 도는 방향을 도트마다 랜덤으로

    // ── 색 ────────────────────────────────────────────────
    Color baseColor = new Color(0.85f, 0.12f, 0.12f, 1f);
    Color subColor = new Color(1f, 0.82f, 0.25f, 1f);

    // ── 날아오기 ──────────────────────────────────────────
    float flyDistance = 1500f;   // 바깥에서 출발하는 거리 (1920x1080 대각 반지름 ≈ 1101)
    float angleJitter = 18f;     // 방사 방향 흔들기(도)
    float staggerWindow = 0.35f; // 도트별 출발 시차가 퍼지는 총 구간
    float flyDuration = 0.45f;   // 한 도트가 날아오는 시간
    float overshoot = 26f;       // 목표를 지나쳤다 되돌아오는 양(px)

    // ── 맥동(커졌다 작아졌다 + 색 전환) ───────────────────
    int pulseCount = 3;          // 반복 횟수. 0이면 맥동 없음
    float pulseDuration = 0.42f; // 한 번의 커졌다 작아지는 시간
    float pulseScale = 1.07f;    // 최대로 커졌을 때 배율
    float pulsePeakRatio = 0.4f; // 한 사이클 중 커지는 데 쓰는 비율

    // ── 마무리 ────────────────────────────────────────────
    float holdTime = 0.35f;      // 맥동 끝난 뒤 유지
    float fadeOutTime = 0.35f;
    float backdropAlpha = 0.5f;  // Directing 자체 Image(배경) 최종 알파

    Vector2 scroll;

    [MenuItem("Window/Boss Directing/Warning Builder")]
    static void Open()
    {
        GetWindow<WarningDirectingBuilder>("Warning Builder");
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
        root = (RectTransform)EditorGUILayout.ObjectField("Directing 루트", root, typeof(RectTransform), true);
        clip = (AnimationClip)EditorGUILayout.ObjectField("클립", clip, typeof(AnimationClip), false);
        if (clip == null)
            EditorGUILayout.HelpBox("Assets/Animation/BossEnterDirecting.anim 을 넣으세요.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("글자", EditorStyles.boldLabel);
        text = EditorGUILayout.TextField("텍스트", text);
        cellSize = EditorGUILayout.FloatField("도트 간격", cellSize);
        dotSize = EditorGUILayout.FloatField("도트 크기", dotSize);
        origin = EditorGUILayout.Vector2Field("중심 위치", origin);
        dotSprite = (Sprite)EditorGUILayout.ObjectField("도트 스프라이트", dotSprite, typeof(Sprite), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("회전", EditorStyles.boldLabel);
        textTilt = EditorGUILayout.Slider("글자 기울기(도)", textTilt, -30f, 30f);
        spinAmount = EditorGUILayout.Slider("날아올 때 회전량(도)", spinAmount, 0f, 720f);
        spinRandomDir = EditorGUILayout.Toggle("회전 방향 랜덤", spinRandomDir);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("색", EditorStyles.boldLabel);
        baseColor = EditorGUILayout.ColorField("원본색 (작을 때)", baseColor);
        subColor = EditorGUILayout.ColorField("서브색 (커질 때)", subColor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("날아오기", EditorStyles.boldLabel);
        flyDistance = EditorGUILayout.FloatField("출발 거리", flyDistance);
        angleJitter = EditorGUILayout.Slider("방향 흔들기(도)", angleJitter, 0f, 60f);
        staggerWindow = EditorGUILayout.FloatField("시차 구간", staggerWindow);
        flyDuration = EditorGUILayout.FloatField("날아오는 시간", flyDuration);
        overshoot = EditorGUILayout.FloatField("오버슈트(px)", overshoot);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("맥동 (커질 때 서브색 / 작아질 때 원본색)", EditorStyles.boldLabel);
        pulseCount = EditorGUILayout.IntSlider("반복 횟수", pulseCount, 0, 10);
        pulseDuration = EditorGUILayout.FloatField("한 번의 시간", pulseDuration);
        pulseScale = EditorGUILayout.Slider("최대 배율", pulseScale, 1f, 1.5f);
        pulsePeakRatio = EditorGUILayout.Slider("커지는 구간 비율", pulsePeakRatio, 0.1f, 0.9f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("마무리", EditorStyles.boldLabel);
        holdTime = EditorGUILayout.FloatField("유지 시간", holdTime);
        fadeOutTime = EditorGUILayout.FloatField("페이드 아웃", fadeOutTime);
        backdropAlpha = EditorGUILayout.Slider("배경 알파", backdropAlpha, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(string.Format("예상 길이 {0:F2}초", TotalTime()), EditorStyles.miniLabel);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(root == null))
        {
            if (GUILayout.Button("1. 도트 배치 (기존 자식 전부 삭제)", GUILayout.Height(28)))
                BuildDots();

            using (new EditorGUI.DisabledScope(clip == null))
            {
                if (GUILayout.Button("2. 클립 굽기", GUILayout.Height(28)))
                    BuildClip();
            }

            if (GUILayout.Button("1 + 2 한번에", GUILayout.Height(28)))
            {
                BuildDots();
                if (clip != null) BuildClip();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    float LandedTime()
    {
        return staggerWindow + flyDuration;
    }

    float TotalTime()
    {
        return LandedTime() + pulseCount * pulseDuration + holdTime + fadeOutTime;
    }

    // ══════════════════════════════════════════════════════
    // 1. 도트 배치
    // ══════════════════════════════════════════════════════
    void BuildDots()
    {
        List<Vector2> cells = LayoutText(text);
        if (cells.Count == 0)
        {
            Debug.LogWarning("[Warning Builder] 만들 도트가 없습니다.");
            return;
        }

        Undo.SetCurrentGroupName("Build Warning Dots");
        int group = Undo.GetCurrentGroup();

        RectTransform container = EnsureDotsContainer();

        // 컨테이너 안(=이전 도트)만 지운다. 루트 직속 자식은 손으로 붙인 연출이므로 건드리지 않는다.
        for (int i = container.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);
        // 컨테이너를 쓰기 전 버전이 루트에 바로 꽂아둔 도트 정리 — 이름으로만 골라 다른 자식은 남긴다.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform c = root.GetChild(i);
            if (c != container && c.name.StartsWith("Dot_"))
                Undo.DestroyObjectImmediate(c.gameObject);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            var go = new GameObject(string.Format("Dot_{0:D3}", i), typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create Dot");
            go.layer = root.gameObject.layer;

            var rt = (RectTransform)go.transform;
            rt.SetParent(container, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dotSize, dotSize);
            rt.anchoredPosition = cells[i];
            rt.localEulerAngles = new Vector3(0f, 0f, textTilt);   // 도트도 같이 기울여야 획이 이어져 보인다

            var img = go.GetComponent<Image>();
            img.sprite = dotSprite;
            img.color = baseColor;
            img.raycastTarget = false;
        }

        // 색 펄스를 커브 1개로 처리하기 위한 컴포넌트
        EnsureTint(container);

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(root);
        Debug.Log("[Warning Builder] 도트 " + cells.Count + "개 배치 완료.");
    }

    /// 도트를 담을 중간 컨테이너를 찾거나 만든다. 맥동/틴트가 여기에만 걸리므로 루트의 다른 자식은
    /// 영향을 받지 않는다. Transform은 반드시 무보정(위치0·스케일1·회전0)이어야 도트 좌표가 안 밀린다.
    RectTransform EnsureDotsContainer()
    {
        Transform found = root.Find(DotsContainerName);
        var container = found as RectTransform;
        if (container == null)
        {
            if (found != null) Undo.DestroyObjectImmediate(found.gameObject); // 이름만 같고 RectTransform이 아님
            var go = new GameObject(DotsContainerName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Dots Container");
            go.layer = root.gameObject.layer;
            container = (RectTransform)go.transform;
            container.SetParent(root, false);
        }

        Undo.RecordObject(container, "Configure Dots Container");
        container.anchorMin = container.anchorMax = container.pivot = new Vector2(0.5f, 0.5f);
        container.anchoredPosition = Vector2.zero;
        container.sizeDelta = Vector2.zero;
        container.localScale = Vector3.one;
        container.localRotation = Quaternion.identity;
        return container;
    }

    /// 틴트도 컨테이너에 붙인다 — WarningDotTint는 자기 아래 Graphic을 전부 물들이므로 루트에 있으면
    /// 화살표 바 같은 다른 자식 색까지 덮어쓴다. 예전 버전이 루트에 붙여둔 건 지운다(둘이 매 프레임 싸운다).
    WarningDotTint EnsureTint(RectTransform container)
    {
        var stale = root.GetComponent<WarningDotTint>();
        if (stale != null) Undo.DestroyObjectImmediate(stale);

        var tint = container.GetComponent<WarningDotTint>();
        if (tint == null) tint = Undo.AddComponent<WarningDotTint>(container.gameObject);
        Undo.RecordObject(tint, "Configure Tint");
        tint.baseColor = baseColor;
        tint.subColor = subColor;
        tint.blend = 0f;
        tint.Refresh();
        return tint;
    }

    /// 텍스트를 도트 좌표 리스트로. 캡하이트(0~6행) 기준 세로 중앙 정렬 후 textTilt 만큼 회전.
    List<Vector2> LayoutText(string s)
    {
        var glyphs = new List<string[]>();
        foreach (char c in s)
        {
            string[] g = GetGlyph(c);
            if (g == null)
            {
                Debug.LogWarning("[Warning Builder] 글리프 없음: '" + c + "' (건너뜀)");
                continue;
            }
            glyphs.Add(g);
        }

        int totalCols = glyphs.Sum(GlyphWidth) + Mathf.Max(0, glyphs.Count - 1) * GlyphGap;
        float startX = origin.x - (totalCols - 1) * cellSize * 0.5f;
        const float centerRow = 3f; // 0~6행의 가운데

        var result = new List<Vector2>();
        int col = 0;
        foreach (string[] g in glyphs)
        {
            int w = GlyphWidth(g);
            for (int r = 0; r < g.Length; r++)
            {
                string row = g[r];
                for (int c = 0; c < w; c++)
                {
                    if (c >= row.Length || row[c] != '#') continue;
                    var p = new Vector2(
                        startX + (col + c) * cellSize,
                        origin.y + (centerRow - r) * cellSize);
                    result.Add(origin + Rotate(p - origin, textTilt));
                }
            }
            col += w + GlyphGap;
        }
        return result;
    }

    // ══════════════════════════════════════════════════════
    // 2. 클립 굽기
    // ══════════════════════════════════════════════════════
    void BuildClip()
    {
        // 루트 자식이 아니라 컨테이너 자식만 도트로 센다 — 그래야 손으로 붙인 다른 연출(화살표 바 등)이
        // 개수에 섞여 아래 불일치 검사에 걸리지 않는다.
        var container = root.Find(DotsContainerName) as RectTransform;
        if (container == null)
        {
            Debug.LogWarning("[Warning Builder] '" + DotsContainerName + "' 컨테이너가 없습니다. 1번 먼저 실행하세요.");
            return;
        }

        var dots = new List<RectTransform>();
        for (int i = 0; i < container.childCount; i++)
        {
            var rt = container.GetChild(i) as RectTransform;
            if (rt != null) dots.Add(rt);
        }
        if (dots.Count == 0)
        {
            Debug.LogWarning("[Warning Builder] 자식 도트가 없습니다. 1번 먼저 실행하세요.");
            return;
        }

        // 최종 위치는 씬에서 읽지 않고 폰트에서 다시 계산한다.
        // (프리뷰/플레이 후 anchoredPosition 은 클립이 마지막으로 샘플한 값 = 흩어진 위치일 수 있음)
        List<Vector2> cells = LayoutText(text);
        if (cells.Count != dots.Count)
        {
            Debug.LogWarning(string.Format(
                "[Warning Builder] 도트 수 불일치 (씬 {0}개 / 글자 {1}개). 1번을 먼저 실행하세요.",
                dots.Count, cells.Count));
            return;
        }

        // 시차 순서: 인덱스 해시로 섞어 결정적(deterministic)으로 만든다.
        List<int> order = Enumerable.Range(0, dots.Count)
                                    .OrderBy(i => Hash01(i))
                                    .ToList();
        var delay = new float[dots.Count];
        for (int rank = 0; rank < order.Count; rank++)
            delay[order[rank]] = dots.Count > 1 ? rank / (float)(dots.Count - 1) * staggerWindow : 0f;

        Undo.RecordObject(clip, "Build Warning Clip");
        clip.ClearCurves();
        clip.frameRate = 60f;

        for (int i = 0; i < dots.Count; i++)
        {
            RectTransform rt = dots[i];
            string path = AnimationUtility.CalculateTransformPath(rt, root);
            Vector2 end = cells[i];
            rt.anchoredPosition = end;                              // 씬이 어긋나 있었으면 같이 복구
            rt.localEulerAngles = new Vector3(0f, 0f, textTilt);

            // 중심에서 이 도트를 지나는 방향으로 바깥에 배치 (방사형)
            Vector2 fromCenter = end - origin;
            float ang = fromCenter.sqrMagnitude < 0.01f
                ? i * 137.508f * Mathf.Deg2Rad                      // 중앙에 겹친 도트는 황금각으로 분산
                : Mathf.Atan2(fromCenter.y, fromCenter.x);
            ang += (Hash01(i) * 2f - 1f) * angleJitter * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

            Vector2 start = end + dir * flyDistance;
            Vector2 over = end - dir * overshoot;   // 목표를 안쪽으로 살짝 지나침

            float t0 = delay[i];
            float t1 = t0 + flyDuration * 0.75f;
            float t2 = t0 + flyDuration;

            SetCurve(path, typeof(RectTransform), "m_AnchoredPosition.x",
                     FlyCurve(t0, start.x, t1, over.x, t2, end.x));
            SetCurve(path, typeof(RectTransform), "m_AnchoredPosition.y",
                     FlyCurve(t0, start.y, t1, over.y, t2, end.y));

            // 회전: 돌면서 날아와 기울기(textTilt)에 안착
            if (spinAmount > 0.01f)
            {
                float sign = spinRandomDir ? (Hash01(i + 9173) < 0.5f ? -1f : 1f) : 1f;
                float startZ = textTilt + sign * spinAmount;
                float overZ = textTilt - sign * spinAmount * 0.04f;
                SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z",
                         FlyCurve(t0, startZ, t1, overZ, t2, textTilt));
            }
        }

        float landed = LandedTime();
        float pulseTotal = pulseCount * pulseDuration;
        float pulseEnd = landed + pulseTotal;
        float endTime = TotalTime();

        // ── 맥동: 스케일과 색 blend 를 완전히 같은 키 타이밍으로 ──
        var scale = new AnimationCurve();
        var tintCurve = new AnimationCurve();
        scale.AddKey(0f, 1f);
        tintCurve.AddKey(0f, 0f);
        for (int p = 0; p < pulseCount; p++)
        {
            float t = landed + p * pulseDuration;
            scale.AddKey(t, 1f);
            tintCurve.AddKey(t, 0f);
            scale.AddKey(t + pulseDuration * pulsePeakRatio, pulseScale);
            tintCurve.AddKey(t + pulseDuration * pulsePeakRatio, 1f);
        }
        scale.AddKey(pulseEnd, 1f);
        tintCurve.AddKey(pulseEnd, 0f);
        scale.AddKey(endTime, 1f);
        tintCurve.AddKey(endTime, 0f);
        Flatten(scale);
        Flatten(tintCurve);

        // 맥동은 루트가 아니라 컨테이너에 — 루트에 걸면 화살표 바 등 다른 자식까지 같이 바운스한다.
        root.localScale = Vector3.one;   // 예전 버전이 루트에 구워둔 맥동이 씬에 남아 있을 수 있다
        SetCurve(DotsContainerName, typeof(RectTransform), "m_LocalScale.x", scale);
        SetCurve(DotsContainerName, typeof(RectTransform), "m_LocalScale.y", scale);
        SetCurve(DotsContainerName, typeof(RectTransform), "m_LocalScale.z",
                 new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(endTime, 1f)));

        // 색 전환: 도트마다 m_Color 를 잡지 않고 WarningDotTint.blend 커브 1개로
        EnsureTint(container);
        SetCurve(DotsContainerName, typeof(WarningDotTint), "blend", tintCurve);

        // 전체 알파는 CanvasGroup 하나로. (도트마다 Image.color.a 잡지 않는다)
        var cg = root.GetComponent<CanvasGroup>();
        if (cg == null) cg = Undo.AddComponent<CanvasGroup>(root.gameObject);
        cg.alpha = 1f;
        var alpha = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 1f),
            new Keyframe(pulseEnd + holdTime, 1f),
            new Keyframe(endTime, 0f));
        Flatten(alpha);
        SetCurve("", typeof(CanvasGroup), "m_Alpha", alpha);

        // 배경 어둡게: Directing 본인의 Image
        if (root.GetComponent<Image>() != null)
        {
            var bg = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, backdropAlpha));
            Flatten(bg);
            SetCurve("", typeof(Image), "m_Color.a", bg);
        }

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        Debug.Log(string.Format("[Warning Builder] 클립 완성. 도트 {0}개 / 길이 {1:F2}s / 맥동 {2}회",
                                dots.Count, endTime, pulseCount));
    }

    void SetCurve(string path, System.Type type, string prop, AnimationCurve curve)
    {
        var binding = new EditorCurveBinding { path = path, type = type, propertyName = prop };
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    /// 빠르게 출발 → 감속하며 오버슈트 → 목표에 안착.
    static AnimationCurve FlyCurve(float t0, float v0, float t1, float v1, float t2, float v2)
    {
        float d1 = Mathf.Max(t1 - t0, 1e-4f);
        float d2 = Mathf.Max(t2 - t1, 1e-4f);
        var k0 = new Keyframe(t0, v0) { inTangent = 0f, outTangent = (v1 - v0) / d1 * 2.2f };
        var k1 = new Keyframe(t1, v1) { inTangent = (v1 - v0) / d1 * 0.15f, outTangent = (v2 - v1) / d2 * 0.6f };
        var k2 = new Keyframe(t2, v2) { inTangent = 0f, outTangent = 0f };
        return new AnimationCurve(k0, k1, k2);
    }

    static void Flatten(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
        {
            Keyframe k = c[i];
            k.inTangent = 0f;
            k.outTangent = 0f;
            c.MoveKey(i, k);
        }
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(r);
        float sin = Mathf.Sin(r);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    static float Hash01(int i)
    {
        unchecked
        {
            uint x = (uint)i * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (x & 0xffffff) / (float)0xffffff;
        }
    }

    // ══════════════════════════════════════════════════════
    // 5x7 도트 폰트 (9행: 0~6 캡하이트, 7~8 디센더)
    // ══════════════════════════════════════════════════════
    const int GlyphGap = 1;

    static int GlyphWidth(string[] g)
    {
        return g.Max(r => r.Length);
    }

    static string[] GetGlyph(char c)
    {
        string[] g;
        if (Font.TryGetValue(c, out g)) return g;
        if (Font.TryGetValue(char.ToUpperInvariant(c), out g)) return g; // 소문자 없으면 대문자로 대체
        return null;
    }

    static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
    {
        { ' ', new[] { "...", "...", "...", "...", "...", "...", "...", "...", "..." } },
        { '!', new[] { "##", "##", "##", "##", "##", "..", "##", "..", ".." } },
        { '?', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..", ".....", "....." } },
        { '.', new[] { "..", "..", "..", "..", "..", "..", "##", "..", ".." } },

        { 'A', new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#", ".....", "....." } },
        { 'B', new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.", ".....", "....." } },
        { 'C', new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.", ".....", "....." } },
        { 'D', new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.", ".....", "....." } },
        { 'E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####", ".....", "....." } },
        { 'F', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#....", ".....", "....." } },
        { 'G', new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###.", ".....", "....." } },
        { 'H', new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#", ".....", "....." } },
        { 'I', new[] { "###", ".#.", ".#.", ".#.", ".#.", ".#.", "###", "...", "..." } },
        { 'J', new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##..", ".....", "....." } },
        { 'K', new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#", ".....", "....." } },
        { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####", ".....", "....." } },
        { 'M', new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#", ".....", "....." } },
        { 'N', new[] { "#...#", "##..#", "##..#", "#.#.#", "#..##", "#..##", "#...#", ".....", "....." } },
        { 'O', new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.", ".....", "....." } },
        { 'P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#....", ".....", "....." } },
        { 'Q', new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#", ".....", "....." } },
        { 'R', new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#", ".....", "....." } },
        { 'S', new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####.", ".....", "....." } },
        { 'T', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "....." } },
        { 'U', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.", ".....", "....." } },
        { 'V', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..", ".....", "....." } },
        // W 는 획을 실제로 기울인 9칸짜리 (5칸 일자 W 는 너무 밋밋해서)
        { 'W', new[] { "#.......#",
                       "#.......#",
                       ".#.....#.",
                       ".#..#..#.",
                       "..#.#.#..",
                       "..#.#.#..",
                       "...#.#...",
                       ".........",
                       "........." } },
        { 'X', new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#", ".....", "....." } },
        { 'Y', new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..", ".....", "....." } },
        { 'Z', new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####", ".....", "....." } },

        { 'a', new[] { ".....", ".....", ".###.", "....#", ".####", "#...#", ".####", ".....", "....." } },
        { 'e', new[] { ".....", ".....", ".###.", "#...#", "#####", "#....", ".###.", ".....", "....." } },
        { 'g', new[] { ".....", ".....", ".####", "#...#", "#...#", ".####", "....#", "#...#", ".###." } },
        { 'i', new[] { ".#.", "...", "##.", ".#.", ".#.", ".#.", "###", "...", "..." } },
        { 'n', new[] { ".....", ".....", "#.##.", "##..#", "#...#", "#...#", "#...#", ".....", "....." } },
        { 'o', new[] { ".....", ".....", ".###.", "#...#", "#...#", "#...#", ".###.", ".....", "....." } },
        { 'r', new[] { ".....", ".....", "#.##.", "##..#", "#....", "#....", "#....", ".....", "....." } },
        { 's', new[] { ".....", ".....", ".####", "#....", ".###.", "....#", "####.", ".....", "....." } },
    };
}
