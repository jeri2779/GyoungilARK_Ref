using System;
using System.Collections.Generic;
using UnityEngine;

// 낮 동안 바람이 들어오는 외곽 면 전체에 안내 화살표를 표시합니다.
public class WindPreview : IDisposable
{
    private readonly MapBoard board;
    private readonly float arrowSize;
    private readonly float arrowHeight;
    private readonly Color arrowColor;
    private readonly float slideCells;
    private readonly float slidePeriod;
    private readonly Dictionary<Vector2Int, List<Tile>> faceByWind;
    private readonly List<GameObject> arrows = new();
    private readonly List<SpriteRenderer> arrowRenderers = new();
    private readonly List<SpriteRenderer> slideTargets = new();
    private readonly List<Vector3> slideSpots = new();

    private GameObject arrowRoot;
    private Sprite arrowSprite;
    private WindArrowSlide slide;

    public int VisibleCount { get; private set; }
    public int ArrowCount => arrows.Count;

    // 사막 보드의 네 외곽 면과 재사용할 화살표를 한 번 준비합니다.
    public WindPreview(
        MapBoard board,
        Transform parent,
        float size,
        float height,
        Color color,
        float slideCells,
        float slidePeriod)
    {
        this.board = board;
        arrowSize = size;
        arrowHeight = height;
        arrowColor = color;
        this.slideCells = slideCells;
        this.slidePeriod = slidePeriod;
        faceByWind = WindFaceCalc.BuildFaces(board);
        BuildArrows(parent);
    }

    // 현재 바람이 들어오는 외곽 면 전체에 화살표를 표시하고 밀어내기를 시작합니다.
    public void Show(Vector2Int wind)
    {
        if (!faceByWind.TryGetValue(wind, out List<Tile> face))
        {
            throw new ArgumentOutOfRangeException(nameof(wind));
        }

        Hide();
        Vector3 worldDirection = WorldDirectionCalc.ReadDirection(board, wind);
        Quaternion rotation = Quaternion.LookRotation(Vector3.up, worldDirection);
        PlaceArrows(face, rotation);
        slide.PlayArrows(slideTargets, slideSpots, worldDirection);
        VisibleCount = face.Count;
    }

    // 모든 안내 화살표를 숨기고 밀어내기를 멈춥니다.
    public void Hide()
    {
        slide.StopArrows();
        for (int index = 0; index < arrows.Count; index++)
        {
            arrows[index].SetActive(false);
        }

        VisibleCount = 0;
    }

    // 생성한 안내 오브젝트와 공유 이미지를 정리합니다.
    public void Dispose()
    {
        Texture2D arrowTexture = arrowSprite.texture;
        DestroyObject(arrowRoot);
        DestroyObject(arrowSprite);
        DestroyObject(arrowTexture);
        arrows.Clear();
        arrowRenderers.Clear();
        faceByWind.Clear();
        VisibleCount = 0;
    }

    // 면의 타일마다 화살표를 세우고 밀어내기에 넘길 기준 자리를 모읍니다.
    private void PlaceArrows(List<Tile> face, Quaternion rotation)
    {
        slideTargets.Clear();
        slideSpots.Clear();
        for (int index = 0; index < face.Count; index++)
        {
            Vector3 spot = face[index].WorldTop + Vector3.up * arrowHeight;
            arrows[index].transform.SetPositionAndRotation(spot, rotation);
            arrows[index].SetActive(true);
            slideTargets.Add(arrowRenderers[index]);
            slideSpots.Add(spot);
        }
    }

    // 가장 긴 외곽 면 길이만큼 화살표 오브젝트를 만들어 재사용합니다.
    private void BuildArrows(Transform parent)
    {
        arrowRoot = new GameObject("WindPreview");
        arrowRoot.hideFlags = HideFlags.DontSave;
        arrowRoot.transform.SetParent(parent, false);
        arrowSprite = WindArrowSprite.CreateSprite();
        slide = arrowRoot.AddComponent<WindArrowSlide>();
        slide.SetMotion(slideCells * board.CellSize, slidePeriod, arrowColor);

        int arrowCount = ReadMaxCount();
        for (int index = 0; index < arrowCount; index++)
        {
            BuildArrow(index);
        }
    }

    // 재사용할 화살표 하나를 만들어 목록에 담습니다.
    private void BuildArrow(int index)
    {
        GameObject arrow = new GameObject($"WindArrow_{index}");
        arrow.hideFlags = HideFlags.DontSave;
        arrow.transform.SetParent(arrowRoot.transform, false);
        arrow.transform.localScale = Vector3.one * arrowSize;
        SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
        renderer.sprite = arrowSprite;
        renderer.color = arrowColor;
        renderer.sortingOrder = 20;
        arrow.SetActive(false);
        arrows.Add(arrow);
        arrowRenderers.Add(renderer);
    }

    // 네 외곽 면 중 가장 긴 타일 수를 조회합니다.
    private int ReadMaxCount()
    {
        int horizontal = Mathf.Max(
            faceByWind[GridCalculator.Right].Count,
            faceByWind[GridCalculator.Left].Count);
        int vertical = Mathf.Max(
            faceByWind[GridCalculator.Up].Count,
            faceByWind[GridCalculator.Down].Count);
        return Mathf.Max(horizontal, vertical);
    }

    // Play Mode와 Editor 검사 환경에 맞게 생성 오브젝트를 제거합니다.
    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
            return;
        }

        UnityEngine.Object.DestroyImmediate(target);
    }
}
