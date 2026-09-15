using System.Collections.Generic;

// 해금 연출이 함께 쓰는 기믹 칸 목록과 "이미 보여준 지역"을 보관한다.
public class GimmickTileData
{
    private readonly Dictionary<int, List<Tile>> tilesByModule = new();
    private readonly HashSet<int> shownModules = new();

    // 계산기가 구한 그 지역의 기믹 칸 목록을 보관한다.
    public void StoreTiles(int moduleId, List<Tile> tiles)
    {
        tilesByModule[moduleId] = tiles;
    }

    // 보관해 둔 그 지역의 기믹 칸 목록을 꺼낸다.
    public bool TryGetTiles(int moduleId, out List<Tile> tiles)
    {
        return tilesByModule.TryGetValue(moduleId, out tiles);
    }

    // 그 지역의 기믹 안내를 이미 보여줬는지 알려준다.
    public bool WasShown(int moduleId)
    {
        return shownModules.Contains(moduleId);
    }

    // 기믹 안내를 한 번이라도 보여준 지역이 있는지 알려준다.
    public bool HasAnyShown()
    {
        return shownModules.Count > 0;
    }

    // 그 지역의 기믹 안내를 보여줬다고 새긴다.
    public void MarkShown(int moduleId)
    {
        shownModules.Add(moduleId);
    }

    // 세이브에서 읽은 "봤음" 값을 되살린다.
    public void RestoreShown(int moduleId, bool wasShown)
    {
        if (!wasShown)
        {
            return;
        }
        shownModules.Add(moduleId);
    }
}
