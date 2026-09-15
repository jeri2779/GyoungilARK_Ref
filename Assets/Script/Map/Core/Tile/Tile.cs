using System;
using System.Collections.Generic;
using UnityEngine;


//타일은 고유 좌표와 인덱스를 가진다
//타일은 지형·점령·용도 3축으로 상태를 가진다
//타일이 알아야 하는 정보는 해당 타일 위 배치된 오브젝트와 저지되는 적이다.
//타일은 인덱스로 인해 인접한 타일로 접근할수있어야 한다.
[DisallowMultipleComponent]
public partial class Tile : MonoBehaviour
{
    [Tooltip("이 타일의 논리 상태(지형·점령·용도 3축). 인스펙터 또는 베이크(Tools/Map)로 저작.")]
    public TileState State = new();

    [Tooltip("적 스폰 지점 여부(경로 시작). MVP 임시 표식 — 이후 EnemyPath/MapData로 이관 예정.")]
    public bool isEnemySpawn;

    // ---- 런타임 캐시(직렬화하지 않음) ----
    private float _topY;

    /// <summary>이 타일이 속한 모듈 보드(아파트 문패의 동 번호). MapBoard.Build가 새긴다.
    /// 좌표는 모듈 로컬 0-base라 (Col,Row)만으로는 모듈을 특정할 수 없다 — 보드까지 있어야 완전한 주소.</summary>
    public MapBoard Board { get; private set; }

    //타일을 점유한 오브젝트
    public GameObject OccupantObject { get; private set; }

    // 점유 오브젝트의 Hero 컴포넌트 캐시(SetOccupant/ClearOccupant에서만 갱신)
    public Hero OccupantHero { get; private set; }

    // 내 위에 올라온 적
    private readonly List<GameObject> _enemies = new();

    //지금 이 타일 위에 있는 적들(읽기 전용). 없으면 빈 목록.
    public IReadOnlyList<GameObject> Enemies => _enemies;

    /// <summary>이 타일 위에 적이 하나라도 있는가.</summary>
    public bool HasEnemy => _enemies.Count > 0;
    public int EnemyCount => _enemies.Count;

    public Vector2Int Coord => new(State.Col, State.Row);
    public TerrainType Terrain => State.Terrain;
    public bool IsGround => Terrain == TerrainType.Ground;
    public bool IsHigh => Terrain == TerrainType.High;
    public bool IsCore => Terrain == TerrainType.Core;
    public bool IsSpecial => Terrain == TerrainType.Special;
    public bool IsEnemyLane => State.EnemyLane;
    public bool IsEnemySpawn => isEnemySpawn;
    public bool HasUnit => OccupantObject != null;

 
    public GameObject UnitPrefab;
    public OccupantKind UnitKind = OccupantKind.MeleeHero;

    /// <summary>적 통행 가능 지형인가. 고지·빈 타일은 막힘, 지상·본진은 통행(설계: 고지=이동 차단).</summary>
    public bool Walkable => State.Terrain is TerrainType.Ground or TerrainType.Core;

    // 걸어서 오는 상대 기준. 지상이어도 헤엄 칸이면 막힌다.
    public bool CanWalk => CanPass(PassType.Walk);

    // 이 칸에 걸린 기믹. 길찾기·배치와는 무관하고 올라선 유닛에게만 영향을 준다.
    public GimmickType Gimmick => State.Gimmick;

    // 불 칸인가. 지나갈 수도, 아군을 놓을 수도 있다.
    public bool IsFire => Gimmick == GimmickType.Fire;

    // 추위 디버프를 막는 고정 모닥불 칸인가.
    public bool IsCampfire => Gimmick == GimmickType.Campfire;

    // 바람을 막는 언덕 위 가림막 칸인가.
    public bool IsWindwall => Gimmick == GimmickType.Windwall;

    // 이 칸을 지나갈 수 있는지.(현재는 물적도 일반 땅 밟을 수 있게 오픈된 형태)
    public bool CanPass(PassType way)
    {
        return Walkable && (State.Pass == PassType.Walk || way == PassType.Swim);
    }

    // 이웃 타일(런타임 캐시, 직렬화하지 않음)
    private Tile[] neighborTiles = Array.Empty<Tile>();

    // 상하좌우에 실제로 있는 타일들. 읽기 전용이라 밖에서 못 바꾼다.
    public ReadOnlySpan<Tile> NeighborTiles => neighborTiles;

    // TileLink가 이어 준 이웃을 새긴다. 저작 도구가 못 건드리게 같은 어셈블리 안에서만 연다.
    internal void SetNeighbors(Tile[] tiles)
    {
        neighborTiles = tiles;
    }

    public Vector3 WorldTop => new(transform.position.x, _topY, transform.position.z);

    /// <summary>MapBoard가 스캔 시 윗면 높이를 캐시해 준다(WorldTop·배치·경로 기준).</summary>
    public void SetTop(float topY)
    {
        _topY = topY;
    }

    /// <summary>MapBoard.Build가 스캔 시 자기 자신을 새긴다(소유 보드 도장).</summary>
    public void SetBoard(MapBoard board)
    {
        Board = board;
    }

    //비어 있는 타일에 유닛을 배치(런타임 인스턴스를 기록). UnitPrefab은 인스펙터 저작값이라 건드리지 않는다.
    public void SetOccupant(GameObject go, OccupantKind kind)
    {
        OccupantObject = go;
        State.Occupant = kind;
        OccupantHero = go.GetComponent<Hero>();
    }

    //배치되어 있는 유닛을 해제 후 반환.
    public GameObject ClearOccupant()
    {
        GameObject go = OccupantObject;

        OccupantObject = null;
        State.Occupant = OccupantKind.None;
        OccupantHero = null;

        return go;
    }

    //이 적이 몇 번째로 들어왔는지. 없으면 -1.
    public int EnemyEnterIndex(GameObject enemy)
    {
        return _enemies.IndexOf(enemy);
    }

    //타일에 적 진입 등록
    public void AddEnemy(GameObject enemy)
    {
        if (_enemies.Contains(enemy))
        {
            return;
        }

        _enemies.Add(enemy);
        FireReceiver.ReceiveEntry(this, enemy.transform);
    }

    //타일에 적 이탈 등록
    public void RemoveEnemy(GameObject enemy)
    {
        if (!_enemies.Remove(enemy))
        {
            return;
        }

        FireReceiver.ReceiveExit(this, enemy.transform);
    }


}
    
