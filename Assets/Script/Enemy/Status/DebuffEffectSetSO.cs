using UnityEngine;

/// <summary>
/// 디버프별 이펙트 공용 설정. DebuffIconSetSO와 같은 취지 — 이펙트 프리팹은 유닛마다 다를 이유가 없으므로 에셋 하나로 돌려쓴다.
/// 프리팹에 남는 건 앵커(머리·몸통) 두 개뿐이다. 그건 프리팹 자기 계층을 가리키므로 공유할 수 없다.
/// </summary>
[CreateAssetMenu(menuName = "Enemy/Debuff Effect Set", fileName = "DebuffEffectSet")]
public class DebuffEffectSetSO : ScriptableObject
{
    [Tooltip("종류별 이펙트. 여기 등록되고 프리팹이 들어 있는 종류만 뜬다.")]
    public DebuffEffect[] effects;
}

/// <summary>
/// 이펙트가 나올 자리.
/// Head/Body는 유닛마다 높이가 달라 프리팹에 빈 오브젝트를 꽂아야 한다(안 꽂으면 그 칸은 안 뜬다).
/// Foot은 유닛 원점이라 앵커가 필요 없다 — 발밑은 크기와 무관하게 항상 원점이다.
/// </summary>
public enum DebuffEffectAnchor
{
    Head,   // 머리 위 — 기절 별처럼 위에 떠야 하는 것. 앵커 필요
    Body,   // 몸통 — 독·화상처럼 몸에서 피어오르는 것. 앵커 필요
    Foot,   // 발밑(유닛 원점) — 둔화 장판처럼 바닥에 깔리는 것. 앵커 불필요
}

/// <summary>
/// 디버프 종류 하나와 그때 소환할 이펙트 짝.
/// type에 여러 종류를 OR로 묶으면 "그 중 아무거나 걸렸을 때" 하나의 이펙트로 표시된다
/// (예: Poison|Ignite|Bleed를 묶어 "지속 피해 중" 연출 하나로).
/// </summary>
[System.Serializable]
public struct DebuffEffect
{
    [Tooltip("이 이펙트가 나타낼 디버프 종류. 여러 개 고르면 그 중 하나라도 걸렸을 때 뜬다.")]
    public DebuffType type;

    [Tooltip("소환할 이펙트 프리팹. 비우면 이 칸은 무시된다.")]
    public GameObject prefab;

    [Tooltip("어느 자리에서 나올지. Head·Body는 프리팹에 앵커를 꽂아야 뜨고, Foot은 유닛 원점이라 앵커가 필요 없다.")]
    public DebuffEffectAnchor anchor;

    [Tooltip("이펙트가 뜨는 순간 같이 낼 효과음 키(EnemySoundDataBase의 key). 비우면 소리 없이 이펙트만 뜬다.\n" +
             "이펙트가 도는 동안 계속이 아니라 걸린 순간 한 번만 난다 — 겹쳐 걸려 이펙트가 유지되는 동안엔 다시 나지 않는다.")]
    public string soundKey;

    [Tooltip("앵커 기준 추가 오프셋(월드). 이 에셋은 모든 유닛이 공유하므로 유닛 크기 차이는 여기서 못 메운다 " +
             "— 크기 보정은 프리팹 앵커로 하고, 여기선 모든 유닛에 공통으로 줄 미세 조정만 넣을 것.")]
    public Vector3 offset;
}
