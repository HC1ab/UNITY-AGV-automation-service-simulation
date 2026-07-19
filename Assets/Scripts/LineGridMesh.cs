// LineGridMesh.cs — 독립된 직선 구간(segment)들을 얇은 리본 사각형으로 만들어 하나의 메쉬로 합친다.
// 야드 적치 구획선(흰색 페인트 라인)처럼, 서로 교차하는 여러 직선을 한 번에 그릴 때 사용.
using System.Collections.Generic;
using UnityEngine;

public static class LineGridMesh
{
    public static Mesh Build(IList<(Vector2 a, Vector2 b)> segments, float y, float width)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float half = width * 0.5f;

        foreach (var seg in segments)
        {
            Vector2 dir = (seg.b - seg.a).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * half;

            int b = verts.Count;
            verts.Add(new Vector3(seg.a.x - normal.x, y, seg.a.y - normal.y));
            verts.Add(new Vector3(seg.a.x + normal.x, y, seg.a.y + normal.y));
            verts.Add(new Vector3(seg.b.x - normal.x, y, seg.b.y - normal.y));
            verts.Add(new Vector3(seg.b.x + normal.x, y, seg.b.y + normal.y));

            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            // 반대편에서도 보이도록 뒷면 추가
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
        }

        var mesh = new Mesh();
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
