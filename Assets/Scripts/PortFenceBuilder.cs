// PortFenceBuilder.cs — 터미널 육지 경계(수변/안벽 제외)를 따라 철망 펜스 벽을 세운다.
// 메시 개수를 줄이기 위해 개별 프리팹을 반복 배치하지 않고, 변(edge)마다 얇은 박스 1개 + 알파 컷아웃 텍스처로 표현한다.
// 텍스처 출처: Poly Haven "Modular Chainlink Fence" (CC0) 철망 텍스처.
// 단위: 1 Unity 유닛 = 실제 1미터 (CLAUDE.md 기준)
using UnityEngine;

[RequireComponent(typeof(PortMapBuilder))]
public class PortFenceBuilder : MonoBehaviour
{
    public Material fenceMaterial; // Fence_Wire_Mat (알파 컷아웃)
    public float fenceHeight = 2.4f;
    public float fenceThickness = 0.05f;
    [Tooltip("텍스처 1장이 덮는 실제 폭(미터). 값이 작을수록 철망 무늬가 촘촘하게 반복된다.")]
    public float textureMetersPerTileX = 2f;
    public float textureMetersPerTileY = 2.4f;

    PortMapBuilder builder;

    void Awake() { builder = GetComponent<PortMapBuilder>(); }

    [ContextMenu("Build Fences")]
    public void BuildFences()
    {
        if (fenceMaterial == null) { Debug.LogWarning("PortFenceBuilder: fenceMaterial 미지정"); return; }
        if (builder == null) builder = GetComponent<PortMapBuilder>();

        ClearFences();

        var root = new GameObject("Fences");
        root.transform.SetParent(transform, false);

        foreach (var t in builder.terminals)
            BuildTerminalFence(t, root.transform);
    }

    [ContextMenu("Clear Fences")]
    public void ClearFences()
    {
        var existing = transform.Find("Fences");
        if (existing != null) DestroyImmediate(existing.gameObject);
    }

    const float SharedEdgeEpsilon = 1f; // 미터. 인접 터미널 경계 좌표 오차 허용치

    // 선석(berth) 평균 위치에서 가장 가까운 변 = 안벽(수변)이므로 펜스를 세우지 않는다.
    // 다른 터미널과 맞닿은 변(터미널 사이 경계)도 육지 외곽이 아니므로 펜스를 세우지 않는다.
    void BuildTerminalFence(Terminal t, Transform parent)
    {
        int n = t.outline.Count;
        if (n < 2) return;

        Vector2 berthAvg = Vector2.zero;
        foreach (var b in t.berths) berthAvg += b.pos;
        if (t.berths.Count > 0) berthAvg /= t.berths.Count;

        int quayEdge = -1;
        float bestDist = float.MaxValue;
        if (t.berths.Count > 0)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 a = t.outline[i];
                Vector2 b = t.outline[(i + 1) % n];
                Vector2 mid = (a + b) * 0.5f;
                float d = Vector2.Distance(mid, berthAvg);
                if (d < bestDist) { bestDist = d; quayEdge = i; }
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (i == quayEdge) continue;
            Vector2 a = t.outline[i];
            Vector2 b = t.outline[(i + 1) % n];
            if (IsSharedWithOtherTerminal(a, b, t)) continue;
            BuildEdgeWall(a, b, t, parent, i);
        }
    }

    // (a,b) 변이 다른 터미널의 어떤 변과 (순서 무관하게) 겹치는지 검사
    bool IsSharedWithOtherTerminal(Vector2 a, Vector2 b, Terminal self)
    {
        foreach (var other in builder.terminals)
        {
            if (other == self) continue;
            int m = other.outline.Count;
            for (int j = 0; j < m; j++)
            {
                Vector2 oa = other.outline[j];
                Vector2 ob = other.outline[(j + 1) % m];
                bool sameDir = Vector2.Distance(a, oa) < SharedEdgeEpsilon && Vector2.Distance(b, ob) < SharedEdgeEpsilon;
                bool revDir = Vector2.Distance(a, ob) < SharedEdgeEpsilon && Vector2.Distance(b, oa) < SharedEdgeEpsilon;
                if (sameDir || revDir) return true;
            }
        }
        return false;
    }

    void BuildEdgeWall(Vector2 a, Vector2 b, Terminal t, Transform parent, int edgeIndex)
    {
        Vector2 diff = b - a;
        float len = diff.magnitude;
        if (len < 0.01f) return;
        float angleY = Mathf.Atan2(diff.x, diff.y) * Mathf.Rad2Deg;
        Vector2 mid = (a + b) * 0.5f;

        var go = new GameObject("FenceWall_" + t.name + "_" + edgeIndex);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(mid.x, t.height + fenceHeight * 0.5f, mid.y);
        go.transform.localRotation = Quaternion.Euler(0f, angleY, 0f);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = BuildWallMesh(len, fenceHeight, fenceThickness, len / textureMetersPerTileX, fenceHeight / textureMetersPerTileY);
        mr.sharedMaterial = fenceMaterial;

#if UNITY_EDITOR
        UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.BatchingStatic);
#endif
    }

    // 로컬 Z=길이(회전 계산이 로컬 +Z를 경계선 방향에 맞추는 기준이므로), Y=높이, X=두께인 얇은 박스 메시.
    // UV는 실측 미터 기준으로 타일링.
    static Mesh BuildWallMesh(float length, float height, float thickness, float tileU, float tileV)
    {
        float hz = length * 0.5f, hy = height * 0.5f, hx = thickness * 0.5f;

        var mesh = new Mesh();
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        var normals = new System.Collections.Generic.List<Vector3>();

        void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
        {
            int baseIdx = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(tileU, 0));
            uvs.Add(new Vector2(tileU, tileV));
            uvs.Add(new Vector2(0, tileV));
            tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
        }

        // 좌측면(+X), 우측면(-X) — 철망 무늬가 양면에서 보이도록
        AddQuad(new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz), Vector3.right);
        AddQuad(new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), Vector3.left);

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.subMeshCount = 1;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
