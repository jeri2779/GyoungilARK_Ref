using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class GuideTabSelector : MonoBehaviour
{
    [SerializeField] private Image gamePlayImage;
    [SerializeField] private Image heroImage;
    [SerializeField] private Image baseImage;
    [SerializeField] private Image enemyImage;
    [SerializeField] private Image gimmickImage;

    [SerializeField] private TextMeshProUGUI gamePlayText;
    [SerializeField] private TextMeshProUGUI heroText;
    [SerializeField] private TextMeshProUGUI baseText;
    [SerializeField] private TextMeshProUGUI enemyText;
    [SerializeField] private TextMeshProUGUI gimmickText;

    [SerializeField] private Color selectedTint = new Color(0.502f, 0.361f, 0.204f);
    [SerializeField] private Color normalTint = Color.white;
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color normalTextColor = new Color(0.196f, 0.196f, 0.196f);

    private const string HeldParameter = "Held";

    private GimmickTileData gimmickTileData;

    // 기믹 기록장을 컨테이너에서 받아 둔다(이 패널은 프리팹이라 씬 오브젝트를 직접 참조할 수 없다).
    [Inject]
    private void Construct(GimmickTileData gimmickTileData)
    {
        this.gimmickTileData = gimmickTileData;
    }

    // 기믹 탭을 선택 상태로 표시한다
    public void SelectGimmickTab()
    {
        ApplySelection(gimmickImage);
    }

    private void OnEnable()
    {
        ApplyGimmickTabVisible();
        ApplySelection(gamePlayImage);
    }

    // 기믹 안내를 한 번이라도 본 뒤부터 기믹 탭 버튼을 내보인다.
    private void ApplyGimmickTabVisible()
    {
         if (gimmickTileData == null) return;
        gimmickImage.gameObject.SetActive(gimmickTileData.HasAnyShown());
    }

    // 게임 플레이 탭을 선택 상태로 표시한다
    public void SelectGamePlayTab()
    {
        ApplySelection(gamePlayImage);
    }

    // 영웅 탭을 선택 상태로 표시한다
    public void SelectHeroTab()
    {
        ApplySelection(heroImage);
    }

    // 거점 탭을 선택 상태로 표시한다
    public void SelectBaseTab()
    {
        ApplySelection(baseImage);
    }

    // 적 탭을 선택 상태로 표시한다
    public void SelectEnemyTab()
    {
        ApplySelection(enemyImage);
    }

    // 선택된 탭만 강조한다 (클릭 이펙트는 전역 ClickEffect가 담당)
    private void ApplySelection(Image selectedImage)
    {
        SetTabState(gamePlayImage, gamePlayText, selectedImage);
        SetTabState(heroImage, heroText, selectedImage);
        SetTabState(baseImage, baseText, selectedImage);
        SetTabState(enemyImage, enemyText, selectedImage);
        SetTabState(gimmickImage, gimmickText, selectedImage);
    }

    // 탭 하나의 배경색과 글씨색을 선택 여부에 맞게 반영한다
    private void SetTabState(Image image, TextMeshProUGUI text, Image selectedImage)
    {
        if (!image.gameObject.activeSelf) return;

        SetTabHeld(image, image == selectedImage);

        if (image == selectedImage)
        {
            image.color = selectedTint;
            text.color = selectedTextColor;
            return;
        }
        image.color = normalTint;
        text.color = normalTextColor;
    }

    // 탭 버튼이 눌린 모양으로 남아 있을지를 애니메이터 또는 ButtonPressScale에 알린다 (둘 다 선택적이다)
    private void SetTabHeld(Image image, bool held)
    {
        Animator buttonAnimator = image.GetComponent<Animator>();
        if (buttonAnimator != null) buttonAnimator.SetBool(HeldParameter, held);
        image.GetComponent<ButtonPressScale>()?.SetHeld(held);
    }
}
