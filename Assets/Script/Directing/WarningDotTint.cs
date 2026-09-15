using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Warning 도트들의 색을 한꺼번에 흔든다.
/// 애니메이션은 <see cref="blend"/> 하나만 잡으면 되고, 자식 Graphic 전부가
/// baseColor ↔ subColor 로 보간된다.
/// (도트마다 m_Color 커브를 잡으면 커브가 수백 개로 늘어나므로 이렇게 묶는다)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class WarningDotTint : MonoBehaviour
{
    [Tooltip("작아져 있을 때(기본) 색")]
    public Color baseColor = new Color(0.85f, 0.12f, 0.12f, 1f);

    [Tooltip("커졌을 때 가는 색")]
    public Color subColor = new Color(1f, 0.82f, 0.25f, 1f);

    [Range(0f, 1f)]
    [Tooltip("애니메이션 클립이 흔드는 값. 0=원본색, 1=서브색")]
    public float blend;

    Graphic[] targets;
    float applied = -1f;

    void OnEnable()
    {
        Refresh();
    }

    void OnDisable()
    {
        applied = -1f;
    }

    void LateUpdate()
    {
        if (!Mathf.Approximately(applied, blend)) Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 인스펙터에서 색을 바꾸면 바로 반영
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Refresh();
        };
    }
#endif

    /// 자식 목록을 다시 모으고 즉시 반영. 도트를 새로 생성한 뒤 호출한다.
    public void Refresh()
    {
        Collect();
        Apply();
    }

    void Collect()
    {
        Graphic[] found = GetComponentsInChildren<Graphic>(true);
        int n = 0;
        for (int i = 0; i < found.Length; i++)
            if (found[i].gameObject != gameObject) n++;   // 루트 자신(배경 Image)은 제외

        targets = new Graphic[n];
        n = 0;
        for (int i = 0; i < found.Length; i++)
            if (found[i].gameObject != gameObject) targets[n++] = found[i];
    }

    void Apply()
    {
        if (targets == null) Collect();

        Color c = Color.Lerp(baseColor, subColor, blend);
        bool stale = false;
        for (int i = 0; i < targets.Length; i++)
        {
            Graphic g = targets[i];
            if (g == null) { stale = true; continue; }
            g.color = c;
        }
        if (stale) Collect();   // 도트가 지워졌으면 다음 프레임부터 새 목록으로

        applied = blend;
    }
}
