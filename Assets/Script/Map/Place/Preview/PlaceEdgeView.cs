using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PlaceEdgeView : IDisposable
{
    private readonly Transform root;
    private readonly Material edgeMat;
    private readonly float edgeWidth;
    private readonly float edgeLift;
    private readonly List<Vector3> vertices = new();
    private readonly List<int> indices = new();
    private readonly List<MapBoard> boards = new();

    private GameObject edgeObject;
    private Mesh edgeMesh;
    private PlaceEdgeData lastData;
    private bool hasData;

    // 외곽선 출력에 필요한 표시 설정을 보관합니다.
    public PlaceEdgeView(Transform root, Material edgeMat, float edgeWidth, float edgeLift)
    {
        this.root = root;
        this.edgeMat = edgeMat;
        this.edgeWidth = edgeWidth;
        this.edgeLift = edgeLift;
    }

    // 외곽선을 계산할 맵 보드 목록을 보관합니다.
    public void Setup(List<MapBoard> source)
    {
        boards.Clear();
        boards.AddRange(source);
    }

    // 선택된 유닛 종류에 맞는 배치 지형 외곽선을 표시합니다.
    public void Show(PlaceEdgeData data)
    {
        if (IsSame(data))
        {
            return;
        }

        lastData = data;
        hasData = true;
        Rebuild(data);
    }

    // 생성한 외곽선 메시와 오브젝트를 제거합니다.
    public void Dispose()
    {
        if (edgeMesh != null)
        {
            UnityEngine.Object.Destroy(edgeMesh);
        }

        if (edgeObject != null)
        {
            UnityEngine.Object.Destroy(edgeObject);
        }
    }

    // 이전 외곽선 상태와 같은지 반환합니다.
    private bool IsSame(PlaceEdgeData data)
    {
        return hasData && data.SameAs(lastData);
    }

    // 현재 선택 상태에 맞춰 외곽선 메시를 다시 만듭니다.
    private void Rebuild(PlaceEdgeData data)
    {
        EnsureOutput();
        vertices.Clear();
        indices.Clear();

        if (CanShow(data))
        {
            AddBoards(data.Kind);
        }

        ApplyMesh();
    }

    // 배치 중인 영웅의 외곽선을 표시할 수 있는지 반환합니다.
    private bool CanShow(PlaceEdgeData data)
    {
        bool placing = data.Mode == HoverMode.Placing || data.Mode == HoverMode.Held;
        bool hero = data.Kind == OccupantKind.MeleeHero || data.Kind == OccupantKind.RangedHero;
        return placing && hero && edgeMat != null;
    }

    // 모든 해금 보드에서 배치 가능 지형의 경계 변을 추가합니다.
    private void AddBoards(OccupantKind kind)
    {
        for (int boardIndex = 0; boardIndex < boards.Count; boardIndex++)
        {
            MapBoard board = boards[boardIndex];
            if (board.IsUnlocked)
            {
                AddBoard(board, kind);
            }
        }
    }

    // 한 보드의 배치 가능 타일 경계 변을 추가합니다.
    private void AddBoard(MapBoard board, OccupantKind kind)
    {
        IReadOnlyList<Tile> tiles = board.CellList;
        for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
        {
            Tile tile = tiles[tileIndex];
            if (CanPlace(tile, kind))
            {
                AddTile(board, tile, kind);
            }
        }
    }

    // 배치 가능한 타일의 노출된 네 변을 추가합니다.
    private void AddTile(MapBoard board, Tile tile, OccupantKind kind)
    {
        Vector2Int cell = tile.Coord;
        float inset = edgeWidth / board.CellSize;

        AddLeft(board, tile, kind, cell, inset);
        AddRight(board, tile, kind, cell, inset);
        AddBottom(board, tile, kind, cell, inset);
        AddTop(board, tile, kind, cell, inset);
    }

    // 왼쪽 이웃이 배치 불가일 때 지상 또는 고지 높이에 선을 추가합니다.
    private void AddLeft(MapBoard board, Tile tile, OccupantKind kind, Vector2Int cell, float inset)
    {
        if (HasPlaceable(board, cell + Vector2Int.left, kind))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x, cell.y), new Vector2(cell.x, cell.y + 1f), new Vector2(cell.x + inset, cell.y), new Vector2(cell.x + inset, cell.y + 1f));
    }

    // 오른쪽 이웃이 배치 불가일 때 지상 또는 고지 높이에 선을 추가합니다.
    private void AddRight(MapBoard board, Tile tile, OccupantKind kind, Vector2Int cell, float inset)
    {
        if (HasPlaceable(board, cell + Vector2Int.right, kind))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x + 1f, cell.y + 1f), new Vector2(cell.x + 1f, cell.y), new Vector2(cell.x + 1f - inset, cell.y + 1f), new Vector2(cell.x + 1f - inset, cell.y));
    }

    // 아래 이웃이 배치 불가일 때 지상 또는 고지 높이에 선을 추가합니다.
    private void AddBottom(MapBoard board, Tile tile, OccupantKind kind, Vector2Int cell, float inset)
    {
        if (HasPlaceable(board, cell + Vector2Int.down, kind))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x + 1f, cell.y), new Vector2(cell.x, cell.y), new Vector2(cell.x + 1f, cell.y + inset), new Vector2(cell.x, cell.y + inset));
    }

    // 위 이웃이 배치 불가일 때 지상 또는 고지 높이에 선을 추가합니다.
    private void AddTop(MapBoard board, Tile tile, OccupantKind kind, Vector2Int cell, float inset)
    {
        if (HasPlaceable(board, cell + Vector2Int.up, kind))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x, cell.y + 1f), new Vector2(cell.x + 1f, cell.y + 1f), new Vector2(cell.x, cell.y + 1f - inset), new Vector2(cell.x + 1f, cell.y + 1f - inset));
    }

    // 지정 좌표의 타일이 같은 유닛을 배치할 수 있는지 반환합니다.
    private static bool HasPlaceable(MapBoard board, Vector2Int cell, OccupantKind kind)
    {
        if (board.TryGetCell(cell, out Tile tile))
        {
            return CanPlace(tile, kind);
        }

        return false;
    }

    // 타일이 지정 유닛의 배치 가능 지형인지 반환합니다.
    private static bool CanPlace(Tile tile, OccupantKind kind)
    {
        return TilePlacementRule.CanPlace(tile.State, kind);
    }

    // 타일 윗면 높이에 경계선 사각형을 추가합니다.
    private void AddQuad(MapBoard board, Tile tile, Vector2 outerA, Vector2 outerB, Vector2 innerA, Vector2 innerB)
    {
        float height = tile.WorldTop.y + edgeLift;
        int start = vertices.Count;

        vertices.Add(LocalPoint(board, outerA, height));
        vertices.Add(LocalPoint(board, outerB, height));
        vertices.Add(LocalPoint(board, innerA, height));
        vertices.Add(LocalPoint(board, innerB, height));

        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start + 2);
        indices.Add(start + 1);
        indices.Add(start + 3);
    }

    // 셀 좌표를 외곽선 루트 기준 좌표로 변환합니다.
    private Vector3 LocalPoint(MapBoard board, Vector2 point, float height)
    {
        Vector3 world = board.CellPointToWorld(point);
        world.y = height;
        return root.InverseTransformPoint(world);
    }

    // 외곽선 출력 오브젝트와 재사용 메시를 준비합니다.
    private void EnsureOutput()
    {
        if (edgeObject != null)
        {
            return;
        }

        edgeObject = new GameObject("PlaceEdge");
        edgeObject.transform.SetParent(root, false);
        edgeMesh = new Mesh { name = "PlaceEdgeMesh" };
        edgeMesh.MarkDynamic();

        edgeObject.AddComponent<MeshFilter>().sharedMesh = edgeMesh;
        MeshRenderer renderer = edgeObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = edgeMat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // 계산한 정점과 인덱스를 재사용 메시로 출력합니다.
    private void ApplyMesh()
    {
        edgeMesh.Clear();
        edgeMesh.SetVertices(vertices);
        edgeMesh.SetTriangles(indices, 0);
        edgeMesh.RecalculateBounds();
        edgeObject.SetActive(vertices.Count > 0);
    }
}
