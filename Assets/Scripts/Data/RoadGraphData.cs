using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoadGraph", menuName = "AGV/Road Graph Data")]
public class RoadGraphData : ScriptableObject
{
    public List<NodeData> nodes = new List<NodeData>();
    public List<EdgeData> edges = new List<EdgeData>();

    public NodeData FindNode(string id)
    {
        return nodes.Find(n => n.id == id);
    }
}
