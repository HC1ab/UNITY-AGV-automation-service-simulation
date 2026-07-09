// PortMesh.cs — 2D 외곽선(X-Z)을 Y로 돌출시킨 3D 메쉬로 변환 (추가 패키지 불필요)
using System.Collections.Generic;
using UnityEngine;

public static class PortMesh
{
    // tileMeters: UV를 실제 미터 단위로 굽는 기준(= 텍스처 1장이 덮는 실제 크기). 이 값으로 나눈 좌표를 UV로 사용해 반복시킨다.
    public static Mesh Extrude(IList<Vector2> outline, float height, float tileMeters = 8f)
    {
        int n = outline.Count;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris  = new List<int>();

        // 윗면 (탑다운에서 실제로 보이는 면)
        for (int i = 0; i < n; i++)
        {
            verts.Add(new Vector3(outline[i].x, height, outline[i].y));
            uvs.Add(new Vector2(outline[i].x / tileMeters, outline[i].y / tileMeters));
        }
        var cap = Triangulate(outline);
        tris.AddRange(cap);

        // 옆벽
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int b = verts.Count;
            float edgeLen = Vector2.Distance(outline[i], outline[next]) / tileMeters;
            float hUv = height / tileMeters;
            verts.Add(new Vector3(outline[i].x,    height, outline[i].y)); uvs.Add(new Vector2(0, hUv));
            verts.Add(new Vector3(outline[next].x, height, outline[next].y)); uvs.Add(new Vector2(edgeLen, hUv));
            verts.Add(new Vector3(outline[i].x,    0,      outline[i].y)); uvs.Add(new Vector2(0, 0));
            verts.Add(new Vector3(outline[next].x, 0,      outline[next].y)); uvs.Add(new Vector2(edgeLen, 0));
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
        }

        var mesh = new Mesh();
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static int[] Triangulate(IList<Vector2> poly) // ear-clipping
    {
        var idx = new List<int>(); int n = poly.Count;
        if (n < 3) return idx.ToArray();
        var V = new List<int>();
        if (Area(poly) > 0) for (int v = 0; v < n; v++) V.Add(v);
        else                for (int v = 0; v < n; v++) V.Add(n - 1 - v);
        int nv = n, count = 2 * nv;
        for (int v = nv - 1; nv > 2;)
        {
            if (count-- <= 0) break;
            int u = v % nv; v = (u + 1) % nv; int w = (v + 1) % nv;
            if (Snip(poly, u, v, w, nv, V))
            { idx.Add(V[u]); idx.Add(V[v]); idx.Add(V[w]); V.RemoveAt(v); nv--; count = 2 * nv; }
        }
        idx.Reverse();
        return idx.ToArray();
    }
    static float Area(IList<Vector2> p){ float a=0; int n=p.Count;
        for(int i=n-1,j=0;j<n;i=j++) a+=p[i].x*p[j].y-p[j].x*p[i].y; return a*0.5f; }
    static bool Snip(IList<Vector2> p,int u,int v,int w,int n,List<int> V){
        Vector2 A=p[V[u]],B=p[V[v]],C=p[V[w]];
        if(Mathf.Epsilon>((B.x-A.x)*(C.y-A.y)-(B.y-A.y)*(C.x-A.x))) return false;
        for(int q=0;q<n;q++){ if(q==u||q==v||q==w) continue; if(InTri(A,B,C,p[V[q]])) return false; }
        return true; }
    static bool InTri(Vector2 A,Vector2 B,Vector2 C,Vector2 P){
        float d1=(B.x-A.x)*(P.y-A.y)-(B.y-A.y)*(P.x-A.x);
        float d2=(C.x-B.x)*(P.y-B.y)-(C.y-B.y)*(P.x-B.x);
        float d3=(A.x-C.x)*(P.y-C.y)-(A.y-C.y)*(P.x-C.x);
        return (d1>=0&&d2>=0&&d3>=0)||(d1<=0&&d2<=0&&d3<=0); }
}
