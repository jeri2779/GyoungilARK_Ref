using UnityEngine;

/// <summary>
/// 타일이 어느 구역에 속하는지, 격자에 어떤 글자로 보일지 정한다.
///
/// 구역은 타일이 매달린 Tilemap 그룹 이름(Production·Combat·Boundary)에서 읽는다 —
/// 이미 씬에 있는 분류인데 인스펙터로는 눈에 안 들어오는 정보다.
///
/// 글자는 지형만으로는 모자라다. 생산 바닥과 전투 지상은 둘 다 Ground라
/// 색만으로는 어느 구역인지 구분되지 않는다 — 구역까지 봐야 P와 G가 갈린다.
/// </summary>
public enum MapZone
{
    Unknown,
    Production,
    Combat,
    Boundary
}

public static class TileZone
{
    public static MapZone Of(Tile tile)
    {
        Transform parent = tile.transform.parent;
        if (parent == null)
        {
            return MapZone.Unknown;
        }

        switch (parent.name)
        {
            case "Production": return MapZone.Production;
            case "Combat": return MapZone.Combat;
            case "Boundary": return MapZone.Boundary;
            default: return MapZone.Unknown;
        }
    }

    public static string Word(MapZone zone)
    {
        switch (zone)
        {
            case MapZone.Production: return "생산";
            case MapZone.Combat: return "전투";
            case MapZone.Boundary: return "외곽";
            default: return "구역 밖";
        }
    }

    /// <summary>격자 한 칸에 찍을 글자. 색만으로 못 가리는 구분을 글자가 대신한다.</summary>
    public static string Letter(Tile tile)
    {
        switch (tile.Terrain)
        {
            case TerrainType.High: return "H";
            case TerrainType.Core: return "C";
            case TerrainType.Special: return "B";
            case TerrainType.Empty: return "-";
            default: return Of(tile) == MapZone.Production ? "P" : "G";
        }
    }
}
