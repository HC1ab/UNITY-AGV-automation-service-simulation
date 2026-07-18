// PortMapBuilder.cs — 부산항 신항 정적 시각화
// 단위: 1 Unity 유닛 = 실제 1미터. 기준 데이터는 CLAUDE.md 참고 (출처: busanpa.com, 2026-07-09 확인).
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class Berth { public string id; public Vector2 pos; }
[System.Serializable] public class Terminal
{
    public string name;
    public Color color = new Color(0.83f, 0.85f, 0.90f);
    public float height = 0.6f;
    public List<Vector2> outline = new List<Vector2>();
    public List<Berth> berths = new List<Berth>();
}

public class PortMapBuilder : MonoBehaviour
{
    public List<Terminal> terminals = new List<Terminal>();
    public Material terminalMat;   // Unlit/Color 권장
    public Material berthMat;
    public Material waterMat;
    public Color waterColor = new Color(0.75f, 0.87f, 0.94f);
    public bool buildWater = false;

    [Tooltip("바다를 남쪽(Z-)으로 얼마나 늘릴지 (미터)")]
    public float seaExtendMeters = 3000f;

    // 런타임에 자동 재생성하지 않음 — 에디터에서 "Generate Map"으로 한 번 구운 결과를
    // 씬에 저장해두고 그대로 사용한다. Play를 눌러도 다시 만들지 않는다.
    [ContextMenu("Generate Map")]
    public void Generate()
    {
        Clear();
        terminals.Clear();
        BuildSampleData();
        if (buildWater) BuildWater();
        foreach (var t in terminals) BuildTerminal(t);

        var fenceBuilder = GetComponent<PortFenceBuilder>();
        if (fenceBuilder != null) fenceBuilder.BuildFences();
    }

