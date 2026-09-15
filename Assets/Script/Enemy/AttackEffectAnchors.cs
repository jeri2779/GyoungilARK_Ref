using UnityEngine;

// 공격 스윙마다 이펙트가 나올 위치들. 스윙 순서대로 순환한다.
// 팔 2개 보스는 2개(좌/우), 다지형 몹은 3~4개, 단일 부위면 1개만 넣으면 된다.
// 이 컴포넌트가 없는 적은 스킬이 owner 위치로 폴백하므로, 손 개념이 없는 적엔 붙이지 않으면 된다.
public class AttackEffectAnchors : MonoBehaviour
{
    [Tooltip("스윙 순서대로 순환하며 쓰는 이펙트 앵커들. 인스펙터에서 실제 본(손 등)에 할당.")]
    [SerializeField] private Transform[] points;

    // i번째 스윙에 쓸 앵커. 개수에 맞춰 순환(2개면 좌/우 번갈아). 없으면 null → 스킬이 폴백.
    public Transform Get(int swingIndex)
    {
        if (points == null || points.Length == 0) return null;
        return points[swingIndex % points.Length];
    }
}
