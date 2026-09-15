public readonly struct PlaceEdgeData
{
    public readonly HoverMode Mode;
    public readonly OccupantKind Kind;

    // 배치 모드와 유닛 종류를 보관합니다.
    public PlaceEdgeData(HoverMode mode, OccupantKind kind)
    {
        Mode = mode;
        Kind = kind;
    }

    // 두 외곽선 상태가 같은지 반환합니다.
    public bool SameAs(PlaceEdgeData other)
    {
        return Mode == other.Mode && Kind == other.Kind;
    }
}
