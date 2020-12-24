using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node
{
    public Vector3 worldPosition { get; }
    public Vector2Int gridIndex { get; }
    public byte cost;
    public ushort integration;
    public List<Node> neighbors;
    
    public Node(Vector3 worldPosition, Vector2Int gridIndex)
    {
        this.worldPosition = worldPosition;
        this.gridIndex = gridIndex;
        neighbors = new List<Node>();
    }
}
