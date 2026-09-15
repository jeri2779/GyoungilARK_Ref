using UnityEngine;
using UnityEngine.InputSystem;

// 왼쪽 버튼 누름 상태에 따라 운영체제 커서 그림을 평상시/눌림 텍스처로 교체한다.
public class HardwareCursor : MonoBehaviour
{
    private const TextureFormat CursorTextureFormat = TextureFormat.RGBA32;

    [SerializeField] private Texture2D sourceTexture;
    [SerializeField] private Vector2 hotspot;
    [Range(8, 512)]
    [SerializeField] private int cursorSize = 64;
    [Range(0.5f, 1f)]
    [SerializeField] private float pressedScale = 0.92f;
    [SerializeField] private float pressedOffsetY = 8f;

    private InputAction clickAction;
    private Texture2D normalTexture;
    private Texture2D pressedTexture;

    // 좌클릭 입력을 만들고 커서 텍스처를 미리 구워둔다.
    private void Awake()
    {
        clickAction = new InputAction(
            "CursorClick",
            InputActionType.Button,
            "<Mouse>/leftButton");

        RebuildCursorTextures();
    }

    // 활성화되는 동안 클릭 이벤트를 구독하고 평상시 그림을 적용한다.
    private void OnEnable()
    {
        clickAction.performed += OnPressed;
        clickAction.canceled += OnReleased;
        clickAction.Enable();
        ApplyNormalCursor();
    }

    // 비활성화되면 구독을 끊고 운영체제 기본 커서로 되돌린다.
    private void OnDisable()
    {
        clickAction.Disable();
        clickAction.performed -= OnPressed;
        clickAction.canceled -= OnReleased;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    // 입력 자원과 생성해둔 텍스처를 정리한다.
    private void OnDestroy()
    {
        clickAction.Dispose();
        DestroyCursorTexture(normalTexture);
        DestroyCursorTexture(pressedTexture);
    }

    // 인스펙터 값이 바뀌면 커서 텍스처를 즉시 다시 굽고, 플레이 중이면 화면에도 바로 반영한다.
    private void OnValidate()
    {
        RebuildCursorTextures();

        if (Application.isPlaying)
        {
            ApplyNormalCursor();
        }
    }

    // 눌림 텍스처로 커서 그림을 바꾼다.
    private void OnPressed(InputAction.CallbackContext context)
    {
        Cursor.SetCursor(pressedTexture, hotspot, CursorMode.Auto);
    }

    // 평상시 텍스처로 커서 그림을 되돌린다.
    private void OnReleased(InputAction.CallbackContext context)
    {
        ApplyNormalCursor();
    }

    // 평상시 커서 그림을 운영체제에 적용한다.
    private void ApplyNormalCursor()
    {
        Cursor.SetCursor(normalTexture, hotspot, CursorMode.Auto);
    }

    // 목표 크기의 평상시 텍스처와, 그걸 축소·하강시킨 눌림 텍스처를 새로 만든다.
    private void RebuildCursorTextures()
    {
        if (sourceTexture == null)
        {
            return;
        }

        int targetWidth = Mathf.RoundToInt(sourceTexture.width * (float)cursorSize / sourceTexture.height);
        int targetHeight = cursorSize;

        DestroyCursorTexture(normalTexture);
        DestroyCursorTexture(pressedTexture);

        normalTexture = ResizeWithGpu(sourceTexture, targetWidth, targetHeight);
        pressedTexture = BuildPressedTexture(normalTexture, pressedScale, pressedOffsetY);
    }

    // GPU 필터링(바일리니어)으로 원본을 목표 크기로 리사이즈한다.
    private Texture2D ResizeWithGpu(Texture2D source, int width, int height)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;

        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;

        Texture2D result = new Texture2D(width, height, CursorTextureFormat, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(renderTexture);

        return result;
    }

    // 평상시 텍스처를 GPU로 축소하고, 그만큼 캔버스 위쪽에 여백을 더한 자리에 붙여 눌림 텍스처를 만든다(잘림 원천 차단).
    private Texture2D BuildPressedTexture(Texture2D normal, float scale, float offsetY)
    {
        int width = normal.width;
        int normalHeight = normal.height;
        int scaledWidth = Mathf.RoundToInt(width * scale);
        int scaledHeight = Mathf.RoundToInt(normalHeight * scale);
        int offsetYPixels = Mathf.Max(0, Mathf.RoundToInt(offsetY));
        int pressedHeight = normalHeight + offsetYPixels;

        int offsetX = (width - scaledWidth) / 2;
        int startY = (normalHeight - scaledHeight) / 2;

        Texture2D shrunk = ResizeWithGpu(normal, scaledWidth, scaledHeight);
        Texture2D pressed = CompositeOnTransparentCanvas(shrunk, width, pressedHeight, offsetX, startY);

        DestroyCursorTexture(shrunk);
        return pressed;
    }

    // 투명하게 비운 캔버스의 지정된 위치에 작은 텍스처를 GPU로 그대로 복사해 옮긴다.
    private Texture2D CompositeOnTransparentCanvas(Texture2D source, int canvasWidth, int canvasHeight, int destX, int destY)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(canvasWidth, canvasHeight, 0, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;

        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.clear);
        Graphics.CopyTexture(source, 0, 0, 0, 0, source.width, source.height, renderTexture, 0, 0, destX, destY);

        Texture2D result = new Texture2D(canvasWidth, canvasHeight, CursorTextureFormat, false);
        result.ReadPixels(new Rect(0, 0, canvasWidth, canvasHeight), 0, 0);
        result.Apply();

        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(renderTexture);

        return result;
    }

    // 재생성 전 이전 텍스처를 정리한다.
    private void DestroyCursorTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }
    }
}
