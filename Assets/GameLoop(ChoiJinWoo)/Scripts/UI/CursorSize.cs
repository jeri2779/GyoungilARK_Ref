using UnityEngine;
using UnityEngine.UI;

// 인스펙터의 크기 값을 기준으로 커서 이미지의 긴 변과 원본 비율을 맞춘다.
[RequireComponent(typeof(Image))]
public class CursorSize : MonoBehaviour
{
    [Min(1f)]
    [SerializeField] private float cursorSize = 100f;

    private RectTransform cursorRect;
    private Image cursorImage;

    // 실행 시작 시 필요한 부품을 보관하고 설정 크기를 적용한다.
    private void Awake()
    {
        CacheParts();
        ApplySize();
    }

    // 인스펙터 값이 바뀌면 편집 화면에 크기를 즉시 적용한다.
    private void OnValidate()
    {
        CacheParts();
        ApplySize();
    }

    // 같은 오브젝트의 커서 UI 부품을 보관한다.
    private void CacheParts()
    {
        cursorRect = (RectTransform)transform;
        cursorImage = GetComponent<Image>();
    }

    // 원본 비율로 계산한 크기를 RectTransform에 적용한다.
    private void ApplySize()
    {
        Vector2 sourceSize = ReadSize();
        float sourceMax = Mathf.Max(sourceSize.x, sourceSize.y);
        float sizeScale = cursorSize / sourceMax;

        cursorRect.sizeDelta = sourceSize * sizeScale;
    }

    // 현재 Sprite의 원본 픽셀 크기를 반환한다.
    private Vector2 ReadSize()
    {
        if (cursorImage.sprite == null)
        {
            return Vector2.one;
        }

        return cursorImage.sprite.rect.size;
    }
}
