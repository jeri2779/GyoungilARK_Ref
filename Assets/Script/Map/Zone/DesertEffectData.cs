using System.Collections.Generic;

// 사막 지대 전용 — 밤마다 계산된 노출 칸의 강한/약한 이펙트 배치 대상을 보관합니다.
public class DesertEffectData
{
    private readonly List<Tile> strongTiles = new();
    private readonly List<Tile> weakTiles = new();

    public IReadOnlyList<Tile> StrongTiles => strongTiles;
    public IReadOnlyList<Tile> WeakTiles => weakTiles;

    // 이 타일을 강한 이펙트 대상으로 보관한다.
    public void KeepStrong(Tile tile)
    {
        strongTiles.Add(tile);
    }

    // 이 타일을 약한 이펙트 대상으로 보관한다.
    public void KeepWeak(Tile tile)
    {
        weakTiles.Add(tile);
    }
}
