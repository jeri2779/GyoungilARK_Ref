using UnityEngine;

// 타일 하나의 표시 정보를 보관합니다.
public readonly struct PaintEntry
{
    public readonly Tile Tile;
    public readonly Color Color;

    // 타일과 표시 색상을 보관합니다.
    public PaintEntry(Tile tile, Color color)
    {
        Tile = tile;
        Color = color;
    }
}
