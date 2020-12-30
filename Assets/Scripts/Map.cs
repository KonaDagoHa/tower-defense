using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Map : MonoBehaviour
{
    // map
    public Vector2 mapSize = new Vector2(50, 50);
    private Vector3 mapBottomLeft => transform.position - Vector3.right * mapSize.x / 2 - Vector3.forward * mapSize.y / 2;
    
    // grid
    public float nodeRadius = 0.5f;
    public float nodeDiameter => nodeRadius * 2;
    public Node[,] grid { get; private set; }
    public Vector2Int gridSize { get; private set; }

    private void Start()
    {
        CreateGrid();
    }

    public Node WorldToNode(Vector3 worldPosition)
    {
        // percentages of worldPosition from bottom left corner to top right corner of grid
        // percentX = 0 and percentY = 0 is bottom left corner
        // percentX = 1 and percentY = 1 is top right corner
        float percentX = Mathf.Clamp01((worldPosition.x + mapSize.x / 2) / mapSize.x);
        float percentY = Mathf.Clamp01((worldPosition.z + mapSize.y / 2) / mapSize.y);

        // indices of grid
        int x = Mathf.RoundToInt((gridSize.x - 1) * percentX);
        int y = Mathf.RoundToInt((gridSize.y - 1) * percentY);

        return grid[x, y];
    }
    
    private void CreateGrid()
    {
        // grid will be centered on the map, but will not necessarily match size of map
        gridSize = new Vector2Int(
            Mathf.CeilToInt(mapSize.x / nodeDiameter),
            Mathf.CeilToInt(mapSize.y / nodeDiameter)
        );
        grid = new Node[gridSize.x, gridSize.y];
        // populate grid with nodes
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                // position of the node
                Vector3 position = mapBottomLeft + new Vector3(
                    x * nodeDiameter + nodeRadius,
                    0,
                    y * nodeDiameter + nodeRadius
                );
                grid[x, y] = new Node(position, new Vector2Int(x, y));
            }
        }

        // after grid is populated, each node will store references to its neighbors
        foreach (Node node in grid)
        {
            node.UpdateNeighbors(grid, gridSize);
        }
    }
}

