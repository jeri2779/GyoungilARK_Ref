using UnityEngine;

// 바람 방향은 하루가 바뀔 때마다 DesertZoneEffect가 재배정한다(고정값 아님).
public class DesertZone : MonoBehaviour
{
    [SerializeField] private DebuffSO[] debuffs;

    [SerializeField] private Vector2Int windDirection = GridCalculator.Right;

    [Tooltip("언덕 가림막이 바람 불어가는 쪽으로 몇 칸을 막아주는지.")]
    [SerializeField, Range(2, 4)] private int windwallReach = 2;

    [SerializeField, Min(0.1f)] private float arrowSize = 0.75f;
    [SerializeField, Min(0f)] private float arrowHeight = 0.08f;
    [SerializeField] private Color arrowColor = Color.yellow;

    [Tooltip("낮 화살표가 바람 방향으로 밀려나는 거리(칸).")]
    [SerializeField, Range(1f, 2f)] private float arrowSlideCells = 1.5f;

    [Tooltip("낮 화살표가 한 번 밀려나갔다 돌아오는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.2f)] private float arrowSlidePeriod = 1.5f;

    private WindwallData windwallData;

    public DebuffSO[] Debuffs => debuffs;
    public Vector2Int WindDirection => windDirection;
    public int WindwallReach => windwallReach;
    public float ArrowSize => arrowSize;
    public float ArrowHeight => arrowHeight;
    public Color ArrowColor => arrowColor;
    public float ArrowSlideCells => arrowSlideCells;
    public float ArrowSlidePeriod => arrowSlidePeriod;
    public WindwallData WindwallData => windwallData;

    // 바람 방향을 새 값으로 바꿔 저장한다.
    public void SetWindDirection(Vector2Int direction)
    {
        windDirection = direction;
    }

    // 완성된 가림막 범위 데이터를 이 사막 지대에 연결한다.
    public void SetWindwall(WindwallData data)
    {
        windwallData = data;
    }
}
