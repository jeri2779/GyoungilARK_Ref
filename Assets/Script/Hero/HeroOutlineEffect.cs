using System.Collections.Generic;
using UnityEngine;

// 선택된 영웅에 실루엣 테두리를 표시한다. 대상의 모든 렌더러를 복제해 확장된 뒷면만
// 그리는 "Custom/HeroOutline" 머티리얼을 입힌다(Inverted Hull 방식). 최초 SetActive(true)
// 호출 시에만 복제본을 만들고(지연 생성), 이후엔 활성 상태만 토글한다.
public class HeroOutlineEffect
{
    private static Material sharedMaterial;

    private readonly Transform root;
    private readonly List<GameObject> clones = new();
    private bool built;

    public HeroOutlineEffect(Transform root)
    {
        this.root = root;
    }

    public void SetActive(bool on)
    {
        if (on && !built)
        {
            Build();
        }

        foreach (GameObject clone in clones)
        {
            clone.SetActive(on);
        }
    }

    private void Build()
    {
        built = true;
        Material material = GetSharedMaterial();

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                clones.Add(BuildSkinnedClone(skinned, material));
            }
            else if (renderer is MeshRenderer meshRenderer)
            {
                clones.Add(BuildStaticClone(meshRenderer, material));
            }
        }
    }

    private static GameObject BuildSkinnedClone(SkinnedMeshRenderer source, Material material)
    {
        GameObject clone = new GameObject("HeroOutline");
        clone.transform.SetParent(source.transform, false);

        SkinnedMeshRenderer smr = clone.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = source.sharedMesh;
        smr.bones = source.bones;
        smr.rootBone = source.rootBone;
        smr.sharedMaterials = BuildMaterialArray(source.sharedMesh, material);

        clone.SetActive(false);
        return clone;
    }

    private static GameObject BuildStaticClone(MeshRenderer source, Material material)
    {
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        Mesh mesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
        GameObject clone = new GameObject("HeroOutline");
        clone.transform.SetParent(source.transform, false);

        MeshFilter filter = clone.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer meshRenderer = clone.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = BuildMaterialArray(mesh, material);

        clone.SetActive(false);
        return clone;
    }

    // 서브메쉬가 여러 개인 경우 전부 같은 외곽선 머티리얼로 채워야 남는 서브메쉬가 분홍색(머티리얼 누락)으로 보이지 않는다.
    private static Material[] BuildMaterialArray(Mesh mesh, Material material)
    {
        int count = mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 1;
        Material[] materials = new Material[count];
        for (int i = 0; i < count; i++) materials[i] = material;
        return materials;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial == null)
        {
            sharedMaterial = new Material(Shader.Find("Custom/HeroOutline"));
        }

        return sharedMaterial;
    }
}