    void BuildWater()
    {
        // 전체 터미널 범위를 계산해 그 아래(남쪽)로 seaExtendMeters만큼 확장
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var t in terminals)
            foreach (var p in t.outline)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }
        float southZ = minZ - seaExtendMeters;
        float width = maxX - minX;
        float depth = maxZ - southZ;

        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Water";
        go.transform.SetParent(transform, false);
        DestroyImmediate(go.GetComponent<Collider>());
        // Unity 기본 Plane은 10x10 유닛(스케일 1당 10유닛)
        go.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);
        go.transform.localPosition = new Vector3((minX + maxX) * 0.5f, -0.05f, (maxZ + southZ) * 0.5f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = waterMat != null ? new Material(waterMat) { color = waterColor }
                                        : new Material(Shader.Find("Unlit/Color")) { color = waterColor };
    }

    [ContextMenu("Clear Map")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    [Tooltip("터미널 바닥 텍스처 1장이 덮는 실제 크기(미터). 값이 작을수록 촘촘하게 반복된다.")]
    public float terminalTextureMetersPerTile = 8f;

    void BuildTerminal(Terminal t)
    {
        var go = new GameObject("Terminal_" + t.name);
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var mesh = PortMesh.Extrude(t.outline, t.height, terminalTextureMetersPerTile);
        mf.sharedMesh = mesh;

        if (terminalMat != null)
        {
            mr.material = new Material(terminalMat);
        }
        else
        {
            mr.material = new Material(Shader.Find("Unlit/Color")) { color = t.color };
        }

        foreach (var b in t.berths)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m.name = "Berth_" + b.id;
            m.transform.SetParent(go.transform, false);
            m.transform.localScale = new Vector3(20f, 0.05f, 20f); // 실측 없음: 시인성용 임의 크기(20m)
            m.transform.localPosition = new Vector3(b.pos.x, t.height + 0.05f, b.pos.y);
            if (berthMat) m.GetComponent<Renderer>().material = berthMat;
        }
    }

    public Color borderColor = new Color(0.15f, 0.15f, 0.15f);
    public float borderWidth = 5f; // 미터

    void BuildBorder(GameObject parent, Terminal t)
    {
        var go = new GameObject("Border");
        go.transform.SetParent(parent.transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = BorderMesh.Build(t.outline, t.height + 0.02f, borderWidth);
        mr.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = borderColor };
    }

    // Assets/Textures/PortMap.png에서 색상 임계값(회색 부두 채움색) + 연결요소 분석 + 컨투어 추적으로
    // 자동 추출한 정규화 좌표(0~1, 왼쪽위 원점). 사람이 눈대중으로 그린 게 아니라 이미지 픽셀에서 직접 뽑은 값이라
    // 이미지의 실제 형태(오목한 모서리 포함)를 그대로 반영한다. (추출 스크립트: scratchpad/extract_outlines.py)
    // 각 부두의 폭(안벽 방향 치수)만 CLAUDE.md 실측 안벽길이(m)로 대체하고, 깊이/간격 축은 원본 비율(*3960m)을 유지한다.
    const float WorldH = 9000f * (44f / 100f); // 3960m. 원본 이미지 세로 비율 유지(깊이/간격 축 전용)
    static float Z(float ny) => (1f - ny) * WorldH;

    // 컨투어 추출 노이즈로 경계 근처에 거의 같은 값(수십m 이내)이 여러 개 잡히는 경우, 가장 바깥쪽 값으로 스냅해서
    // 부두 경계가 깔끔한 직선이 되도록 한다(진짜 큰 모서리 형태는 이 허용치보다 훨씬 크므로 보존됨).
    const float BoundarySnapTolerance = 0.012f; // 정규화 좌표 기준(~110m)

    // pts: (nx, ny) 추출 좌표들. widthAxisIsX=true면 nx가 폭(실측 안벽길이) 축, false면 ny가 폭 축(DGT처럼 세로로 긴 부두).
    static List<Vector2> MapByWidth(Vector2[] pts, bool widthAxisIsX, float widthStart, float realWidth)
    {
        float wMin = float.MaxValue, wMax = float.MinValue;
        foreach (var p in pts)
        {
            float w = widthAxisIsX ? p.x : p.y;
            wMin = Mathf.Min(wMin, w); wMax = Mathf.Max(wMax, w);
        }
        var result = new List<Vector2>();
        foreach (var p in pts)
        {
            float w = widthAxisIsX ? p.x : p.y;
            if (w - wMin < BoundarySnapTolerance) w = wMin;
            else if (wMax - w < BoundarySnapTolerance) w = wMax;
            float f = (w - wMin) / (wMax - wMin);
            float widthCoord = widthStart + f * realWidth;
            if (widthAxisIsX)
                result.Add(new Vector2(widthCoord, Z(p.y)));
            else
                result.Add(new Vector2(p.x * 9000f, widthStart + (1f - f) * realWidth));
        }
        return result;
    }

    // 변 중 가장 긴 것을 안벽(квay)으로 간주하고 그 위에 선석을 균등 배치한다.
    static List<Berth> BerthsOnLongestEdge(List<Vector2> outline, params string[] ids)
    {
        int bestI = 0; float bestLen = -1f;
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
            float len = Vector2.Distance(a, b);
            if (len > bestLen) { bestLen = len; bestI = i; }
        }
        Vector2 qa = outline[bestI], qb = outline[(bestI + 1) % outline.Count];
        var list = new List<Berth>();
        int n = ids.Length;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            list.Add(new Berth { id = ids[i], pos = Vector2.Lerp(qa, qb, t) });
        }
        return list;
    }

    void BuildSampleData()
    {
        // ---- 북쪽 열: HJNC - PNC - PNIT ----
        float hjncX0 = 0f, hjncX1 = hjncX0 + 1100f;    // 안벽 1,100m
        float pncX0 = hjncX1, pncX1 = pncX0 + 2000f;   // 안벽 2,000m
        float pnitX0 = pncX1, pnitX1 = pnitX0 + 1200f; // 안벽 1,200m

        var hjncPts = new[] { new Vector2(0.2898f, 0.2777f), new Vector2(0.2839f, 0.2774f), new Vector2(0.2833f, 0.1967f), new Vector2(0.2654f, 0.1191f), new Vector2(0.2785f, 0.1006f), new Vector2(0.2739f, 0.0843f), new Vector2(0.2792f, 0.0736f), new Vector2(0.2944f, 0.0736f), new Vector2(0.3028f, 0.0573f), new Vector2(0.4450f, 0.0518f), new Vector2(0.4467f, 0.2703f) };
        var pncPts = new[] { new Vector2(0.4591f, 0.2703f), new Vector2(0.4529f, 0.2700f), new Vector2(0.4512f, 0.0514f), new Vector2(0.7570f, 0.0370f), new Vector2(0.7580f, 0.2570f) };
        var pnitPts = new[] { new Vector2(0.7707f, 0.2555f), new Vector2(0.7648f, 0.2552f), new Vector2(0.7632f, 0.0366f), new Vector2(0.9285f, 0.0292f), new Vector2(0.9659f, 0.0673f), new Vector2(0.9670f, 0.2467f) };

        var hjncOutline = MapByWidth(hjncPts, true, hjncX0, hjncX1 - hjncX0);
        terminals.Add(new Terminal { name = "HJNC", outline = hjncOutline, berths = BerthsOnLongestEdge(hjncOutline, "MS3-01", "MS3-02", "MS3-03", "MS3-04") });

        var pncOutline = MapByWidth(pncPts, true, pncX0, pncX1 - pncX0);
        terminals.Add(new Terminal { name = "PNC", outline = pncOutline, berths = BerthsOnLongestEdge(pncOutline, "MSN-04", "MSN-05", "MSN-06", "MSN-07", "MSN-08", "MSN-09") });

        var pnitOutline = MapByWidth(pnitPts, true, pnitX0, pnitX1 - pnitX0);
        terminals.Add(new Terminal { name = "PNIT", outline = pnitOutline, berths = BerthsOnLongestEdge(pnitOutline, "MSN-01", "MSN-02", "MSN-03") });

        // ---- 남쪽 열: BCT - BNCT - HPNT ----
        float bctX0 = 200f, bctX1 = bctX0 + 1050f;      // 안벽 1,050m
        float bnctX0 = bctX1, bnctX1 = bnctX0 + 1400f;  // 안벽 1,400m
        float hpntX0 = bnctX1, hpntX1 = hpntX0 + 1150f; // 안벽 1,150m

        var bctPts = new[] { new Vector2(0.4261f, 0.9811f), new Vector2(0.4079f, 0.7918f), new Vector2(0.5719f, 0.7064f), new Vector2(0.5869f, 0.8953f) };
        var bnctPts = new[] { new Vector2(0.5963f, 0.8865f), new Vector2(0.5793f, 0.7016f), new Vector2(0.7657f, 0.6028f), new Vector2(0.7826f, 0.7888f) };
        var hpntPts = new[] { new Vector2(0.7916f, 0.7874f), new Vector2(0.7732f, 0.5980f), new Vector2(0.9400f, 0.5070f), new Vector2(0.9684f, 0.5059f), new Vector2(0.9690f, 0.5436f), new Vector2(0.9635f, 0.6028f), new Vector2(0.9416f, 0.6490f), new Vector2(0.8595f, 0.7008f), new Vector2(0.8413f, 0.7230f), new Vector2(0.8168f, 0.7703f) };

        var bctOutline = MapByWidth(bctPts, true, bctX0, bctX1 - bctX0);
        terminals.Add(new Terminal { name = "BCT", outline = bctOutline, berths = BerthsOnLongestEdge(bctOutline, "MS6-01", "MS6-02", "MS6-03") });

        var bnctOutline = MapByWidth(bnctPts, true, bnctX0, bnctX1 - bnctX0);
        terminals.Add(new Terminal { name = "BNCT", outline = bnctOutline, berths = BerthsOnLongestEdge(bnctOutline, "MS5-01", "MS5-02", "MS5-03", "MS5-04") });

        var hpntOutline = MapByWidth(hpntPts, true, hpntX0, hpntX1 - hpntX0);
        terminals.Add(new Terminal { name = "HPNT", outline = hpntOutline, berths = BerthsOnLongestEdge(hpntOutline, "MS4-01", "MS4-02", "MS4-03") });

        // ---- DGT: 세로로 긴 독립 부두. 안벽길이(1,050m)가 세로(북남, ny) 축.
        // z 앵커는 0이 아니라 원본 이미지에서의 실제 위치(Z(ny))를 사용해야 북쪽 열(HJNC 등)과 같은 밴드에 위치한다.
        var dgtPts = new[] { new Vector2(0.0339f, 0.4834f), new Vector2(0.0289f, 0.4830f), new Vector2(0.0271f, 0.2559f), new Vector2(0.0138f, 0.1916f), new Vector2(0.0126f, 0.0710f), new Vector2(0.1124f, 0.0662f), new Vector2(0.1155f, 0.4786f) };
        float dgtNyMax = float.MinValue;
        foreach (var p in dgtPts) dgtNyMax = Mathf.Max(dgtNyMax, p.y);
        float dgtZ0 = Z(dgtNyMax); // ny가 가장 큰(이미지에서 가장 아래=남쪽) 점의 실제 Z 위치를 시작점으로 삼는다
        var dgtOutline = MapByWidth(dgtPts, false, dgtZ0, 1050f);
        terminals.Add(new Terminal { name = "DGT", outline = dgtOutline, berths = BerthsOnLongestEdge(dgtOutline, "MS7-01", "MS7-02", "MS7-03") });

        RecenterOn("PNC");
    }

    // 지정한 터미널의 좌하단 모서리(min x, min z)가 원점(0,0)에 오도록 전체 좌표를 이동
    void RecenterOn(string terminalName)
    {
        var anchor = terminals.Find(t => t.name == terminalName);
        if (anchor == null) return;

        float minX = float.MaxValue, minZ = float.MaxValue;
        foreach (var p in anchor.outline)
        {
            minX = Mathf.Min(minX, p.x);
            minZ = Mathf.Min(minZ, p.y);
        }

        var offset = new Vector2(minX, minZ);
        foreach (var t in terminals)
        {
            for (int i = 0; i < t.outline.Count; i++) t.outline[i] -= offset;
            foreach (var b in t.berths) b.pos -= offset;
        }
    }
}
