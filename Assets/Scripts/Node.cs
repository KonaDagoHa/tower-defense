using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node
{
    public Vector3 worldPosition { get; }
    public Vector2Int gridIndex { get; }
    public byte cost;
    public ushort integration;
    public List<Node> neighborsCardinal = new List<Node>();
    public List<Node> neighborsIntercardinal = new List<Node>();
    public Vector3 flowDirection = Vector3.zero;

    private readonly Vector2Int[] neighborDirectionsCardinal = {
        new Vector2Int(0, 1), // north
        new Vector2Int(1, 0), // east
        new Vector2Int(0, -1), // south
        new Vector2Int(-1, 0) //west
    };

    private readonly Vector2Int[] neighborDirectionsIntercardinal = {
        new Vector2Int(1, 1), // northeast
        new Vector2Int(1, -1), // southeast
        new Vector2Int( -1, -1), // southwest
        new Vector2Int(-1, 1) // northwest
    };
    
    public Node(Vector3 worldPosition, Vector2Int gridIndex)
    {
        this.worldPosition = worldPosition;
        this.gridIndex = gridIndex;
    }
    
    public void UpdateNeighbors(Node[,] grid, Vector2Int gridSize)
    {
        // find north, east, south, and west neighbors of node and add to corresponding list
        foreach (Vector2Int direction in neighborDirectionsCardinal)
        {
            Vector2Int neighborIndex = gridIndex + direction;
            if (neighborIndex.x >= 0 && neighborIndex.x < gridSize.x &&
                neighborIndex.y >= 0 && neighborIndex.y < gridSize.y)
            {
                neighborsCardinal.Add(grid[neighborIndex.x, neighborIndex.y]);
            }
        }

        // find northeast, northwest, southwest, and northwest neighbors and add to corresponding list
        foreach (Vector2Int direction in neighborDirectionsIntercardinal)
        {
            Vector2Int neighborIndex = gridIndex + direction;
            if (neighborIndex.x >= 0 && neighborIndex.x < gridSize.x &&
                neighborIndex.y >= 0 && neighborIndex.y < gridSize.y)
            {
                neighborsIntercardinal.Add(grid[neighborIndex.x, neighborIndex.y]);
            }
        }
    }

    public void UpdateFlowDirection()
    {
        Node lowestIntegrationNeighbor = this;
        foreach (Node neighbor in neighborsCardinal)
        {
            if (neighbor.integration < lowestIntegrationNeighbor.integration)
            {
                lowestIntegrationNeighbor = neighbor;
            }
        }
        foreach (Node neighbor in neighborsIntercardinal)
        {
            if (neighbor.integration < lowestIntegrationNeighbor.integration)
            {
                lowestIntegrationNeighbor = neighbor;
            }
        }

        flowDirection = (lowestIntegrationNeighbor.worldPosition - worldPosition).normalized;
    }
}
