using System.Collections.Generic;
using UnityEngine;

// 밤마다 계산된 노출 칸에 이펙트를 배치합니다. 판단은 하지 않고 배치만 합니다. 낮엔 전부 치웁니다.
public class DesertLineEffect
{
    private readonly MapBoard board;
    private readonly GameObject windPrefab;
    private readonly List<GameObject> spawned = new();

    public DesertLineEffect(MapBoard board, GameObject windPrefab)
    {
        this.board = board;
        this.windPrefab = windPrefab;
    }

    // 이미 계산된 결과를 그대로 화면에 배치한다.
    public void Show(Vector2Int wind, DesertEffectData data)
    {
        Hide();
        Quaternion rotation = WorldDirectionCalc.ReadRotation(board, wind);
        SpawnAll(data.StrongTiles, windPrefab, rotation);
        SpawnAll(data.WeakTiles, windPrefab, rotation);
    }

    // 배치된 모든 이펙트를 치운다.
    public void Hide()
    {
        for (int index = 0; index < spawned.Count; index++)
        {
            Object.Destroy(spawned[index]);
        }

        spawned.Clear();
    }

    // 타일 목록마다 같은 프리팹으로 이펙트를 심는다.
    private void SpawnAll(IReadOnlyList<Tile> tiles, GameObject prefab, Quaternion rotation)
    {
        for (int index = 0; index < tiles.Count; index++)
        {
            Spawn(prefab, tiles[index], rotation);
        }
    }

    // 실제 타일 위치에 이펙트 하나를 만들어 목록에 보관한다.
    private void Spawn(GameObject prefab, Tile tile, Quaternion rotation)
    {
        GameObject effect = Object.Instantiate(prefab, tile.WorldTop, rotation, board.transform);
        spawned.Add(effect);
    }
}
