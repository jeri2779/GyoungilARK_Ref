using System.Collections.Generic;
using UnityEngine;

// 경로 종류별 색상을 한 곳에 저장하는 데이터 자산. 여러 모듈이 이 자산 하나를 같이 참조한다.
[CreateAssetMenu(menuName = "Map/Trail Palette")]
public class TrailPalette : ScriptableObject
{
    [SerializeField] private Gradient groundGradient; //지상 경로 전용 색.
    [SerializeField] private Gradient airGradient; //공중 경로 전용 색.
    [SerializeField] private Gradient swimGradient; //수영 경로 전용 색.

    private Dictionary<TrailKind, Gradient> table; //종류별 색 조회용 표. 분기문 대신 이 표 하나로 찾는다.

    // 종류별 색 조회 표를 구성합니다.
    private void OnEnable()
    {
        table = new Dictionary<TrailKind, Gradient>
        {
            { TrailKind.Ground, groundGradient },
            { TrailKind.Air, airGradient },
            { TrailKind.Swim, swimGradient },
        };
    }

    // 이 종류에 맞는 색을 표에서 찾아 돌려줍니다.
    public Gradient GradientOf(TrailKind kind) => table[kind];
}
