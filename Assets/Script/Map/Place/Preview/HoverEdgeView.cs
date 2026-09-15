using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 커서 아래 타일 딱 한 칸의 둘레에 얇은 테두리를 그린다. 언제나 한 칸뿐이라 이웃 판정 없이 네 변을 그대로 그린다.
public sealed class HoverEdgeView : IDisposable
{
    private readonly Transform root;
    private readonly Material edgeMat;
    private readonly float edgeWidth;
    private readonly float edgeLift;
    private readonly List<Vector3> vertices = new();
    private readonly List<int> indices = new();

    private GameObject edgeObject;
    private Mesh edgeMesh;
    private Tile lastTile;
    private bool hasTile;

    // 외곽선 출력에 필요한 표시 설정을 보관합니다.
    public HoverEdgeView(Transform root, Material edgeMat, float edgeWidth, float edgeLift)
    {
        this.root = root;
        this.edgeMat = edgeMat;
        this.edgeWidth = edgeWidth;
        this.edgeLift = edgeLift;
    }

    // 커서가 가리키는 타일에 맞춰 테두리를 표시합니다. 없으면 감춥니다.
    public void Show(Tile tile)
    {
        if (IsSame(tile))
        {
            return;
        }

        lastTile = tile;
        hasTile = true;
        Rebuild(tile);
    }

    // 생성한 테두리 메시와 오브젝트를 제거합니다.
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

    // 이전에 표시한 타일과 같은지 반환합니다.
    private bool IsSame(Tile tile)
    {
        return hasTile && tile == lastTile;
    }

    // 넘겨받은 타일에 맞춰 테두리 메시를 다시 만듭니다.
    private void Rebuild(Tile tile)
    {
        EnsureOutput();
        vertices.Clear();
        indices.Clear();

        if (tile != null && edgeMat != null)
        {
            AddTile(tile);
        }

        ApplyMesh();
    }

    // 타일 한 칸의 네 변을 모두 추가합니다.
    private void AddTile(Tile tile)
    {
        MapBoard board = tile.Board;
        Vector2Int cell = tile.Coord;
        float inset = edgeWidth / board.CellSize;

        AddLeft(board, tile, cell, inset);
        AddRight(board, tile, cell, inset);
        AddBottom(board, tile, cell, inset);
        AddTop(board, tile, cell, inset);
    }

    // 왼쪽 변에 선을 추가합니다.
    private void AddLeft(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        AddQuad(board, tile, new Vector2(cell.x, cell.y), new Vector2(cell.x, cell.y + 1f), new Vector2(cell.x + inset, cell.y), new Vector2(cell.x + inset, cell.y + 1f));
    }

    // 오른쪽 변에 선을 추가합니다.
    private void AddRight(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        AddQuad(board, tile, new Vector2(cell.x + 1f, cell.y + 1f), new Vector2(cell.x + 1f, cell.y), new Vector2(cell.x + 1f - inset, cell.y + 1f), new Vector2(cell.x + 1f - inset, cell.y));
    }

    // 아래쪽 변에 선을 추가합니다.
    private void AddBottom(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        AddQuad(board, tile, new Vector2(cell.x + 1f, cell.y), new Vector2(cell.x, cell.y), new Vector2(cell.x + 1f, cell.y + inset), new Vector2(cell.x, cell.y + inset));
    }

    // 위쪽 변에 선을 추가합니다.
    private void AddTop(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        AddQuad(board, tile, new Vector2(cell.x, cell.y + 1f), new Vector2(cell.x + 1f, cell.y + 1f), new Vector2(cell.x, cell.y + 1f - inset), new Vector2(cell.x + 1f, cell.y + 1f - inset));
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

        edgeObject = new GameObject("HoverEdge");
        edgeObject.transform.SetParent(root, false);
        edgeMesh = new Mesh { name = "HoverEdgeMesh" };
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
