// TerminalYardBuilder.cs — 컨테이너 적치 슬롯 흰색 구획선을 가진 야드 블록을 격자로 여러 개 생성한다.
// 블록 하나 = rows(폭 방향 컨테이너 수) x bays(길이 방향 컨테이너 수).
// 블록을 gridCols x gridRows개 만큼 간격을 두고 나란히 배치한다.
//
// 참고: Terminal_DGT 자체가 월드에서 Y축 270도 회전돼 있어, 로컬 X/Z는 실제 화면(월드)
// 기준으로 서로 뒤바뀌어 보인다. 로컬 X는 블록의 폭(rows) 방향 + 블록들을 나란히 늘어놓는
// 방향(월드 Z), 로컬 Z는 블록의 길이(bays) 방향 + 여러 줄을 쌓는 방향(월드 X)이다.
using System.Collections.Generic;
using UnityEngine;

public class TerminalYardBuilder : MonoBehaviour
{
    [Tooltip("그리드의 (col0,row0) 블록 모서리 — Terminal_DGT 로컬 좌표")]
    public Vector3 origin = new Vector3(10f, 0f, -10f);

    [Header("블록 1개 크기")]
    [Tooltip("폭 방향 컨테이너 수")]
    public int rows = 40;
    [Tooltip("길이 방향 컨테이너(베이) 수")]
    public int bays = 2;

    [Header("컨테이너 크기 (40ft 기준)")]
    public float containerLength = 12.19f;
    public float containerWidth = 2.44f;
    public float bayGap = 0.5f; // 베이 사이 간격 — 크레인 통행용

    [Header("블록을 몇 개, 얼마나 간격 두고 늘어놓을지")]
    [Tooltip("로컬 X방향(=월드 Z)으로 나란히 놓을 블록 수")]
    public int gridCols = 5;
    [Tooltip("로컬 X방향 블록 사이 간격(m)")]
    public float colGap = 10f;
    [Tooltip("로컬 Z방향(=월드 X)으로 쌓을 블록 수")]
    public int gridRows = 5;
    [Tooltip("로컬 Z방향 블록 사이 간격(m)")]
    public float rowGap = 6f;

    [Header("추가 확장 (베이 구획선을 월드 -Z 방향으로 더 길게)")]
    [Tooltip("실제 사진처럼 베이 구획선이 행 라인 범위보다 월드 -Z 방향으로 더 뻗어나가는 여유 길이(m). 0이면 사용 안 함")]
    public float extraDepthZ = 0f;

    [Header("예외 간격 (특정 블록 사이만 다르게)")]
    [Tooltip("몇 번째 블록 뒤의 간격을 다르게 할지 (1부터 시작. 예: 12면 12번째와 13번째 사이). 0이면 사용 안 함")]
    public int exceptionAfterRow = 0;
    [Tooltip("위 위치의 간격(m)")]
    public float exceptionGap = 16f;

    public float lineWidth = 0.15f;
    public Material lineMaterial; // 흰색, Unlit/Color 권장

    [Tooltip("바닥(데크) 표면보다 얼마나 띄워서 그릴지 (Z-fighting 방지)")]
    public float surfaceOffset = 0.02f;

    const float DeckHeight = 10f; // Terminal_DGT height

    [ContextMenu("Generate Yard")]
    public void Generate()
    {
        Clear();
        if (rows < 1 || bays < 1 || gridCols < 1 || gridRows < 1) return;

        float width = rows * containerWidth;        // 블록 1개, 로컬 X 크기(=월드 Z)
        float bayStep = containerLength + bayGap;
        float depth = bays * bayStep;                // 블록 1개, 로컬 Z 크기(=월드 X)

        var mat = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Unlit/Color")) { color = Color.white };
        float y = DeckHeight + surfaceOffset;

        var root = new GameObject("YardBlocks");
        root.transform.SetParent(transform, false);

        // 행(row)별 누적 Z 오프셋 — 특정 위치(exceptionAfterRow)만 다른 간격을 쓸 수 있도록 누적으로 계산
        var rowOffsetZ = new float[gridRows];
        float cumulative = 0f;
        for (int row = 0; row < gridRows; row++)
        {
            rowOffsetZ[row] = cumulative;
            int rowNumber1Based = row + 1; // 1번째, 2번째, ...
            float gapAfterThisRow = (rowNumber1Based == exceptionAfterRow) ? exceptionGap : rowGap;
            cumulative += depth + gapAfterThisRow;
        }

        for (int col = 0; col < gridCols; col++)
        {
            for (int row = 0; row < gridRows; row++)
            {
                Vector3 blockOrigin = origin + new Vector3(col * (width + colGap), 0f, -rowOffsetZ[row]);
                BuildOneBlock(blockOrigin, width, depth, bayStep, y, mat, root.transform, col, row);
            }
        }
    }

    void BuildOneBlock(Vector3 blockOrigin, float width, float depth, float bayStep, float y, Material mat, Transform parent, int col, int row)
    {
        var segments = new List<(Vector2 a, Vector2 b)>();

        // 세로선(행 경계, 길이 방향으로 뻗음) — rows+1개
        for (int i = 0; i <= rows; i++)
        {
            float x = blockOrigin.x + i * containerWidth;
            segments.Add((new Vector2(x, blockOrigin.z), new Vector2(x, blockOrigin.z - depth)));
        }

        // 가로선(베이 경계, 폭 방향으로 뻗음) — bays+1개. 로컬 X는 월드 Z와 직접 대응되므로,
        // +local X로 extraDepthZ만큼 더 뻗어야 실제로 월드 -Z 방향으로 확장된다.
        // 단, 양 끝(바깥 경계)만 확장하고 가운데 베이 구분선은 확장하지 않는다.
        for (int j = 0; j <= bays; j++)
        {
            float z = blockOrigin.z - j * bayStep;
            bool isOuterEdge = (j == 0 || j == bays);
            float extra = isOuterEdge ? extraDepthZ : 0f;
            segments.Add((new Vector2(blockOrigin.x, z), new Vector2(blockOrigin.x + width + extra, z)));
        }

        var mesh = LineGridMesh.Build(segments, y, lineWidth);
        var go = new GameObject($"YardBlock_c{col}_r{row}");
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = mat;
    }

    [ContextMenu("Clear Yard")]
    public void Clear()
    {
        var existing = transform.Find("YardBlocks");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }
}
