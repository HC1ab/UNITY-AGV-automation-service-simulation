// PathBuilder.cs — 노드 ID 시퀀스를 실제 이동에 쓸 Vector3 웨이포인트 목록으로 변환한다.
using System.Collections.Generic;
using UnityEngine;

public static class PathBuilder
{
    public static List<Vector3> BuildWaypointPath(List<string> nodeIds, RoadGraphData graph)
    {
        if (nodeIds == null || nodeIds.Count == 0 || graph == null) return null;

        var waypoints = new List<Vector3>();
        var firstNode = graph.FindNode(nodeIds[0]);
        if (firstNode == null) return null;
        waypoints.Add(firstNode.position);

        for (int i = 0; i < nodeIds.Count - 1; i++)
        {
            var edge = FindEdge(graph, nodeIds[i], nodeIds[i + 1]);
            if (edge == null) return null;

            bool forward = edge.fromId == nodeIds[i];
            var segment = forward ? edge.waypoints : Reversed(edge.waypoints);

            // segment[0]은 이미 이전 구간의 마지막 점(현재 노드)과 같으므로 중복 추가하지 않는다.
            for (int j = 1; j < segment.Count; j++)
                waypoints.Add(segment[j]);
        }

        return waypoints;
    }

    static EdgeData FindEdge(RoadGraphData graph, string a, string b)
    {
        foreach (var edge in graph.edges)
        {
            if ((edge.fromId == a && edge.toId == b) || (edge.fromId == b && edge.toId == a))
                return edge;
        }
        return null;
    }

    static List<Vector3> Reversed(List<Vector3> points)
    {
        var copy = new List<Vector3>(points);
        copy.Reverse();
        return copy;
    }
}
