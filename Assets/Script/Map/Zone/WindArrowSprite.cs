using UnityEngine;

// 모든 안내 화살표가 함께 쓰는 이미지를 코드로 그려 만드는 생성 전담.
public static class WindArrowSprite
{
    private const int TextureSize = 32;

    // 위쪽을 가리키는 단순한 화살표 스프라이트를 새로 만든다.
    public static Sprite CreateSprite()
    {
        Texture2D texture = CreateTexture();
        Rect rect = new Rect(0f, 0f, TextureSize, TextureSize);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), TextureSize);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // 화살표 모양을 픽셀로 채운 이미지를 만든다.
    private static Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.DontSave;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(ReadPixels());
        texture.Apply();
        return texture;
    }

    // 이미지 전체 픽셀 색을 한 번에 계산한다.
    private static Color[] ReadPixels()
    {
        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int row = 0; row < TextureSize; row++)
        {
            for (int column = 0; column < TextureSize; column++)
            {
                pixels[row * TextureSize + column] = ReadPixel(column, row);
            }
        }

        return pixels;
    }

    // 한 픽셀이 화살표 몸통이나 머리 안에 있는지 계산한다.
    private static Color ReadPixel(int column, int row)
    {
        bool shaft = column >= 13 && column <= 18 && row >= 3 && row <= 19;
        int halfWidth = 29 - row;
        bool headRow = row >= 15 && row <= 29;
        bool head = headRow && Mathf.Abs(column - 16) <= halfWidth;
        if (shaft || head)
        {
            return Color.white;
        }

        return Color.clear;
    }
}
