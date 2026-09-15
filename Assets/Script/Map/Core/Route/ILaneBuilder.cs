using System.Collections.Generic;
using UnityEngine;

public interface ILaneBuilder
{
    // 맵 입력을 기준으로 각 적 스폰 지점의 이동 레인을 계산합니다. coreDistance는 생략하면 내부에서 계산합니다.
    IReadOnlyList<LaneData> BuildLanes(IReadOnlyDictionary<Vector2Int, Tile> cells, IReadOnlyList<Tile> spawns, IReadOnlyList<Tile> cores, IReadOnlyDictionary<Tile, int> coreDistance = null);
}
