// RoadGridBuilder.cs — 3x3 격자형 테스트 도로망 생성 (AGV_NAVIGATION_GUIDE.md 2-1 단계 프로토타입)
// 단위: 1 Unity 유닛 = 실제 1미터 (CLAUDE.md 기준)
using System.Collections.Generic;
using UnityEngine;

public class RoadGridBuilder : MonoBehaviour
{
    public RoadGraphData graph;

    [Tooltip("격자 한 변의 노드 수 (3이면 3x3 = 9개 노드)")]
    public int gridSize = 3;

    [Tooltip("인접 노드 사이 거리(미터)")]
    public float spacing = 50f;

    [Tooltip("도로 폭(미터)")]
    public float roadWidth = 6f;

    [Tooltip("격자의 좌하단(row0,col0) 노드가 위치할 월드 좌표")]
    public Vector3 gridOrigin = new Vector3(300f, 0.05f, -400f);

    public Material roadMaterial; // Unlit/Color 권장

    [ContextMenu("Generate Grid")]
    public void Generate()
    {
        Clear();
        if (graph == null)
        {
            Debug.LogWarning("RoadGridBuilder: RoadGraphData(graph)가 지정되지 않았습니다.");
            return;
        }

        graph.nodes.Clear();
        graph.edges.Clear();

        // 노드 생성 (row = Z축, col = X축)
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                var pos = gridOrigin + new Vector3(col * spacing, 0f, row * spacing);
                graph.nodes.Add(new NodeData { id = NodeId(row, col), position = pos });
            }
        }

        // 엣지 생성 (가로: 같은 row, 인접 col / 세로: 같은 col, 인접 row)
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                if (col + 1 < gridSize) AddEdge(row, col, row, col + 1);
                if (row + 1 < gridSize) AddEdge(row, col, row + 1, col);
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(graph);
        UnityEditor.AssetDatabase.SaveAssets();
#endif

        BuildVisualMesh();
    }

    string NodeId(int row, int col) => "R" + row + "C" + col;

    void AddEdge(int rowA, int colA, int rowB, int colB)
    {
        var a = graph.FindNode(NodeId(rowA, colA));
        var b = graph.FindNode(NodeId(rowB, colB));
        graph.edges.Add(new EdgeData
        {
            fromId = a.id,
            toId = b.id,
            waypoints = new List<Vector3> { a.position, b.position }
        });
    }

    [ContextMenu("Clear Grid")]
    public void Clear()
    {
        var existing = transform.Find("RoadVisuals");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    void BuildVisualMesh()
    {
        var root = new GameObject("RoadVisuals");
        root.transform.SetParent(transform, false);

        var mat = roadMaterial != null ? roadMaterial : new Material(Shader.Find("Unlit/Color")) { color = new Color(0.15f, 0.15f, 0.15f) };

        // 엣지 스트립
        foreach (var edge in graph.edges)
        {
            Vector3 a = edge.waypoints[0];
            Vector3 b = edge.waypoints[edge.waypoints.Count - 1];
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            float angleY = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Road_" + edge.fromId + "_" + edge.toId;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.position = mid;
            go.transform.rotation = Quaternion.Euler(90f, angleY, 0f);
            go.transform.localScale = new Vector3(roadWidth, len, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // 노드(교차로) 패드 — 엣지 사이 코너를 채워 교차로처럼 보이게 함
        foreach (var node in graph.nodes)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Node_" + node.id;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.position = node.position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(roadWidth, roadWidth, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    void OnDrawGizmos()
    {
        if (graph == null) return;

        Gizmos.color = Color.cyan;
        foreach (var node in graph.nodes)
            Gizmos.DrawSphere(node.position, 1.5f);

        Gizmos.color = Color.yellow;
        foreach (var edge in graph.edges)
            for (int i = 0; i < edge.waypoints.Count - 1; i++)
                Gizmos.DrawLine(edge.waypoints[i], edge.waypoints[i + 1]);
    }
}
