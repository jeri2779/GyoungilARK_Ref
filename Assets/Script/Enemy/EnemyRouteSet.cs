using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>저작 경로가 어떤 이동 방식을 위한 것인가.
/// None은 저작 경로를 쓰지 않는다는 뜻이다 — 지상 적은 맵의 레인(EnemyLanes)을 그대로 따른다.</summary>
public enum EnemyRouteKind
{
    None = 0,
    Air  = 1,
    Swim = 2,
}

/// <summary>
/// 공중·수영 적이 따라갈 경로를 사람이 그려 담아 두는 에셋.
///
/// 왜 맵의 RouteConfig에 넣지 않는가 — 맵의 경로는 통행 방식을 타일 속성(PassType)으로만 갈라서
/// 지형을 아예 보지 않는 공중을 담을 자리가 없고, 그 구조를 고치는 건 다른 담당 영역이다.
/// FlyingPathfinder·SwimPathfinder가 맵을 읽기만 하고 자기 길찾기를 따로 둔 것과 같은 선을 긋는다.
///
/// 좌표는 모듈 로컬 0-base다(Tile.State.Col/Row 그대로). 그래서 이 에셋은 모듈 하나에 묶이며,
/// 런타임 조회는 WaveSpawner의 인스펙터 참조로 한다 — 프리팹 신원을 런타임에 되짚을 수 없기 때문이다
/// (PrefabUtility는 에디터 전용). Module 필드는 저작 창이 대상을 확인하는 용도다.
///
/// 같은 (종류, 스폰)에 항목이 여럿이면 갈래로 취급해 스폰할 때 랜덤으로 하나를 고른다.
/// </summary>
[CreateAssetMenu(fileName = "EnemyRouteSet", menuName = "Enemy/Enemy Route Set")]
public class EnemyRouteSet : ScriptableObject
{
    /// <summary>스폰 한 곳에서 본진까지 그린 경로 한 벌.</summary>
    [Serializable]
    public class Entry
    {
        public EnemyRouteKind Kind = EnemyRouteKind.Air;

        [Tooltip("이 경로가 시작하는 스폰 칸(모듈 로컬 좌표)")]
        public Vector2Int Spawn;

        [Tooltip("거쳐 가는 칸. 이웃하지 않은 칸 사이는 런타임에 자동으로 이어진다")]
        public List<Vector2Int> Nodes = new();
    }

    [Tooltip("이 경로들이 붙는 모듈 프리팹. 저작 창이 대상을 확인하는 데만 쓴다(런타임 조회에는 쓰지 않는다).")]
    public GameObject Module;

    public List<Entry> Entries = new();

    /// <summary>이 종류·이 스폰에 그려 둔 경로들을 into에 담는다(갈래). 호출부가 목록을 재사용해 할당을 없앤다.</summary>
    public void Collect(EnemyRouteKind kind, Vector2Int spawn, List<Entry> into)
    {
        into.Clear();
        if (kind == EnemyRouteKind.None) return;

        for (int i = 0; i < Entries.Count; i++)
        {
            Entry entry = Entries[i];
            if (!Usable(entry, kind)) continue;
            if (entry.Spawn != spawn) continue;

            into.Add(entry);
        }
    }

    /// <summary>이 종류·이 스폰에 쓸 수 있는 경로가 하나라도 있는가. 포탈 후보를 좁힐 때 쓴다.</summary>
    public bool Has(EnemyRouteKind kind, Vector2Int spawn)
    {
        if (kind == EnemyRouteKind.None) return false;

        for (int i = 0; i < Entries.Count; i++)
        {
            Entry entry = Entries[i];
            if (!Usable(entry, kind)) continue;
            if (entry.Spawn == spawn) return true;
        }

        return false;
    }

    /// <summary>이 종류에 그려 둔 경로들의 스폰 좌표(중복 제거). 진단 로그가 "어디에 그려져 있는지" 알릴 때 쓴다.</summary>
    public void SpawnsOf(EnemyRouteKind kind, List<Vector2Int> into)
    {
        into.Clear();
        for (int i = 0; i < Entries.Count; i++)
        {
            Entry entry = Entries[i];
            if (!Usable(entry, kind)) continue;
            if (!into.Contains(entry.Spawn)) into.Add(entry.Spawn);
        }
    }

    /// <summary>이 종류에 그려 둔 경로 수. 저작 창의 표기에 쓴다.</summary>
    public int CountOf(EnemyRouteKind kind)
    {
        int count = 0;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Usable(Entries[i], kind)) count++;
        }

        return count;
    }

    // 빈 항목은 없는 것으로 본다 — 저작 중에 만들어 두고 아직 안 그린 항목이 포탈 후보를 늘리면 안 된다.
    private static bool Usable(Entry entry, EnemyRouteKind kind)
    {
        return entry != null
            && entry.Kind == kind
            && entry.Nodes != null
            && entry.Nodes.Count > 0;
    }
}
