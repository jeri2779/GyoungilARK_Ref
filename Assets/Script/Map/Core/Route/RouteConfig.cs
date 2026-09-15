using System.Collections.Generic;
using UnityEngine;

// 이 모듈에 지정된 경로 목록을 보관하고 스폰 좌표로 조회합니다. 한 스폰에 여러 벌을 담습니다.
// 조회는 지금 선택된 일차 기준으로 걸러지고, 그 날짜 전용이 없으면 공통(0)을 대신 냅니다.
[DisallowMultipleComponent]
public sealed class RouteConfig : MonoBehaviour
{
    private const int CommonDay = 0;

    [SerializeField] private List<RouteData> routes = new();

    private readonly Dictionary<Vector2Int, Dictionary<int, List<RouteData>>> routeMap = new();
    private readonly List<int> usedDays = new();
    private int selectedDay = CommonDay;

    // 게임이 시작될 때 저장된 목록으로 조회용 사전을 한 번 세운다.
    private void Awake()
    {
        Rebuild();
    }

    // 인스펙터에서 값이 바뀌어도 조회용 사전이 낡지 않게 다시 세운다.
    private void OnValidate()
    {
        Rebuild();
    }

    // 지금부터 이 일차 기준으로 조회한다. 창의 날짜 버튼이 부른다.
    public void SetActiveDay(int day)
    {
        selectedDay = day;
    }

    // 지금 선택된 일차. "+갈래"가 새 경로를 이 날짜로 붙일 때 쓴다.
    public int SelectedDay => selectedDay;

    // 이 모듈에서 실제로 쓰인 날짜들(공통 제외, 오름차순). 날짜 탭을 그릴 때 쓴다.
    public IReadOnlyList<int> UsedDays => usedDays;

    // 지금 선택된 일차 "전용" 경로가 이 스폰에 있는지. 공통으로 대신 찾지 않는다 —
    // 저작을 새로 시작할지 판단하는 자리라 대체가 섞이면 공통 경로를 잘못 붙잡는다.
    public bool TryGetOwnRoute(Vector2Int spawn, out List<RouteData> found)
    {
        if (routeMap.TryGetValue(spawn, out Dictionary<int, List<RouteData>> byDay))
        {
            return byDay.TryGetValue(selectedDay, out found);
        }

        found = null;
        return false;
    }

    // 이 스폰에서, 지금 선택된 일차에 해당하는 경로들을 찾습니다. 없으면 false.
    public bool TryGetRoutes(Vector2Int spawn, out List<RouteData> found)
    {
        if (routeMap.TryGetValue(spawn, out Dictionary<int, List<RouteData>> byDay))
        {
            return ByDayOrCommon(byDay, out found);
        }

        found = null;
        return false;
    }

    // 선택된 일차 것을 먼저 찾고, 없으면 공통(0)을 대신 냅니다.
    private bool ByDayOrCommon(Dictionary<int, List<RouteData>> byDay, out List<RouteData> found)
    {
        if (byDay.TryGetValue(selectedDay, out found))
        {
            return true;
        }

        return byDay.TryGetValue(CommonDay, out found);
    }

    // 이 경로가 목록 몇 번째인지. 맵 메이커가 고른 경로를 고칠 때 쓰는 통로다.
    public int IndexOf(RouteData route)
    {
        return routes.IndexOf(route);
    }

    // 이 번호의 경로. 방금 만든 항목을 되받을 때 쓴다.
    public RouteData RouteAt(int index)
    {
        return routes[index];
    }

    // 지정 목록을 스폰·일차 사전으로 정리합니다. 맵 메이커가 목록을 고친 뒤에도 부릅니다.
    public void Rebuild()
    {
        routeMap.Clear();
        usedDays.Clear();

        for (int index = 0; index < routes.Count; index++)
        {
            AddToMap(routes[index]);
        }

        usedDays.Sort();
    }

    // 경로 하나를 그 스폰의 서랍(날짜별 사전)에 끼워 넣는다.
    private void AddToMap(RouteData route)
    {
        if (routeMap.TryGetValue(route.Spawn, out Dictionary<int, List<RouteData>> byDay))
        {
            AddToDay(byDay, route);
        }
        else
        {
            var created = new Dictionary<int, List<RouteData>>();
            AddToDay(created, route);
            routeMap[route.Spawn] = created;
        }

        AddUsedDay(route.Day);
    }

    // 처음 보는 날짜면(공통 제외) 목록에 추가한다.
    private void AddUsedDay(int day)
    {
        if (day == CommonDay)
        {
            return;
        }

        if (usedDays.Contains(day))
        {
            return;
        }

        usedDays.Add(day);
    }

    // 그 서랍 안에서도 이 경로의 날짜 칸을 찾아 쌓는다.
    private static void AddToDay(Dictionary<int, List<RouteData>> byDay, RouteData route)
    {
        if (byDay.TryGetValue(route.Day, out List<RouteData> found))
        {
            found.Add(route);
            return;
        }

        byDay[route.Day] = new List<RouteData> { route };
    }
}
