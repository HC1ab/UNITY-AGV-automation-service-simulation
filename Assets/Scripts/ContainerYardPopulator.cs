// ContainerYardPopulator.cs — TerminalYardBuilder가 그려둔 슬롯 격자 위에 컨테이너를
// 랜덤하게(점유율 + 1~3단 높이) 배치한다. 슬롯 좌표 계산 규칙은 TerminalYardBuilder와
// 동일해야 흰색 구획선과 어긋나지 않는다.
//
// 상세 프리팹(containerPrefabs)은 fillCol/RowMin~Max 범위의 "쇼케이스" 블록에만 쓰고,
// 나머지 블록은 GenerateRestAsCubes()로 단순 큐브(블록당 색상별 1개 메쉬로 합쳐서 드로우콜
// 최소화)를 채워 오브젝트 수/렌더링 부담을 크게 줄인다.
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ContainerYardPopulator : MonoBehaviour
{
    public TerminalYardBuilder yard;

    [Header("상세 프리팹 쇼케이스 범위 (블록 인덱스, 0-based, inclusive)")]
    public int fillColMin = 0;
    public int fillColMax = 0;
    public int fillRowMin = 0;
    public int fillRowMax = 0;

    [Range(0f, 1f)]
    public float occupancyRate = 0.6f;
    public int maxStackHeight = 3;
    public float containerHeight = 2.59f;

    [Tooltip("Red/Yellow/Blue/White 40ft 컨테이너 프리팹")]
    public GameObject[] containerPrefabs;

    [Header("나머지 블록 — 단순 큐브 (성능용)")]
    public Color[] cubeColors = new Color[]
    {
        new Color(0.80f * 0.7f, 0.10f * 0.7f, 0.08f * 0.7f), // red (30% 어둡게)
        new Color(0.95f * 0.7f, 0.75f * 0.7f, 0.05f * 0.7f), // yellow (30% 어둡게)
        new Color(0.06f * 0.7f, 0.22f * 0.7f, 0.55f * 0.7f), // blue (30% 어둡게)
        new Color(0.92f * 0.7f, 0.92f * 0.7f, 0.90f * 0.7f), // white (30% 어둡게)
    };

    const float DeckHeight = 10f;

    float[] ComputeRowOffsetZ(float depth)
    {
        var rowOffsetZ = new float[yard.gridRows];
        float cumulative = 0f;
        for (int row = 0; row < yard.gridRows; row++)
        {
            rowOffsetZ[row] = cumulative;
            int rowNumber1Based = row + 1;
            float gapAfterThisRow = (rowNumber1Based == yard.exceptionAfterRow) ? yard.exceptionGap : yard.rowGap;
            cumulative += depth + gapAfterThisRow;
        }
        return rowOffsetZ;
    }

    [ContextMenu("Generate Containers")]
    public void Generate()
    {
        Clear();
        if (yard == null || containerPrefabs == null || containerPrefabs.Length == 0) return;

        float width = yard.rows * yard.containerWidth;
        float bayStep = yard.containerLength + yard.bayGap;
        float depth = yard.bays * bayStep;
        var rowOffsetZ = ComputeRowOffsetZ(depth);

        var root = new GameObject("Containers");
        root.transform.SetParent(transform, false);

        for (int col = Mathf.Max(0, fillColMin); col <= Mathf.Min(yard.gridCols - 1, fillColMax); col++)
        {
            for (int row = Mathf.Max(0, fillRowMin); row <= Mathf.Min(yard.gridRows - 1, fillRowMax); row++)
            {
                Vector3 blockOrigin = yard.origin + new Vector3(col * (width + yard.colGap), 0f, -rowOffsetZ[row]);
                PopulateBlock(blockOrigin, bayStep, root.transform);
            }
        }
    }

    [ContextMenu("Generate Rest As Cubes")]
    public void GenerateRestAsCubes()
    {
        ClearCubes();
        if (yard == null || cubeColors == null || cubeColors.Length == 0) return;

        sharedCubeMaterials = null; // cubeColors 배열 크기가 바뀌었을 수 있으니 매번 새로 만든다

        float width = yard.rows * yard.containerWidth;
        float bayStep = yard.containerLength + yard.bayGap;
        float depth = yard.bays * bayStep;
        var rowOffsetZ = ComputeRowOffsetZ(depth);

        Mesh cubeMesh = GetSharedCubeMesh();

        var root = new GameObject("ContainerCubes");
        root.transform.SetParent(transform, false);

        for (int col = 0; col < yard.gridCols; col++)
        {
            for (int row = 0; row < yard.gridRows; row++)
            {
                bool isShowcaseBlock = col >= fillColMin && col <= fillColMax && row >= fillRowMin && row <= fillRowMax;
                if (isShowcaseBlock) continue; // 상세 프리팹으로 이미 채운 블록은 건너뜀

                Vector3 blockOrigin = yard.origin + new Vector3(col * (width + yard.colGap), 0f, -rowOffsetZ[row]);
                BuildBlockCubes(blockOrigin, bayStep, cubeMesh, root.transform, col, row);
            }
        }
    }

    void BuildBlockCubes(Vector3 blockOrigin, float bayStep, Mesh cubeMesh, Transform parent, int col, int row)
    {
        // 색상별로 이 블록 안의 큐브들을 모아뒀다가 한 번에 메쉬 하나로 합쳐서(색상당 드로우콜 1개)
        // 큐브 하나하나를 개별 GameObject로 만들 때보다 오브젝트/드로우콜 수를 크게 줄인다.
        var combineLists = new List<CombineInstance>[cubeColors.Length];
        for (int i = 0; i < cubeColors.Length; i++) combineLists[i] = new List<CombineInstance>();

        for (int r = 0; r < yard.rows; r++)
        {
            for (int b = 0; b < yard.bays; b++)
            {
                if (Random.value > occupancyRate) continue;

                float x = blockOrigin.x + (r + 0.5f) * yard.containerWidth;
                float z = blockOrigin.z - (b + 0.5f) * bayStep;

                int height = Random.Range(1, maxStackHeight + 1);
                for (int level = 1; level <= height; level++)
                {
                    int colorIdx = Random.Range(0, cubeColors.Length);
                    float y = DeckHeight + (level - 0.5f) * containerHeight;

                    var ci = new CombineInstance
                    {
                        mesh = cubeMesh,
                        transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity,
                            new Vector3(yard.containerWidth, containerHeight, yard.containerLength))
                    };
                    combineLists[colorIdx].Add(ci);
                }
            }
        }

        for (int i = 0; i < cubeColors.Length; i++)
        {
            if (combineLists[i].Count == 0) continue;

            var combined = new Mesh();
            combined.indexFormat = combineLists[i].Count * 24 > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            combined.CombineMeshes(combineLists[i].ToArray());

            var go = new GameObject($"Cubes_c{col}_r{row}_{i}");
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = combined;
            mr.sharedMaterial = GetSharedCubeMaterial(i);
        }
    }

    Mesh sharedCubeMesh;
    Mesh GetSharedCubeMesh()
    {
        if (sharedCubeMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sharedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
        }
        return sharedCubeMesh;
    }

    Material[] sharedCubeMaterials;
    Material GetSharedCubeMaterial(int index)
    {
        if (sharedCubeMaterials == null) sharedCubeMaterials = new Material[cubeColors.Length];
        if (sharedCubeMaterials[index] == null)
            sharedCubeMaterials[index] = new Material(Shader.Find("Unlit/Color")) { color = cubeColors[index] };
        return sharedCubeMaterials[index];
    }

    void PopulateBlock(Vector3 blockOrigin, float bayStep, Transform parent)
    {
        for (int r = 0; r < yard.rows; r++)
        {
            for (int b = 0; b < yard.bays; b++)
            {
                if (Random.value > occupancyRate) continue;

                float x = blockOrigin.x + (r + 0.5f) * yard.containerWidth;
                float z = blockOrigin.z - (b + 0.5f) * bayStep;

                // 1차: 이 칸의 스택 높이만 먼저 랜덤으로 정함
                int height = Random.Range(1, maxStackHeight + 1);

                // 2차: 컨테이너를 한 개씩 놓을 때마다 색상을 각각 랜덤으로 선택
                for (int level = 1; level <= height; level++)
                {
                    var prefab = containerPrefabs[Random.Range(0, containerPrefabs.Length)];
                    float y = DeckHeight + (level - 0.5f) * containerHeight;
                    SpawnContainer(prefab, new Vector3(x, y, z), parent);
                }
            }
        }
    }

    void SpawnContainer(GameObject prefab, Vector3 localPos, Transform parent)
    {
        GameObject instance;
#if UNITY_EDITOR
        instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#else
        instance = Instantiate(prefab, parent);
#endif
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = Quaternion.Euler(270f, 270f, 0f);
    }

    [ContextMenu("Clear Containers")]
    public void Clear()
    {
        var existing = transform.Find("Containers");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    [ContextMenu("Clear Rest Cubes")]
    public void ClearCubes()
    {
        var existing = transform.Find("ContainerCubes");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }
}
