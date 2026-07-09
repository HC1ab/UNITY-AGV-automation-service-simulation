// BorderMesh.cs — 외곽선을 따라 바닥에 평평하게 눕는 띠(리본) 메쉬 생성 (billboard 아님, 어느 각도에서도 보임)
using System.Collections.Generic;
using UnityEngine;

public static class BorderMesh
{
    public static Mesh Build(IList<Vector2> outline, float y, float width)
    {
        int n = outline.Count;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float half = width * 0.5f;

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = outline[(i - 1 + n) % n];
            Vector2 cur = outline[i];
            Vector2 next = outline[(i + 1) % n];

            Vector2 dirIn = (cur - prev).normalized;
            Vector2 dirOut = (next - cur).normalized;
            Vector2 nIn = new Vector2(-dirIn.y, dirIn.x);
            Vector2 nOut = new Vector2(-dirOut.y, dirOut.x);
            Vector2 miter = (nIn + nOut).normalized;
            float cosHalf = Vector2.Dot(miter, nIn);
            float miterLen = half / Mathf.Max(cosHalf, 0.3f);

            Vector2 inner = cur - miter * miterLen;
            Vector2 outer = cur + miter * miterLen;

            verts.Add(new Vector3(inner.x, y, inner.y));
            verts.Add(new Vector3(outer.x, y, outer.y));
        }

        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = i * 2 + 1;
            int c = ((i + 1) % n) * 2, d = ((i + 1) % n) * 2 + 1;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
            // 반대편에서도 보이도록 뒷면 추가
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(b); tris.Add(d); tris.Add(c);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
