// AGVController.cs — RoadGraphData 기반 웨이포인트를 순서대로 따라가는 단순 Kinematic 이동.
// Transform 제어(이 컴포넌트)와 시각 표현(Visual 자식)을 분리한 구조를 전제로 한다
// (AGV_NAVIGATION_GUIDE.md 4장). 이동 방향은 XZ 평면 기준, Y는 스폰 시점 높이를 그대로 유지한다.
using System.Collections.Generic;
using UnityEngine;

public class AGVController : MonoBehaviour
{
    [Tooltip("이동 속도 (m/s)")]
    public float moveSpeed = 2f;

    [Tooltip("회전 속도 (deg/s)")]
    public float rotationSpeed = 90f;

    [Tooltip("웨이포인트 도달 판정 거리 (m)")]
    public float waypointReachedThreshold = 1f;

    List<Vector3> path;
    int currentIndex;
    float fixedY;
    bool hasFixedY;

    void Awake()
    {
        fixedY = transform.position.y;
        hasFixedY = true;
    }

    public void SetPath(List<Vector3> waypoints)
    {
        path = waypoints;
        currentIndex = 0;
        if (!hasFixedY)
        {
            fixedY = transform.position.y;
            hasFixedY = true;
        }
    }

    // 출발 노드로 즉시 순간이동 (Y는 기존 높이 유지). 경로 시작 전 호출해서
    // 현재 위치와 출발 노드가 다를 때 도로를 무시하고 가로지르는 것을 방지한다.
    public void TeleportTo(Vector3 position)
    {
        if (!hasFixedY)
        {
            fixedY = transform.position.y;
            hasFixedY = true;
        }
        position.y = fixedY;
        transform.position = position;
    }

    public bool IsMoving => path != null && currentIndex < path.Count;

    void Update()
    {
        if (path == null || currentIndex >= path.Count) return;

        Vector3 target = path[currentIndex];
        target.y = fixedY;

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= waypointReachedThreshold)
        {
            currentIndex++;
            return;
        }

        Vector3 direction = toTarget.normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        Vector3 move = direction * moveSpeed * Time.deltaTime;
        if (move.magnitude > distance) move = direction * distance;
        transform.position += move;
    }
}
