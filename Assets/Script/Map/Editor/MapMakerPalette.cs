using UnityEngine;

/// <summary>
/// 맵 메이커 창이 쓰는 색을 정한다.
///
/// 값은 어두운 에디터 스킨(#383838) 위에서 서로 구분되도록 고른 것이다 —
/// EditorGUI.DrawRect는 여기 적은 값을 그대로 화면에 쓰므로(감마 보정이 끼지 않는다)
/// 배경이나 다른 색과 가까운 값을 넣으면 그 자리에서 바로 구분이 안 된다.
///
/// 지형(큰 분류)은 바탕색으로, 배치 허용(작은 분류)은 칸 안의 점으로 나눈다.
/// </summary>
public static class MapMakerPalette
{
    /// <summary>어두운 에디터 스킨 배경. 고르지 않은 칸을 이 쪽으로 섞어 죽인다.</summary>
    public static readonly Color Panel = new(0.220f, 0.220f, 0.220f);

    public static readonly Color Ground = new(0.290f, 0.478f, 0.322f);
    public static readonly Color High = new(0.561f, 0.259f, 0.196f);
    public static readonly Color Border = new(0.353f, 0.290f, 0.431f);
    public static readonly Color Core = new(0.184f, 0.373f, 0.620f);
    public static readonly Color Nothing = new(0.420f, 0.420f, 0.420f);

    /// <summary>고지 칸 위쪽에 얹는 밝은 띠 — 한 단 올라와 있다는 표시.</summary>
    public static readonly Color HighTop = new(0.851f, 0.545f, 0.416f);

    /// <summary>본진 칸 가운데 표식.</summary>
    public static readonly Color CoreMark = new(0.737f, 0.851f, 1.000f);

    /// <summary>적 스폰 칸 테두리.</summary>
    public static readonly Color Spawn = new(0.910f, 0.753f, 0.290f);

    /// <summary>지금 고른 배치 허용이 켜져 있는 칸의 점.</summary>
    public static readonly Color Mark = new(0.918f, 0.918f, 0.918f);

    public static readonly Color PathEdge = new(0.000f, 0.000f, 0.000f, 0.55f);
    public static readonly Color Problem = new(0.910f, 0.259f, 0.369f);

    /// <summary>
    /// 경로마다 다른 선 색. 전부 흰 선이면 겹치는 구간에서 어느 스폰의 길인지 갈리지 않는다.
    /// 지형(초록·주황·보라)과 표식(금·빨강·청록·연보라) 위에서 각각 읽히는 값으로 골랐다.
    /// </summary>
    private static readonly Color[] Lanes =
    {
        new(0.361f, 0.784f, 1.000f),   // 하늘
        new(1.000f, 0.565f, 0.216f),   // 주황
        new(0.988f, 0.451f, 0.769f),   // 분홍
        new(0.561f, 0.945f, 0.435f),   // 연두
        new(1.000f, 0.898f, 0.404f),   // 노랑
        new(0.376f, 0.941f, 0.808f),   // 청록
        new(0.769f, 0.639f, 1.000f),   // 라벤더
        new(1.000f, 0.435f, 0.400f)    // 산호
    };

    /// <summary>이 번호 경로의 선 색. 경로가 색 수보다 많으면 앞에서부터 다시 쓴다.</summary>
    public static Color Lane(int index)
    {
        return Lanes[Mathf.Abs(index) % Lanes.Length];
    }

    /// <summary>프리팹과 다른(씬 오버라이드) 칸 표식 — 청록. 문제(빨강)·스폰(금)과 겹치지 않게 골랐다.</summary>
    public static readonly Color Override = new(0.216f, 0.804f, 0.831f);

    /// <summary>고른 경로에 사람이 그린 칸 표식 — 보라. 스폰(금)·문제(빨강)와 갈린다.</summary>
    public static readonly Color Node = new(0.706f, 0.510f, 0.933f);

    /// <summary>배치 허용이 켜졌지만 효과 없는(무효 조합) 칸 표식 — 주황.</summary>
    public static readonly Color Inert = new(0.960f, 0.510f, 0.129f);

    /// <summary>장식 붓 표식 — 지형이 아니라 타일 위에 얹는 것이라 지형색과 겹치지 않는 연두로 둔다.</summary>
    public static readonly Color Decor = new(0.580f, 0.859f, 0.420f);

    /// <summary>헤엄 칸 표식 — 지형색은 지상과 같으므로 이 색 띠로만 갈린다.</summary>
    public static readonly Color Swim = new(0.361f, 0.620f, 0.980f);

    /// <summary>불 칸 표식 — 헤엄과 같은 이유로 띠로만 갈리며, 물과 정반대 색으로 둔다.</summary>
    public static readonly Color Fire = new(0.949f, 0.412f, 0.188f);

    /// <summary>모닥불 칸 표식 — 불의 주황색과 구분되는 따뜻한 노란색.</summary>
    public static readonly Color Campfire = new(1.000f, 0.765f, 0.235f);

    /// <summary>가림막 칸 표식 — 밝은 자홍색, 초록·갈색·보라 지형 위에서도 확실히 튄다.</summary>
    public static readonly Color Windwall = new(1.000f, 0.259f, 0.702f);

    /// <summary>칸 글자(P·G·H·B) — 지형색 위에서 읽히도록 거의 흰색으로 둔다.</summary>
    public static readonly Color Letter = new(0.945f, 0.945f, 0.945f);

    /// <summary>지형별 바탕색.</summary>
    public static Color Terrain(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Ground: return Ground;
            case TerrainType.High: return High;
            case TerrainType.Core: return Core;
            case TerrainType.Special: return Border;
            default: return Nothing;
        }
    }

    /// <summary>지금 고른 붓과 상관없는 칸을 배경 쪽으로 죽인다 — 무슨 모드인지 한눈에 보이게 하는 장치다.</summary>
    public static Color Dim(Color color)
    {
        return Color.Lerp(color, Panel, 0.68f);
    }
}
