using System.Collections.Generic;

public class WindShelterData
{
    private readonly Dictionary<Tile, WindShelter> shelterByTile = new();

    // 이 타일의 바람 막힘 정보를 저장한다.
    public void KeepShelter(Tile tile, WindShelter shelter)
    {
        shelterByTile[tile] = shelter;
    }

    // 이 타일의 바람 막힘 정보를 꺼내온다.
    public WindShelter ReadShelter(Tile tile)
    {
        return shelterByTile[tile];
    }
}
