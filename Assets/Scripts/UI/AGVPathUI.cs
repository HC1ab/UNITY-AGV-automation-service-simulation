// AGVPathUI.cs — 드롭다운 2개(출발/도착 노드) + 버튼으로 AGVController에 경로를 주입하는 UI Toolkit 컨트롤러.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class AGVPathUI : MonoBehaviour
{
    public RoadGraphData graph;
    public AGVController agv;

    DropdownField startDropdown;
    DropdownField goalDropdown;
    Button goButton;
    Label statusLabel;

    bool bound;

    void OnEnable()
    {
        BindElements();
    }

    // OnEnable 시점에 UIDocument가 아직 UXML을 파싱하지 않았다면 BindElements가 실패할 수 있어,
    // 바인딩될 때까지 매 프레임 재시도한다 (실 사용자 클릭이 goButton.clicked에 정상적으로
    // 연결되도록 보장하기 위함 — 한 번 성공하면 더 이상 아무 일도 하지 않는다).
    void Update()
    {
        if (!bound) BindElements();
    }

    void OnDisable()
    {
        if (goButton != null) goButton.clicked -= OnGoClicked;
        bound = false;
    }

    // UIDocument가 UXML을 파싱해 실제 엘리먼트를 만드는 시점이 이 컴포넌트의 OnEnable보다
    // 늦을 수 있어(스크립트 실행 순서 비보장), OnEnable에서 실패하면 버튼 클릭 시점에 다시 시도한다.
    void BindElements()
    {
        if (bound) return;

        var root = GetComponent<UIDocument>().rootVisualElement;
        var start = root.Q<DropdownField>("start-dropdown");
        var goal = root.Q<DropdownField>("goal-dropdown");
        var go = root.Q<Button>("go-button");
        var status = root.Q<Label>("status-label");
        if (start == null || goal == null || go == null || status == null) return; // 아직 준비 안 됨

        startDropdown = start;
        goalDropdown = goal;
        goButton = go;
        statusLabel = status;

        if (graph != null)
        {
            var ids = graph.nodes.Select(n => n.id).ToList();
            startDropdown.choices = ids;
            goalDropdown.choices = ids;
            if (ids.Count > 0)
            {
                startDropdown.value = ids[0];
                goalDropdown.value = ids[ids.Count - 1];
            }
        }

        goButton.clicked += OnGoClicked;
        bound = true;
    }

    void OnGoClicked()
    {
        BindElements();
        if (graph == null || agv == null)
        {
            SetStatus("graph 또는 agv 참조가 비어 있습니다.");
            return;
        }

        string startId = startDropdown.value;
        string goalId = goalDropdown.value;

        var nodeIds = AStarPathfinder.FindPath(graph, startId, goalId);
        if (nodeIds == null)
        {
            SetStatus($"경로를 찾을 수 없습니다 ({startId} → {goalId}).");
            return;
        }

        var waypoints = PathBuilder.BuildWaypointPath(nodeIds, graph);
        if (waypoints == null)
        {
            SetStatus("웨이포인트 생성에 실패했습니다.");
            return;
        }

        agv.TeleportTo(waypoints[0]);
        agv.SetPath(waypoints);
        SetStatus($"이동 중: {string.Join(" → ", nodeIds)}");
    }

    void SetStatus(string text)
    {
        if (statusLabel != null) statusLabel.text = text;
    }
}
