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

    // 픽셀 대신 정규화 좌표(0~1, 왼쪽위 원점)로 지정 — 이미지 실제 해상도에 의존하지 않음
    // 1 유닛 = 1미터(CLAUDE.md 기준). 안벽 합계 실측치(약 8,950m, 근사 9,000m)를 이미지 전체 폭 기준으로 매핑.
    const float WorldW = 9000f;
    const float WorldH = 9000f * (44f / 100f); // 이미지 비율(약 2.27:1) 유지 = 3960m
    static Vector2 N(float nx, float ny) => new Vector2(nx * WorldW, (1f - ny) * WorldH);

    void BuildSampleData()
    {
        terminals.Add(new Terminal
        {
            name = "DGT",
            outline = new List<Vector2> {
                N(0.00f,0.05f), N(0.09f,0.02f), N(0.115f,0.10f),
                N(0.115f,0.30f), N(0.08f,0.46f), N(0.00f,0.40f)
            },
            berths = new List<Berth> {
                new Berth{ id="MS7-01", pos=N(0.11f,0.14f) },
                new Berth{ id="MS7-02", pos=N(0.11f,0.24f) },
                new Berth{ id="MS7-03", pos=N(0.10f,0.34f) },
            }
        });

        terminals.Add(new Terminal
        {
            name = "HJNC",
            outline = new List<Vector2> {
                N(0.165f,0.03f), N(0.280f,0.03f), N(0.280f,0.27f), N(0.165f,0.27f)
            },
            berths = new List<Berth> {
                new Berth{ id="MS3-01", pos=N(0.185f,0.27f) },
                new Berth{ id="MS3-02", pos=N(0.21f,0.27f) },
                new Berth{ id="MS3-03", pos=N(0.235f,0.27f) },
                new Berth{ id="MS3-04", pos=N(0.26f,0.27f) },
            }
        });

        terminals.Add(new Terminal
        {
            name = "PNC",
            outline = new List<Vector2> {
                N(0.280f,0.03f), N(0.610f,0.03f), N(0.610f,0.27f), N(0.280f,0.27f)
            },
            berths = new List<Berth> {
                new Berth{ id="MSN-04", pos=N(0.32f,0.27f) },
                new Berth{ id="MSN-05", pos=N(0.375f,0.27f) },
                new Berth{ id="MSN-06", pos=N(0.43f,0.27f) },
                new Berth{ id="MSN-07", pos=N(0.485f,0.27f) },
                new Berth{ id="MSN-08", pos=N(0.54f,0.27f) },
                new Berth{ id="MSN-09", pos=N(0.585f,0.27f) },
            }
        });

        terminals.Add(new Terminal
        {
            name = "PNIT",
            outline = new List<Vector2> {
                N(0.610f,0.03f), N(0.93f,0.00f), N(0.965f,0.05f), N(0.965f,0.27f), N(0.610f,0.27f)
            },
            berths = new List<Berth> {
                new Berth{ id="MSN-01", pos=N(0.66f,0.27f) },
                new Berth{ id="MSN-02", pos=N(0.78f,0.27f) },
                new Berth{ id="MSN-03", pos=N(0.90f,0.27f) },
            }
        });

        terminals.Add(new Terminal
        {
            name = "BCT",
            outline = new List<Vector2> {
                N(0.285f,0.68f), N(0.435f,0.60f), N(0.455f,0.90f), N(0.30f,0.98f)
            },
            berths = new List<Berth> {
                new Berth{ id="MS6-01", pos=N(0.32f,0.68f) },
                new Berth{ id="MS6-02", pos=N(0.365f,0.65f) },
                new Berth{ id="MS6-03", pos=N(0.41f,0.62f) },
            }
        });

        terminals.Add(new Terminal
        {
            name = "BNCT",
            outline = new List<Vector2> {
                N(0.435f,0.60f), N(0.575f,0.55f), N(0.595f,0.83f), N(0.455f,0.90f)
            },
            berths = new List<Berth> {
                new Berth{ id="MS5-01", pos=N(0.46f,0.62f) },
                new Berth{ id="MS5-02", pos=N(0.495f,0.60f) },
                new Berth{ id="MS5-03", pos=N(0.53f,0.585f) },
                new Berth{ id="MS5-04", pos=N(0.565f,0.565f) },
            }
        });

        terminals.Add(new Terminal
        {
            name = "HPNT",
            outline = new List<Vector2> {
                N(0.575f,0.55f), N(0.83f,0.45f), N(0.965f,0.62f), N(0.90f,0.78f), N(0.595f,0.83f)
            },
            berths = new List<Berth> {
                new Berth{ id="MS4-01", pos=N(0.63f,0.55f) },
                new Berth{ id="MS4-02", pos=N(0.72f,0.51f) },
                new Berth{ id="MS4-03", pos=N(0.81f,0.475f) },
            }
        });

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
