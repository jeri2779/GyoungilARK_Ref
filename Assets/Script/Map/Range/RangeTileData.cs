using System.Collections.Generic;

// 지금 든 범위를 채워 그릴지, 어떤 외곽선으로 그릴지 가리키는 갈림값.
public enum RangeEdgeKind
{
    None,
    Campfire,
    Windwall,
}

// 계산된 사거리 칸을 들고 있다가 내준다. 계산하거나 표시하지는 않는다.
public class RangeTileData
{
    private readonly List<Tile> tiles = new();

    public IReadOnlyList<Tile> Tiles => tiles;

    // 내용이 바뀔 때마다 올라간다 — TilePaintView가 다시 그릴지 판단하는 값.
    public int Version { get; private set; }

    // 지금 든 범위를 무엇으로 그릴 것인가 — 유닛 사거리는 채우고, 모닥불·가림막은 각자 색 외곽선만 그리는 갈림에 쓴다.
    public RangeEdgeKind EdgeKind { get; private set; }

    // 아직 들고 있는 범위가 있는가 — 있어야만 비우는 실행이 의미 있다.
    public bool HasRange => tiles.Count > 0 || EdgeKind != RangeEdgeKind.None;

    // 이번에 클릭한 범위와 그 범위를 그릴 방식을 보관한다.
    public void KeepRange(List<Tile> range, RangeEdgeKind edgeKind)
    {
        tiles.Clear();
        tiles.AddRange(range);
        EdgeKind = edgeKind;
        Version++;
    }

    // 보관한 범위를 비운다.
    public void ClearRange()
    {
        tiles.Clear();
        EdgeKind = RangeEdgeKind.None;
        Version++;
    }
}
