using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 모닥불·가림막처럼 클릭으로 켜진 범위의 바깥 경계에만 펄스 외곽선을 그린다.
public class CampfireEdgeView : IDisposable
{
    private readonly Transform root;
    private readonly Material edgeMat;
    private readonly float edgeWidth;
    private readonly float edgeLift;
    private readonly string outputName;
    private readonly List<Vector3> vertices = new();
    private readonly List<int> indices = new();
    private readonly HashSet<Tile> inRange = new();

    private GameObject edgeObject;
    private Mesh edgeMesh;
    private int lastVersion;
    private bool hasRange;

    // 외곽선 출력에 필요한 표시 설정을 보관합니다.
    public CampfireEdgeView(Transform root, Material edgeMat, float edgeWidth, float edgeLift, string outputName)
    {
        this.root = root;
        this.edgeMat = edgeMat;
        this.edgeWidth = edgeWidth;
        this.edgeLift = edgeLift;
        this.outputName = outputName;
    }

    // 클릭한 칸의 범위 목록에 맞춰 외곽선을 표시합니다.
    public void Show(IReadOnlyList<Tile> range, int version)
    {
        if (IsSame(version))
        {
            return;
        }

        lastVersion = version;
        hasRange = true;
        Rebuild(range);
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

    // 이전에 표시한 범위와 같은 판인지 반환합니다.
    private bool IsSame(int version)
    {
        return hasRange && version == lastVersion;
    }

    // 넘겨받은 범위에 맞춰 외곽선 메시를 다시 만듭니다.
    private void Rebuild(IReadOnlyList<Tile> range)
    {
        EnsureOutput();
        vertices.Clear();
        indices.Clear();
        inRange.Clear();

        if (edgeMat != null)
        {
            FillLookup(range);
            AddRangeTiles(range);
        }

        ApplyMesh();
    }

    // 이웃 판정에 쓸 조회용 집합을 채웁니다.
    private void FillLookup(IReadOnlyList<Tile> range)
    {
        for (int i = 0; i < range.Count; i++)
        {
            inRange.Add(range[i]);
        }
    }

    // 범위 안 타일마다 노출된 변을 추가합니다.
    private void AddRangeTiles(IReadOnlyList<Tile> range)
    {
        for (int i = 0; i < range.Count; i++)
        {
            AddTile(range[i]);
        }
    }

    // 범위 밖으로 노출된 네 변을 추가합니다.
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

    // 왼쪽 이웃이 범위 밖일 때 그 변에 선을 추가합니다.
    private void AddLeft(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        if (HasNeighbor(board, cell + Vector2Int.left))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x, cell.y), new Vector2(cell.x, cell.y + 1f), new Vector2(cell.x + inset, cell.y), new Vector2(cell.x + inset, cell.y + 1f));
    }

    // 오른쪽 이웃이 범위 밖일 때 그 변에 선을 추가합니다.
    private void AddRight(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        if (HasNeighbor(board, cell + Vector2Int.right))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x + 1f, cell.y + 1f), new Vector2(cell.x + 1f, cell.y), new Vector2(cell.x + 1f - inset, cell.y + 1f), new Vector2(cell.x + 1f - inset, cell.y));
    }

    // 아래 이웃이 범위 밖일 때 그 변에 선을 추가합니다.
    private void AddBottom(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        if (HasNeighbor(board, cell + Vector2Int.down))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x + 1f, cell.y), new Vector2(cell.x, cell.y), new Vector2(cell.x + 1f, cell.y + inset), new Vector2(cell.x, cell.y + inset));
    }

    // 위 이웃이 범위 밖일 때 그 변에 선을 추가합니다.
    private void AddTop(MapBoard board, Tile tile, Vector2Int cell, float inset)
    {
        if (HasNeighbor(board, cell + Vector2Int.up))
        {
            return;
        }

        AddQuad(board, tile, new Vector2(cell.x, cell.y + 1f), new Vector2(cell.x + 1f, cell.y + 1f), new Vector2(cell.x, cell.y + 1f - inset), new Vector2(cell.x + 1f, cell.y + 1f - inset));
    }

    // 지정 좌표의 타일이 지금 범위 안에 있는지 반환합니다.
    private bool HasNeighbor(MapBoard board, Vector2Int cell)
    {
        if (board.TryGetCell(cell, out Tile tile))
        {
            return inRange.Contains(tile);
        }

        return false;
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

        edgeObject = new GameObject(outputName);
        edgeObject.transform.SetParent(root, false);
        edgeMesh = new Mesh { name = outputName + "Mesh" };
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
