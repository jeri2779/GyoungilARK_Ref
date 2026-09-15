using UnityEngine;

[RequireComponent(typeof(Animator))]
public class BuildPanelSlide : MonoBehaviour
{
    [SerializeField] private GameObject buildPanel;

    private static readonly int InHash = Animator.StringToHash("BuildPanelIn");
    private static readonly int OutHash = Animator.StringToHash("BuildPanelOut");
    private Animator slideAnim;

    // 같은 오브젝트의 Animator를 보관한다.
    private void Awake()
    {
        slideAnim = GetComponent<Animator>();
    }

    // 빌드 패널을 활성화하고 화면 안으로 이동시킨다.
    public void Open()
    {
        buildPanel.SetActive(true);
        slideAnim.Play(InHash, 0, 0f);
    }

    // 빌드 패널을 화면 밖으로 이동시킨다.
    public void Close()
    {
        slideAnim.Play(OutHash, 0, 0f);
    }

    // 닫기 애니메이션이 끝난 빌드 패널을 비활성화한다.
    public void FinishClose()
    {
        buildPanel.SetActive(false);
    }
}
