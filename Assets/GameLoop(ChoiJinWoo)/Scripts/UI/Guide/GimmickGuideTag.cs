using UnityEngine;

// 이 가이드 항목이 어느 지역의 기믹을 설명하는지 표시한다.
public class GimmickGuideTag : MonoBehaviour
{
    [SerializeField] private int moduleId;

    public int ModuleId => moduleId;
}
