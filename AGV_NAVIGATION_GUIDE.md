# 야드-CFS AGV 내비게이션 시스템 — Unity 구현 가이드

설계 문서(RoadGraphData → AStarPathfinder → PathBuilder → AGVController → TrafficManager)를
실제 Unity 프로젝트로 옮기는 순서와 방법을 정리한다. 도로 좌표 자체는 추후 위성사진
트레이싱으로 채워 넣는다는 전제 하에, 지금은 구조와 워크플로우를 먼저 확정한다.

---

## 1. 폴더/에셋 구조

```
Assets/
  Scripts/
    Data/
      NodeData.cs
      EdgeData.cs
      RoadGraphData.cs
    Pathfinding/
      AStarPathfinder.cs
      PathBuilder.cs
    Agent/
      AGVController.cs
      AGVStateMachine.cs        (Idle/Moving/WaitingAtIntersection/Charging)
    Traffic/
      TrafficManager.cs
    Editor/
      RoadGraphEditorWindow.cs  (그래프 저작 도구 진입점)
      RoadGraphSceneGUI.cs      (Scene 뷰 핸들/기즈모)
    Tests/
      AStarPathfinderTests.cs   (EditMode 유닛테스트)
  ScriptableObjects/
    RoadGraph_TestYard.asset    ← 1단계용 가짜 그래프 (노드 4~5개)
    RoadGraph_Block1.asset      ← 이후 실제 블록 단위로 분리 저장
  Prefabs/
    AGV.prefab
    Node_Gizmo.prefab           (선택: 씬 뷰 보조용)
  Scenes/
    00_GraphAuthoring.unity     (그래프 저작 전용, AGV 없음)
    01_PathfindingTest.unity    (경로 탐색 콘솔 출력만)
    02_SingleAGV.unity          (AGV 1대, 트래픽 매니저 없음)
    03_MultiAGV_Traffic.unity   (다중 AGV + 교차로 예약)
    04_RL_Hook.unity            (ML-Agents 연동, 맨 마지막)
```

씬을 단계별로 분리해두는 이유는 단순하다 — 나중에 문제가 생겼을 때 "그래프가 잘못됐나 /
경로탐색이 잘못됐나 / 추종이 잘못됐나 / 교통조정이 잘못됐나"를 씬 단위로 바로 격리해서
확인할 수 있게 하기 위함이다.

---

## 2. 구현 순서 (바텀업, 5단계)

각 단계는 **이전 단계가 눈으로 검증된 뒤에만** 다음으로 넘어간다. 5개 컴포넌트를 한 번에
연결하고 처음 실행하면 실패 지점을 특정하기 어렵다.

### 2-1. 그래프 저작 (00_GraphAuthoring)

- 코드보다 먼저 준비할 것: 최소 4~5개 노드, 노드 하나는 분기(교차로) 역할.
- Scene 뷰에서 노드를 배치하고 엣지를 곡선으로 잇는다 (3장 참고).
- **완료 기준**: `RoadGraph_TestYard.asset`이 저장되고, Scene 뷰에서 노드=구, 엣지=선으로
  항상 렌더링됨.

### 2-2. 경로 탐색 단독 테스트 (01_PathfindingTest)

- AGV, Pure Pursuit, TrafficManager는 아직 등장하지 않는다.
- 빈 씬에 버튼(또는 `[ContextMenu]`) 하나만 두고 `AStarPathfinder.FindPath(graph, startId, goalId)`
  결과를 콘솔에 노드 ID 시퀀스로 출력.
- 가능하면 `Tests/AStarPathfinderTests.cs`에 EditMode 유닛테스트로도 고정 — 그래프를
  나중에 실제 좌표로 갈아끼워도 회귀 테스트로 계속 돌릴 수 있게.
- **완료 기준**: 의도한 노드 순서가 정확히 나옴. 존재하지 않는 노드 요청 시 `null` 반환
  확인 (예외로 죽지 않음).

### 2-3. 웨이포인트 시각화 (여전히 01 씬, AGV 없이)

- `PathBuilder.BuildWaypointPath(nodeSequence, graph)` 결과를 `OnDrawGizmos`로 선 그리기.
- **완료 기준**: 그려진 선이 그래프의 실제 곡선(엣지 웨이포인트)을 그대로 따라감 — 노드
  사이를 직선으로 지름길 내지 않는지 확인.

