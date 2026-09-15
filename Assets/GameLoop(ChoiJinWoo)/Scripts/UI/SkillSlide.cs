using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SkillSlide : MonoBehaviour
{
    private static readonly int InHash = Animator.StringToHash("SkillIn");
    private static readonly int OutHash = Animator.StringToHash("SkillOut");
    private Animator slideAnim;

    // 같은 오브젝트의 Animator를 보관한다.
    private void Awake()
    {
        slideAnim = GetComponent<Animator>();
    }

    // 패널을 활성화하고 화면 안으로 이동시킨다.
    public void Open()
    {
        gameObject.SetActive(true);
        slideAnim.Play(InHash, 0, 0f);
    }

    // 패널을 화면 위로 이동시킨다.
    public void Close()
    {
        slideAnim.Play(OutHash, 0, 0f);
    }

    // 시작 시 퇴장 연출 없이 패널을 숨긴다.
    public void HideNow()
    {
        gameObject.SetActive(false);
    }

    // 닫기 애니메이션이 끝난 패널을 비활성화한다.
    public void FinishClose()
    {
        gameObject.SetActive(false);
    }
}
