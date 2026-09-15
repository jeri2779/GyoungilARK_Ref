using UnityEngine;

/// <summary>
/// 은신(Cloaking) 렌더링만 담당한다. EnemyBase가 소유하고, 저지/사망 여부를 clear로 넘겨 매 프레임 Tick으로 굴린다.
/// 재질을 영구히 덮지 않고, 은신 셰이더의 _CloakAmount를 0~1로 보간해 [본체 ↔ 흐릿]을 "점점" 전환한다.
/// Setup을 안 했거나(=은신 몹이 아님) 설정이 없으면 Tick/Reset은 조용히 no-op.
/// </summary>
public class EnemyCloak
{
    private const string SettingsPath = "Skills/CloakSettings"; // Resources/CloakSettings.asset

    private static readonly int CloakAmountId = Shader.PropertyToID("_CloakAmount");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    private CloakSettingsSO _settings;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Material[][] _originalMats;   // 렌더러별 원래 재질 — 드러날 때(amount=0) 이걸로 복귀(원본 조명/색 그대로)
    private Material[][] _cloakMats;      // 렌더러별 은신 재질 — 은신/전환 중에만 사용
    private bool _applied;                // 현재 은신 재질이 올라가 있는지
    private float _amount;                // 0=또렷 ~ 1=은신. 매 프레임 목표값으로 보간.

    public bool IsSetup => _renderers != null;

    // 은신 몹일 때 EnemyBase가 (Attribute 결정 뒤) 1회 호출. settings가 null이면 Resources에서 폴백 로드.
    public void Setup(GameObject root, CloakSettingsSO settings)
    {
        _settings = settings != null ? settings : Resources.Load<CloakSettingsSO>(SettingsPath);
        if (_settings == null || _settings.cloakMaterial == null) return;

        _renderers = root.GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
        _originalMats = new Material[_renderers.Length][];
        _cloakMats = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            Material src = r.sharedMaterial;                            // 원래 재질(안 덮어씀, 드러날 때 복귀용)
            Texture baseTex = src != null ? src.mainTexture : null;     // 본체 텍스처(없으면 흰색 폴백)
            Color baseColor = GetBaseColor(src);                        // 본체 색(_BaseColor/_Color)

            Material[] orig = r.sharedMaterials;
            _originalMats[i] = orig;                                    // 원래 재질 배열 보관
            Material[] cloak = new Material[orig.Length];
            for (int j = 0; j < cloak.Length; j++) cloak[j] = _settings.cloakMaterial;
            _cloakMats[i] = cloak;

            // 은신 재질이 올라갔을 때 원래 텍스처·색을 재현하도록 MPB에 미리 넣어둠(전환 중 본체가 섞여 보임).
            r.GetPropertyBlock(_mpb);
            if (baseTex != null) _mpb.SetTexture(BaseMapId, baseTex);
            _mpb.SetColor(BaseColorId, baseColor);
            _mpb.SetFloat(CloakAmountId, 0f);
            r.SetPropertyBlock(_mpb);
        }
        _applied = false; // 시작은 원래 재질 그대로(또렷)
    }

    // clear=true(저지/사망)면 또렷(0)로, 아니면 은신(1)로 페이드.
    public void Tick(bool clear)
    {
        if (_renderers == null) return;

        float target = clear ? 0f : 1f;
        float next = Mathf.MoveTowards(_amount, target, _settings.fadeSpeed * Time.deltaTime);
        if (next == _amount) return;
        _amount = next;

        bool needCloak = _amount > 0.0001f;
        ApplyMaterial(needCloak);          // amount>0 → 은신 재질 / amount==0 → 원래 재질(원본 그대로)
        if (needCloak) SetAmount(_amount);
    }

    // 풀 재사용/사망 등에서 또렷 상태로 되돌린다.
    public void Reset()
    {
        _amount = 0f;
        if (_renderers != null) ApplyMaterial(false);
    }

    // 은신 재질 ↔ 원래 재질 전환. 원래 재질을 영구히 덮지 않고, 필요할 때만 갈아끼운다.
    private void ApplyMaterial(bool on)
    {
        if (on == _applied) return;
        for (int i = 0; i < _renderers.Length; i++)
            _renderers[i].sharedMaterials = on ? _cloakMats[i] : _originalMats[i];
        _applied = on;
    }

    // 모든 렌더러의 _CloakAmount를 MPB로 갱신(재질 인스턴스 생성 없이 개체별로).
    private void SetAmount(float amount)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].GetPropertyBlock(_mpb);
            _mpb.SetFloat(CloakAmountId, amount);
            _renderers[i].SetPropertyBlock(_mpb);
        }
    }

    // 원래 재질의 본체 색을 최대한 정확히 뽑아온다(_BaseColor 우선, 없으면 _Color, 둘 다 없으면 흰색).
    private static Color GetBaseColor(Material src)
    {
        if (src == null) return Color.white;
        if (src.HasProperty(BaseColorId)) return src.GetColor(BaseColorId);
        if (src.HasProperty(LegacyColorId)) return src.GetColor(LegacyColorId);
        return Color.white;
    }
}
