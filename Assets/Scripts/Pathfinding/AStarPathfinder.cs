// AStarPathfinder.cs — RoadGraphData 위에서 두 노드 사이 최단 경로(노드 ID 시퀀스)를 찾는다.
// 엣지는 fromId->toId 한 방향으로만 저장돼 있지만 도로는 양방향 주행 가능하므로 양방향 인접으로 취급한다.
using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinder
{
    class Neighbor
    {
        public string nodeId;
        public float cost;
    }

    public static List<string> FindPath(RoadGraphData graph, string startId, string goalId)
    {
        if (graph == null) return null;
        var startNode = graph.FindNode(startId);
        var goalNode = graph.FindNode(goalId);
        if (startNode == null || goalNode == null) return null;

        if (startId == goalId) return new List<string> { startId };

        var adjacency = BuildAdjacency(graph);

        var openSet = new List<string> { startId };
        var cameFrom = new Dictionary<string, string>();
        var gScore = new Dictionary<string, float> { [startId] = 0f };
        var fScore = new Dictionary<string, float> { [startId] = Heuristic(startNode, goalNode) };

        while (openSet.Count > 0)
        {
            string current = LowestFScore(openSet, fScore);
            if (current == goalId) return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            if (!adjacency.TryGetValue(current, out var neighbors)) continue;

            foreach (var neighbor in neighbors)
            {
                float tentativeG = gScore[current] + neighbor.cost;
                if (!gScore.TryGetValue(neighbor.nodeId, out var existingG) || tentativeG < existingG)
                {
                    cameFrom[neighbor.nodeId] = current;
                    gScore[neighbor.nodeId] = tentativeG;
                    fScore[neighbor.nodeId] = tentativeG + Heuristic(graph.FindNode(neighbor.nodeId), goalNode);
                    if (!openSet.Contains(neighbor.nodeId)) openSet.Add(neighbor.nodeId);
                }
            }
        }

        return null; // 도달 불가능
    }

    static Dictionary<string, List<Neighbor>> BuildAdjacency(RoadGraphData graph)
    {
        var adjacency = new Dictionary<string, List<Neighbor>>();
        void AddDirected(string fromId, string toId, float cost)
        {
            if (!adjacency.TryGetValue(fromId, out var list))
            {
                list = new List<Neighbor>();
                adjacency[fromId] = list;
            }
            list.Add(new Neighbor { nodeId = toId, cost = cost });
        }

        foreach (var edge in graph.edges)
        {
            var a = graph.FindNode(edge.fromId);
            var b = graph.FindNode(edge.toId);
            if (a == null || b == null) continue;
            float cost = Vector3.Distance(a.position, b.position);
            AddDirected(edge.fromId, edge.toId, cost);
            AddDirected(edge.toId, edge.fromId, cost);
        }
        return adjacency;
    }

    static float Heuristic(NodeData a, NodeData b) => Vector3.Distance(a.position, b.position);

    static string LowestFScore(List<string> openSet, Dictionary<string, float> fScore)
    {
        string best = openSet[0];
        float bestScore = fScore.TryGetValue(best, out var s) ? s : float.MaxValue;
        for (int i = 1; i < openSet.Count; i++)
        {
            float score = fScore.TryGetValue(openSet[i], out var sc) ? sc : float.MaxValue;
            if (score < bestScore) { best = openSet[i]; bestScore = score; }
        }
        return best;
    }

    static List<string> ReconstructPath(Dictionary<string, string> cameFrom, string current)
    {
        var path = new List<string> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Insert(0, current);
        }
        return path;
    }
}