### 2-4. AGV 1대 — Kinematic 이동만 (02_SingleAGV)

- 큐브 또는 임시 프리팹에 `AGVController` 부착, `SetPath()`로 2-3단계 경로 주입.
- TrafficManager는 아직 연결하지 않는다.
- **완료 기준**: 곡선 구간에서 튀거나 진동하지 않고 자연스럽게 회전. 웨이포인트 통과 시
  스냅이 눈에 띄게 덜컹거리지 않는지 확인 (덜컹거리면 `waypointSnapThreshold` 또는
  `lookaheadDistance` 튜닝).

### 2-5. 다중 AGV + TrafficManager (03_MultiAGV_Traffic)

- AGV 2~3대를 동일 교차로 노드로 동시 진입시키는 시나리오를 의도적으로 구성.
- **완료 기준**: 교착(deadlock) 없이 한쪽이 양보하고 통과. `TryReserve`/`Release` 호출
  로그로 순서 확인 가능해야 함.

### 2-6. RL 훅 (04_RL_Hook, 맨 마지막)

- 2-5까지 안정된 뒤에만 착수. 처음부터 RL까지 얹으면 버그가 어느 계층 문제인지
  구분이 안 됨.
- `RequestDecision()` 호출 지점만 우선 넣고, 실제 정책은 규칙 기반 폴백으로 대체해
  구조만 먼저 검증.

### 진행 체크리스트

| 단계 | 씬 | 완료 기준 | 상태 |
|---|---|---|---|
| 그래프 저작 | 00 | 노드/엣지 저장 + Scene 뷰 상시 렌더링 | ☐ |
| 경로 탐색 | 01 | 노드 시퀀스 콘솔 출력 정확 | ☐ |
| 웨이포인트 시각화 | 01 | Gizmo 선이 곡선 그대로 추종 | ☐ |
| AGV 단독 이동 | 02 | 곡선 주행 자연스러움 | ☐ |
| 다중 AGV 교통조정 | 03 | 교착 없이 순차 통과 | ☐ |
| RL 훅 | 04 | 훅 지점 동작, 정책은 폴백 | ☐ |

---

## 3. 그래프 저작 도구 (가장 중요한 부분)

노드 수십 개, 웨이포인트 수백 개를 Inspector에 `Vector3`로 일일이 타이핑하는 건 현실적으로
불가능하다. 커스텀 에디터로 Scene 뷰에서 직접 배치하는 워크플로우를 만든다.

### 3-1. 기능 요구사항

- Scene 뷰 클릭 → 노드 생성 (`RoadGraphData.nodes`에 추가, 지형 위 스냅)
- 노드 두 개 순서대로 선택 → 엣지 생성
- 엣지 생성 시 컨트롤 포인트를 드래그해 곡선 웨이포인트를 실시간으로 찍음
  (직선 2점이 아니라 여러 점을 촘촘히 샘플링해서 `EdgeData.waypoints`에 저장)
- `OnSceneGUI`에서 노드=구(Handles.SphereHandleCap), 엣지=선(Handles.DrawAAPolyLine)으로
  **항상** 렌더링 — 저작 중이 아니어도 그래프 전체가 계속 보여야 함
- 노드/엣지 삭제, ID 재부여 기능
- 저장은 `EditorUtility.SetDirty(graph)` + `AssetDatabase.SaveAssets()`로 즉시 반영

### 3-2. 코드 골격 (RoadGraphSceneGUI.cs)

```csharp
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoadGraphData))]
public class RoadGraphSceneGUI : Editor {
    RoadGraphData graph;
    int selectedNodeIndex = -1;

    void OnEnable() {
        graph = (RoadGraphData)target;
    }

    void OnSceneGUI() {
        // 노드는 항상 그리기
        for (int i = 0; i < graph.nodes.Count; i++) {
            var node = graph.nodes[i];
            Handles.color = i == selectedNodeIndex ? Color.yellow : Color.cyan;
            if (Handles.Button(node.position, Quaternion.identity, 1f, 1f, Handles.SphereHandleCap)) {
                selectedNodeIndex = i;
                Repaint();
            }
            Handles.Label(node.position + Vector3.up, node.id);
        }

        // 엣지는 항상 선으로 그리기
        Handles.color = Color.white;
        foreach (var edge in graph.edges) {
            if (edge.waypoints != null && edge.waypoints.Count > 1)
                Handles.DrawAAPolyLine(4f, edge.waypoints.ToArray());
        }

        // Ctrl+클릭으로 새 노드 생성 (지면 레이캐스트)
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.control && e.button == 0) {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out var hit)) {
                Undo.RecordObject(graph, "Add Node");
                graph.nodes.Add(new NodeData {
                    id = $"N{graph.nodes.Count}",
                    position = hit.point
                });
                EditorUtility.SetDirty(graph);
                e.Use();
            }
        }
    }
}
```

