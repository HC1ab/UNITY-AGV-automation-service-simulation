using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EdgeData
{
    public string fromId;
    public string toId;
    public List<Vector3> waypoints = new List<Vector3>();
}
