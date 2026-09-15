using System;
using UnityEngine;

/// <summary>
/// 한 테마(개척지·광산 …)가 쓰는 타일 프리팹 묶음. 에디터 저작 전용 설정이다.
///
/// 붓마다 프리팹을 여러 개 둔다 — 같은 지상이라도 찍을 변형이 여럿인데 하나뿐이면
/// 변형을 쓰려고 매번 슬롯을 갈아끼우고 창으로 돌아와야 한다.
///
/// 모듈은 이쪽에서 참조한다. 모듈 프리팹은 런타임 어셈블리라 에디터 전용인 이 에셋을
/// 가리킬 수 없어서, 테마가 "내가 기본인 모듈"을 들고 창이 그것을 읽어 탭을 미리 골라 준다.
/// 이름이 아니라 에셋 참조라 모듈 이름이나 폴더가 바뀌어도 안 깨진다.
/// </summary>
[CreateAssetMenu(fileName = "TileTheme", menuName = "Map/Tile Theme")]
public class TileTheme : ScriptableObject
{
    /// <summary>한 붓이 찍을 수 있는 프리팹들.</summary>
    [Serializable]
    public class Slot
    {
        public MapBrush Brush;
        public GameObject[] Prefabs = Array.Empty<GameObject>();
    }

    [Tooltip("탭에 뜨는 이름. 비우면 에셋 이름을 쓴다")]
    public string Label;

    [Tooltip("이 테마를 기본으로 쓰는 모듈 프리팹")]
    public GameObject[] Modules = Array.Empty<GameObject>();

    [Tooltip("붓별 프리팹. 지형 붓과 장식 붓만 뜻이 있다")]
    public Slot[] Slots = Array.Empty<Slot>();

    [Tooltip("모닥불 기믹 칸을 찍고 끌 때 자동으로 얹고 걷는 장식 프리팹(FireTorch_CampFire 등)")]
    public GameObject CampfirePrefab;

    [Tooltip("가림막 기믹 칸을 찍고 끌 때 자동으로 얹고 걷는 장식 프리팹(WIndWall 등)")]
    public GameObject WindwallPrefab;

    /// <summary>탭에 뜨는 이름.</summary>
    public string Title
    {
        get { return string.IsNullOrEmpty(Label) ? name : Label; }
    }

    /// <summary>프리팹을 가질 수 있는 붓들. 창의 목록 순서와 에셋 슬롯 순서를 같게 맞춘다.</summary>
    public static MapBrush[] Painters()
    {
        return new[]
        {
            MapBrush.Ground,
            MapBrush.High,
            MapBrush.Special,
            MapBrush.Core,
            MapBrush.Empty,
            MapBrush.Decor
        };
    }

    /// <summary>이 붓이 찍을 프리팹들. 슬롯이 없으면 빈 배열(교체는 하지 않고 이유를 알린다).</summary>
    public GameObject[] For(MapBrush brush)
    {
        foreach (Slot slot in Slots)
        {
            if (slot.Brush == brush)
            {
                return slot.Prefabs ?? Array.Empty<GameObject>();
            }
        }

        return Array.Empty<GameObject>();
    }

    /// <summary>이 붓의 첫 프리팹. 빈 칸은 건너뛴다(인스펙터에서 슬롯을 비워 둘 수 있다).</summary>
    public GameObject First(MapBrush brush)
    {
        foreach (GameObject prefab in For(brush))
        {
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    /// <summary>이 프리팹이 이 붓의 목록에 있는가. 창이 고른 프리팹이 아직 유효한지 볼 때 쓴다.</summary>
    public bool Has(MapBrush brush, GameObject prefab)
    {
        foreach (GameObject listed in For(brush))
        {
            if (listed == prefab)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>이 모듈 프리팹이 이 테마를 기본으로 쓰는가.</summary>
    public bool Owns(GameObject modulePrefab)
    {
        if (modulePrefab == null)
        {
            return false;
        }

        foreach (GameObject module in Modules)
        {
            if (module == modulePrefab)
            {
                return true;
            }
        }

        return false;
    }
}
