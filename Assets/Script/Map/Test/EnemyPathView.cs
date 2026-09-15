using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemyPathView : MonoBehaviour
{
    [FormerlySerializedAs("routeMap")]
    [SerializeField] private EnemyLanes enemyLanes;
    [SerializeField] private bool pathVisible = true;

    private readonly List<Tile> pathTiles = new();
    private readonly HashSet<Vector2Int> pathCoords = new();

    public bool PathVisible => pathVisible;
    public bool HasPath => pathTiles.Count > 0;
    public int TileCount => pathTiles.Count;
    public IReadOnlyList<Tile> PathTiles => pathTiles;

    public event Action OnPathChanged;

    // 같은 GameObject의 EnemyLanes를 인스펙터 참조에 자동 할당합니다.
    private void Reset()
    {
        enemyLanes = GetComponent<EnemyLanes>();
    }

    // 레인 변경 시 표시용 경로 목록을 다시 만들도록 이벤트를 구독합니다.
    private void OnEnable()
    {
        Prepare();
        if (enemyLanes != null)
        {
            enemyLanes.Changed += RebuildPath;
        }
    }

    // 현재 레인을 기준으로 최초 표시용 경로 목록을 만듭니다.
    private void Start()
    {
        RebuildPath();
    }

    // 레인 변경 이벤트 구독을 해제합니다.
    private void OnDisable()
    {
        if (enemyLanes != null)
        {
            enemyLanes.Changed -= RebuildPath;
        }
    }

    // 지정 좌표가 표시용 적 이동 경로에 포함되는지 확인합니다.
    public bool IsOnPath(Vector2Int coord)
    {
        return pathCoords.Contains(coord);
    }

    // 모든 유효 레인을 합쳐 중복 없는 표시용 경로 목록을 다시 만듭니다.
    public void RebuildPath()
    {
        pathTiles.Clear();
        pathCoords.Clear();
        Prepare();

        if (enemyLanes == null)
        {
            OnPathChanged?.Invoke();
            return;
        }

        AddLanes(enemyLanes.Lanes);
        OnPathChanged?.Invoke();
    }

    // 경로 표시 여부를 전환하고 변경 이벤트를 알립니다.
    public void ToggleVisibility()
    {
        pathVisible = !pathVisible;
        OnPathChanged?.Invoke();
    }

    // EnemyLanes 참조가 없으면 같은 GameObject에서 가져옵니다.
    private void Prepare()
    {
        if (enemyLanes == null)
        {
            enemyLanes = GetComponent<EnemyLanes>();
        }
    }

    // 전달된 모든 레인을 표시용 경로 목록에 합칩니다.
    private void AddLanes(IReadOnlyList<LaneData> lanes)
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            AddLane(lanes[i]);
        }
    }

    // 유효한 레인에서 스폰과 코어를 제외한 중복 없는 타일만 추가합니다.
    private void AddLane(LaneData lane)
    {
        if (!lane.IsValid)
        {
            return;
        }

        for (int i = 0; i < lane.Tiles.Count; i++)
        {
            Tile tile = lane.Tiles[i];
            bool endpoint = tile.IsEnemySpawn || tile.IsCore;
            if (endpoint || !pathCoords.Add(tile.Coord))
            {
                continue;
            }

            pathTiles.Add(tile);
        }
    }
}
