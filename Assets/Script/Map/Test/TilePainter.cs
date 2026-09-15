using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TilePainter : MonoBehaviour
{
    //디버그용 타일 색상 변경
    public MapBoard board; // 주입(자동탐색 금지)

    [Header("Colors")]
    public Color okColor = new(0.21f, 0.77f, 0.41f);
    public Color denyColor = new(0.85f, 0.29f, 0.27f);
    [Tooltip("배치 프리뷰/호버 시 유닛 사거리 타일 색.")]
    public Color rangeColor = new(0f, 1f, 0.226f);
    [Tooltip("시전자로 선택된 영웅의 액티브 스킬 타격 범위 타일 색.")]
    public Color skillColor = new(1f, 0.55f, 0.15f);
    [Header("Range Scan")]
    [Tooltip("모든 타일 표시에 공통으로 사용할 RangeScan 머티리얼입니다.")]
    [SerializeField] private Material scanMat;
    [Range(0f, 1f)]
    [Tooltip("상태 색상에서 스캔 선을 얼마나 밝힐지 정합니다.")]
    [SerializeField] private float scanLight = 0.55f;
    [Tooltip("타일 윗면에서 이만큼 띄워 그린다.")]
    public float lift = 0.02f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ScanColorId = Shader.PropertyToID("_ScanColor");
    private MaterialPropertyBlock _props;
    private readonly Dictionary<Tile, GameObject> _marks = new();     // 칠해진 타일 → 그 위에 띄운 판
    private readonly Stack<GameObject> _pool = new();                 // 다 쓴 판(매 프레임 다시 쓴다)
    private Mesh _quad;

    // 타일별 색상 전달 상자를 준비합니다.
    private void Awake()
    {
        _props = new MaterialPropertyBlock();
    }

    public void SetColor(Vector2Int coord, Color color)
    {
        if (board.TryGetCell(coord, out Tile tile))
        {
            Paint(tile, color);
        }
    }

    public void ClearColor(Vector2Int coord)
    {
        if (board.TryGetCell(coord, out Tile tile))
        {
            Restore(tile);
        }
    }

    // 타일을 직접 받는 경로 — 좌표는 모듈 로컬이라 보드 역조회가 모듈을 특정 못 하므로,
    // 어느 모듈 타일이든 색칠하려면 이쪽을 쓴다.
    public void SetColor(Tile tile, Color color)
    {
        Paint(tile, color);
    }

    // 타일 표시를 제거합니다.
    public void ClearColor(Tile tile)
    {
        Restore(tile);
    }

    // 타일 머티리얼은 건드리지 않고 윗면 위에 색 판을 한 장 띄운다.
    // 타일 셰이더에 색 프로퍼티가 없는 것도 있어서, 타일 자체를 물들이는 방식은 쓸 수 없다.
    private void Paint(Tile tile, Color color)
    {
        PaintScan(tile, color);
    }

    // 타일 윗면에 공통 스캔 표시 메시를 배치합니다.
    private void PaintScan(Tile tile, Color color)
    {
        if (tile == null || !Paintable(tile))
        {
            return;
        }

        if (scanMat == null)
        {
            return;
        }

        if (!_marks.TryGetValue(tile, out GameObject mark))
        {
            mark = Take();
            _marks[tile] = mark;
        }

        float size = ResolveSize(tile);
        mark.transform.SetPositionAndRotation(
            tile.WorldTop + Vector3.up * lift,
            Quaternion.Euler(90f, 0f, 0f));
        mark.transform.localScale = new Vector3(size, size, 1f);
        MeshRenderer renderer = mark.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = scanMat;
        ApplyColor(renderer, color);
        mark.SetActive(true);
    }

    // 타일 크기를 반환합니다.
    private static float ResolveSize(Tile tile)
    {
        if (tile.Board != null)
        {
            return tile.Board.CellSize;
        }

        return 1f;
    }

    // 상태 색상과 같은 계열의 밝은 스캔 색상을 적용합니다.
    private void ApplyColor(MeshRenderer renderer, Color color)
    {
        Color scanColor = Color.Lerp(color, Color.white, scanLight);
        _props.Clear();
        _props.SetColor(BaseColorId, color);
        _props.SetColor(ScanColorId, scanColor);
        renderer.SetPropertyBlock(_props);
    }

    private void Restore(Tile tile)
    {
        if (tile == null || !_marks.TryGetValue(tile, out GameObject mark))
        {
            return;
        }

        _marks.Remove(tile);
        mark.SetActive(false);
        _pool.Push(mark);
    }

    // 칠해도 되는 타일인가. 어느 칠하기 경로로 와도 여기서 걸린다.
    // 장식 타일은 외곽 줄이든 생산·전투를 가르는 안쪽 벽이든 칠하지 않는다 —
    // 덮는 칸에 걸치면 그 자리가 빈 채로 남아, 무엇 때문에 못 놓는지가 그대로 보인다.
    private static bool Paintable(Tile tile)
    {
        if (tile.IsSpecial)
        {
            return false;
        }

        if (tile.Board == null)
        {
            return true;   // 보드 도장 전(에디터 도구)에는 맵 밖인지 판정할 근거가 없다
        }

        RectInt rect = tile.Board.PlayRect;
        return rect.width <= 0 || rect.Contains(tile.Coord);
    }

    // 판 하나를 꺼낸다. 타일의 자식으로 두면 MapBoard가 윗면 높이를 이 판까지 포함해 재므로 painter 아래에 붙인다.
    private GameObject Take()
    {
        if (_pool.Count > 0)
        {
            return _pool.Pop();
        }

        GameObject mark = new("TileMark");
        mark.hideFlags = HideFlags.DontSave;
        mark.transform.SetParent(transform, false);
        mark.AddComponent<MeshFilter>().sharedMesh = Quad();

        MeshRenderer renderer = mark.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return mark;
    }

    // 한 칸 크기(1×1) 판 메시. Euler(90,0,0)으로 세우면 윗면을 보게 되도록 -Z를 향해 만든다.
    private Mesh Quad()
    {
        if (_quad != null)
        {
            return _quad;
        }

        _quad = new Mesh { name = "TileMarkQuad", hideFlags = HideFlags.DontSave };
        _quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
        };
        _quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        _quad.triangles = new[] { 0, 1, 2, 2, 1, 3 };

        return _quad;
    }

    private void OnDestroy()
    {
        if (_quad != null)
        {
            Destroy(_quad);
            _quad = null;
        }
    }

    // ---- 에디터 디버그: 지형 한눈에 보기(우클릭 실행) ----
    // 노랑=스폰, 파랑=본진(Core), 빨강=High(벽), 초록=Ground(통행), 회색=Empty(벽).
    // 장식(Special)은 여기서도 칠하지 않아 빈자리로 남는다.

    [ContextMenu("Tint Terrain")]
    public void TintTerrain()
    {
        if (board.Cells.Count == 0) board.Build();

        foreach (Tile t in board.Cells.Values)
        {
            Color c = t.isEnemySpawn ? new Color(0.95f, 0.9f, 0.2f)
                : t.Terrain switch
                {
                    TerrainType.Core   => new Color(0.2f, 0.5f, 1f),
                    TerrainType.High   => new Color(0.9f, 0.3f, 0.2f),
                    TerrainType.Ground => new Color(0.3f, 0.8f, 0.4f),
                    _                  => new Color(0.5f, 0.5f, 0.5f),
                };
            Paint(t, c);
        }
    }

    [ContextMenu("Clear Tint")]
    public void ClearTint()
    {
        foreach (Tile t in board.Cells.Values) Restore(t);
    }
}
