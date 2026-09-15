using UnityEngine;

// 화면 어디를 눌러도 그 자리에 클릭 마크를 하나 띄운다.
public class ClickEffect : MonoBehaviour
{
    [SerializeField] private RectTransform effectRoot;
    [SerializeField] private GameObject markPrefab;
    [SerializeField] private float markScale = 1f;

    private ClickInput clickInput;

    // 클릭 입력 객체를 준비한다.
    private void Awake()
    {
        clickInput = new ClickInput();
    }

    // 활성화 중에만 클릭 이벤트를 받는다.
    private void OnEnable()
    {
        clickInput.Clicked += SpawnMark;
        clickInput.Enable();
    }

    // 비활성화되면 클릭 이벤트 수신을 멈춘다.
    private void OnDisable()
    {
        clickInput.Disable();
        clickInput.Clicked -= SpawnMark;
    }

    // 입력 객체가 가진 자원을 정리한다.
    private void OnDestroy()
    {
        clickInput.Dispose();
    }

    // 클릭 자리에 마크를 만들고, 재생이 끝나면 없앤다
    private void SpawnMark(Vector2 screenPosition)
    {
        GameObject mark = Instantiate(markPrefab, effectRoot);
        RectTransform markRect = (RectTransform)mark.transform;
        markRect.anchoredPosition = LocalPosition(screenPosition);
        markRect.sizeDelta *= markScale;
    }

    // 화면 좌표를 전용 캔버스 기준 로컬 좌표로 바꾼다 (오버레이 캔버스라 카메라는 필요 없다)
    private Vector2 LocalPosition(Vector2 screenPosition)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(effectRoot, screenPosition, null, out localPoint);
        return localPoint;
    }

}
