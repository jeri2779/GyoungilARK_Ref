using UnityEngine;

/// <summary>
/// 디버프 아이콘 공용 설정. 아이콘 이미지는 유닛마다 다를 이유가 없으므로 에셋 하나로 돌려쓴다
/// (CloakSettingsSO와 같은 취지 — 프리팹마다 같은 걸 다시 꽂지 않는다).
///
/// 프리팹에 남는 건 debuffIconRoot 하나뿐이다. 그건 그 프리팹 자기 계층(GridLayoutGroup)을 가리키므로 공유할 수 없다.
/// 테마별로 아이콘을 다르게 쓰고 싶어지면 이 에셋을 하나 더 만들어 해당 프리팹에만 꽂으면 된다.
/// </summary>
[CreateAssetMenu(menuName = "Enemy/Debuff Icon Set", fileName = "DebuffIconSet")]
public class DebuffIconSetSO : ScriptableObject
{
    [Tooltip("디버프 아이콘 1칸 프리팹. Image 컴포넌트가 있어야 하고, 스프라이트는 아래 icons에서 종류별로 갈아끼운다.")]
    public GameObject iconPrefab;

    [Tooltip("종류별 아이콘 스프라이트. 여기 등록되고 스프라이트가 들어 있는 종류만 표시된다.")]
    public EnemyDebuffIcon[] icons;
}
