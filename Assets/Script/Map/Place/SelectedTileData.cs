// 선택된 타일 하나를 보관한다.
public class SelectedTileData
{
    private Tile _selected;

    public Tile Selected
    {
        get { return _selected; }
    }

    public void Select(Tile tile)
    {
        _selected = tile;
    }

    public void Clear()
    {
        _selected = null;
    }

    public bool IsSelected(Tile tile)
    {
        return _selected == tile;
    }
}
