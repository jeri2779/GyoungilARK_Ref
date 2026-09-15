using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 배치 모드에서 커서가 가리키는 자리에 놓일 모습을 미리 세워 보여준다.
public class PlaceGhost
{
    // 타일 색 판보다 늦게 그리는 큐(큐가 거리 정렬보다 우선한다).
    private const int GhostQueue = (int)RenderQueue.Transparent + 100;

    // 반투명으로 바꿀 때 건드리는 URP 프로퍼티.
    private const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    private readonly float alpha;
    private readonly Dictionary<GameObject, GameObject> ghosts = new();   // 프리팹 → 만들어 둔 미리보기
    private readonly Dictionary<Material, Material> fades = new();        // 원본 머티리얼 → 반투명 사본
    private readonly GameObject holder;                                   // 미리보기들을 모아 두는 씬 오브젝트
    private readonly List<Material> slots = new();                        // 렌더러 머티리얼을 담아 두고 다시 쓰는 통
    private GameObject shownGhost;

    public PlaceGhost(float alpha)
    {
        this.alpha = alpha;
        holder = new GameObject("Place Ghosts");
    }

    // 찾아 둔 자리에 미리보기를 세운다.
    public void ShowGhost(GameObject prefab, PlaceData data)
    {
        MakeGhost(prefab);
        GameObject ghost = ghosts[prefab];
        ghost.transform.position = data.Position;
        SwapGhost(ghost);
    }

    // 만들어 둔 미리보기를 전부 버린다.
    public void ClearGhosts()
    {
        Object.Destroy(holder);
        ghosts.Clear();
        shownGhost = null;
        DestroyFades();
    }

    // 사본만 버린다. 못 바꿔서 원본을 담아 둔 항목은 프로젝트 에셋이라 파괴하면 안 된다.
    private void DestroyFades()
    {
        foreach (KeyValuePair<Material, Material> pair in fades)
        {
            if (pair.Key == pair.Value)
            {
                continue;
            }

            Object.Destroy(pair.Value);
        }

        fades.Clear();
    }

    // 장부에 빠진 미리보기를 채운다.
    private void MakeGhost(GameObject prefab)
    {
        if (ghosts.ContainsKey(prefab))
        {
            return;
        }

        ghosts[prefab] = BuildGhost(prefab);
    }

    // 겉모습만 남긴 복제본을 만든다. 꺼둔 껍데기 안에서 복제해야 스크립트가 깨어나지 않는다.
    private GameObject BuildGhost(GameObject prefab)
    {
        // hideFlags(DontSave)를 붙이면 씬 언로드에서 빠져 플레이 종료 후에도 남는다.
        GameObject ghost = new(prefab.name + " (Ghost)");
        ghost.transform.SetParent(holder.transform);
        ghost.SetActive(false);

        GameObject model = Object.Instantiate(prefab, ghost.transform);
        model.transform.localPosition = Vector3.zero;   // 확정 배치가 유닛 루트를 자리에 딱 놓는 것과 같게
        StripHidden(model);
        StripScripts(model);
        FadeGhost(model);

        return ghost;
    }

    // 복제본에서 꺼져 있는 파츠를 떼어낸다(모듈러 캐릭터는 대부분이 꺼진 파츠다).
    private static void StripHidden(GameObject model)
    {
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
        {
            if (part == null)
            {
                continue;   // 부모를 먼저 지우면 배열에 남아 있던 자식은 이미 파괴돼 있다
            }

            if (!part.gameObject.activeSelf)
            {
                Object.DestroyImmediate(part.gameObject);
            }
        }
    }

    // 복제본에서 유닛 스크립트를 떼어낸다(Animator는 남아 컨트롤러 기본 상태인 idle을 재생한다).
    private static void StripScripts(GameObject model)
    {
        foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.DestroyImmediate(script);
        }
    }

    // 복제본의 머티리얼을 반투명 사본으로 바꾼다.
    private void FadeGhost(GameObject model)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            FadeRenderer(renderer);
        }
    }

    // 렌더러의 머티리얼 슬롯을 사본으로 갈아 끼운다.
    private void FadeRenderer(Renderer renderer)
    {
        renderer.GetSharedMaterials(slots);   // 배열을 새로 받는 sharedMaterials와 달리 할당이 없다

        for (int index = 0; index < slots.Count; index++)
        {
            Material source = slots[index];
            MakeFade(source);
            slots[index] = fades[source];
        }

        renderer.SetSharedMaterials(slots);
    }

    // 장부에 빠진 반투명 사본을 채운다. 못 바꾸는 머티리얼은 원본을 그대로 쓴다.
    private void MakeFade(Material source)
    {
        if (fades.ContainsKey(source))
        {
            return;
        }

        if (!CanFade(source))
        {
            Debug.LogWarning($"[PlaceGhost] {source.shader.name}에 URP 투명 프로퍼티가 없어 미리보기가 불투명하게 뜹니다.");
            fades[source] = source;
            return;
        }

        fades[source] = BuildFade(source);
    }

    // 반투명으로 바꿀 수 있는 머티리얼인지 본다. 없는 프로퍼티에 쓰면 유니티가 조용히 무시한다.
    private static bool CanFade(Material source)
    {
        return source.HasProperty(BaseColorId)
            && source.HasProperty(SurfaceId)
            && source.HasProperty(SrcBlendId)
            && source.HasProperty(DstBlendId)
            && source.HasProperty(ZWriteId)
            && new LocalKeyword(source.shader, TransparentKeyword).isValid;
    }

    // 원본을 베낀 반투명 머티리얼을 만든다. 설정은 URP 투명 그대로다.
    private Material BuildFade(Material source)
    {
        Material fade = new(source);

        Color color = fade.GetColor(BaseColorId);
        color.a = alpha;
        fade.SetColor(BaseColorId, color);

        fade.SetFloat(SurfaceId, 1f);
        fade.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        fade.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
        fade.SetFloat(ZWriteId, 0f);   // 깊이를 쓰면 렌더러 정렬이 위치에 따라 뒤바뀌어 부분마다 진하기가 달라진다
        fade.SetKeyword(new LocalKeyword(fade.shader, TransparentKeyword), true);
        fade.renderQueue = GhostQueue;

        return fade;
    }

    // 보이는 미리보기를 갈아 끼운다.
    private void SwapGhost(GameObject ghost)
    {
        if (shownGhost == ghost)
        {
            return;
        }

        HideGhost();
        ghost.SetActive(true);
        shownGhost = ghost;
    }

    // 보이는 미리보기를 감춘다.
    public void HideGhost()
    {
        if (shownGhost == null)
        {
            return;
        }

        shownGhost.SetActive(false);
        shownGhost = null;
    }
}
