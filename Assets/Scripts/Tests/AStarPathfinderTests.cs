// AStarPathfinderTests.cs — RoadGraph_TestYard.asset 에 의존하지 않고, 4~5노드짜리 임시 그래프로
// AStarPathfinder의 정확성을 검증하는 EditMode 유닛테스트.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AStarPathfinderTests
{
    // A - B - C
    //     |
    //     D - E
    static RoadGraphData BuildSmallGraph()
    {
        var graph = ScriptableObject.CreateInstance<RoadGraphData>();
        graph.nodes.Add(new NodeData { id = "A", position = new Vector3(0, 0, 0) });
        graph.nodes.Add(new NodeData { id = "B", position = new Vector3(10, 0, 0) });
        graph.nodes.Add(new NodeData { id = "C", position = new Vector3(20, 0, 0) });
        graph.nodes.Add(new NodeData { id = "D", position = new Vector3(10, 0, 10) });
        graph.nodes.Add(new NodeData { id = "E", position = new Vector3(10, 0, 20) });

        void AddEdge(string a, string b)
        {
            var na = graph.FindNode(a);
            var nb = graph.FindNode(b);
            graph.edges.Add(new EdgeData
            {
                fromId = a,
                toId = b,
                waypoints = new List<Vector3> { na.position, nb.position }
            });
        }

        AddEdge("A", "B");
        AddEdge("B", "C");
        AddEdge("B", "D");
        AddEdge("D", "E");
        return graph;
    }

    [Test]
    public void FindPath_ReturnsExpectedSequence_ForReachableNodes()
    {
        var graph = BuildSmallGraph();
        var path = AStarPathfinder.FindPath(graph, "A", "E");
        CollectionAssert.AreEqual(new[] { "A", "B", "D", "E" }, path);
    }

    [Test]
    public void FindPath_ReturnsSingleNode_WhenStartEqualsGoal()
    {
        var graph = BuildSmallGraph();
        var path = AStarPathfinder.FindPath(graph, "C", "C");
        CollectionAssert.AreEqual(new[] { "C" }, path);
    }

    [Test]
    public void FindPath_ReturnsNull_ForNonexistentNode()
    {
        var graph = BuildSmallGraph();
        var path = AStarPathfinder.FindPath(graph, "A", "Z");
        Assert.IsNull(path);
    }

    [Test]
    public void FindPath_ReturnsShorterRoute_NotViaDeadEnd()
    {
        var graph = BuildSmallGraph();
        var path = AStarPathfinder.FindPath(graph, "A", "C");
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, path);
    }

    [Test]
    public void BuildWaypointPath_ConcatenatesWithoutDuplicates()
    {
        var graph = BuildSmallGraph();
        var nodeIds = AStarPathfinder.FindPath(graph, "A", "E");
        var waypoints = PathBuilder.BuildWaypointPath(nodeIds, graph);

        Assert.AreEqual(4, waypoints.Count);
        Assert.AreEqual(graph.FindNode("A").position, waypoints[0]);
        Assert.AreEqual(graph.FindNode("E").position, waypoints[3]);
    }
}