위 골격에 엣지 생성(두 노드 선택 후 컨트롤 포인트 드래그) 로직을 이어 붙이면 된다.
이 부분은 반복적인 GUI 코드가 많아 MCP에 맡기기 좋은 영역이다 (5장 참고).

### 3-3. 재사용성

이 도구가 갖춰지면, 지금은 임의 배치한 테스트 그래프를 나중에 위성사진 트레이싱 좌표로
갈아끼울 때도 **같은 워크플로우**를 그대로 쓴다. 좌표를 코드로 하드코딩하는 게 아니라
에디터에서 시각적으로 배치 → 저장하는 흐름 자체가 핵심이며, 이번 단계에서 도구를
제대로 만들어두면 이후 실측 좌표 반영이 순수 데이터 교체 작업으로 끝난다.

---

## 4. AGV 프리팹 구조

```
AGV (빈 오브젝트)
 ├─ AGVController.cs   ← Transform 직접 제어 (부모가 담당)
 ├─ AGVStateMachine.cs ← Idle/Moving/WaitingAtIntersection/Charging
 └─ Visual (자식 오브젝트, 순수 표시용)
      └─ Meshy AI로 생성한 AGV 3D 모델 (회전/스케일만 조정)
```

- **Transform 제어와 시각 표현을 분리**하는 이유: 나중에 WheelCollider나
  ConfigurableJoint 같은 물리 기반 이동으로 확장할 때, 부모(로직)를 건드리지 않고
  자식(Visual)만 물리 바디로 교체하거나 병행 가능.
- `Visual` 하위에 `WheelVisual`(바퀴 회전 표시), `CraneController`(적재 시각 효과) 등을
  추가로 붙여도 로직 계층과 독립적으로 동작.

---

## 5. MCP(Claude Code)에 맡길 것 vs 직접 확인할 것

| 구분 | 내용 |
|---|---|
| **맡기기 좋음** | `Data`/`Pathfinding`/`Agent`/`Traffic` 스크립트 초안 생성 및 리팩토링, `AStarPathfinderTests.cs` 유닛테스트 작성, Editor GUI의 반복적인 코드(버튼 배치, 리스트 렌더링 등), `RoadGraphSceneGUI`의 엣지 생성/삭제 로직 확장 |
| **직접 확인 필요** | Scene 뷰에서 그래프가 의도대로 그려지는지, AGV가 곡선에서 부자연스럽게 튀는지, 다중 AGV 교착 발생 여부 — 스크린샷을 찍어 "여기서 이렇게 어긋나는데 고쳐줘" 식으로 피드백 루프를 도는 게 가장 빠름 |

### 권장 작업 흐름

1. 이 문서 + 이전 대화의 기술 설계 문서를 MCP 컨텍스트로 전달
2. 2장의 단계 순서대로 한 단계씩 요청 (한 번에 전체를 요청하지 않기)
3. 각 단계 완료 후 Unity 에디터에서 직접 실행 → 스크린샷/로그로 검증
4. 문제 발생 시 어느 씬(00~04)에서 발생했는지 먼저 특정한 뒤 MCP에 전달

---

## 6. 다음 결정 필요 사항

- `RoadGraph_TestYard.asset`의 테스트용 4~5노드 좌표를 지금 임의로 잡을지, 아니면
  1블록 분량만 먼저 위성사진에서 트레이싱해서 넣을지
- `AGVStateMachine`의 상태 전이 조건(예: Charging 진입 배터리 임계값)을 몇 %로 둘지
- Editor 도구에서 엣지 곡선 컨트롤 포인트를 몇 개까지 허용할지 (너무 많으면 저작 시간
  증가, 너무 적으면 실제 도로 곡률 재현 어려움 — 우선 엣지당 5~8점 권장)
