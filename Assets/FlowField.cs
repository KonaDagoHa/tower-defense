using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class FlowField : MonoBehaviour
{
    [SerializeField] private Map map;
    [SerializeField] private LayerMask defaultTerrain;
    [SerializeField] private LayerMask impassableTerrain;
    [SerializeField] private LayerMask roughTerrain;
    [SerializeField] private float nodeRadius = 0.5f;
    
    private Vector2Int gridSize;
    private Node[,] grid;
    private float nodeDiameter => nodeRadius * 2;

    private static class NodeCost
    {
        public const byte defaultTerrain = 1;
        public const byte impassableTerrain = byte.MaxValue;
        public const byte roughTerrain = 4;
    }
    
    private readonly Vector2Int[] neighborDirections = {
        new Vector2Int(0, 1), // north
        new Vector2Int(1, 0), // east
        new Vector2Int(0, -1), // south
        new Vector2Int(-1, 0),  //west
    };

    public Node NodeFromWorldPosition(Vector3 worldPosition)
    {
        // percentages of worldPosition from bottom left corner to top right corner of grid
            // percentX = 0 and percentY = 0 is bottom left corner
            // percentX = 1 and percentY = 1 is top right corner
        float percentX = Mathf.Clamp01((worldPosition.x + map.size.x / 2) / map.size.x);
        float percentY = Mathf.Clamp01((worldPosition.y + map.size.y / 2) / map.size.y);

        // indices of grid
        int x = Mathf.RoundToInt((gridSize.x - 1) * percentX);
        int y = Mathf.RoundToInt((gridSize.y - 1) * percentY);

        return grid[x, y];
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                CreateGrid();
                CreateCostField();
                CreateIntegrationField(hit.point);
                Debug.Log(hit.point);
            }
            
        }
    }

    private void UpdateNeighbors(Node node)
    {
        // find north, east, south, and west neighbors of node and add to node's neighbors list
        foreach (Vector2Int direction in neighborDirections)
        {
            Vector2Int neighborIndex = node.gridIndex + direction;
            if (neighborIndex.x >= 0 && neighborIndex.x < gridSize.x &&
                neighborIndex.y >= 0 && neighborIndex.y < gridSize.y)
            {
                node.neighbors.Add(grid[neighborIndex.x, neighborIndex.y]);
            }
        }
    }

    private void CreateGrid()
    {
        // grid will be centered on the map, but will not necessarily match size of map
        gridSize = new Vector2Int(
            Mathf.RoundToInt(map.size.x / nodeDiameter),
            Mathf.RoundToInt(map.size.y / nodeDiameter)
        );
        grid = new Node[gridSize.x, gridSize.y];
        Vector3 mapBottomLeft = map.bottomLeft;
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
            UpdateNeighbors(node);
        }
    }

    private void CreateCostField()
    {
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Node node = grid[x, y];
                // check for objects that would overlap the node at (x, y) on specific layers
                if (Physics.CheckSphere(node.worldPosition, nodeRadius, impassableTerrain))
                {
                    // node is impassable
                    node.cost = NodeCost.impassableTerrain;
                }
                else if (Physics.CheckSphere(node.worldPosition, nodeRadius, roughTerrain))
                {
                    // node is rough
                    node.cost = NodeCost.roughTerrain;
                }
                else
                {
                    // node is default
                    node.cost = NodeCost.defaultTerrain;
                }
            }
        }
    }

    private void CreateIntegrationField(Vector3 targetPosition)
    {
        // reset integration values of all nodes
        foreach (Node node in grid)
        {
            node.integration = ushort.MaxValue;
        }
        // set cost and integration of targetNode to 0
        Node targetNode = NodeFromWorldPosition(targetPosition);
        targetNode.cost = 0;
        targetNode.integration = 0;
        // create queue of open nodes and add target node to it
        Queue<Node> openNodes = new Queue<Node>();
        openNodes.Enqueue(targetNode);

        while (openNodes.Count > 0)
        {
            Node currentNode = openNodes.Dequeue();
            foreach (Node neighbor in currentNode.neighbors)
            {
                // don't even bother evaluating impassable nodes to save some time
                if (neighbor.cost == byte.MaxValue) { continue; }

                ushort newNeighborIntegration = (ushort) (currentNode.integration + neighbor.cost);
                
                if (newNeighborIntegration < neighbor.integration)
                {
                    neighbor.integration = newNeighborIntegration;
                    openNodes.Enqueue(neighbor);
                }
            }
        }
    }

    private void CreateFlowField()
    {
        CreateCostField();
        //CreateIntegrationField();
    }
    
    private void OnDrawGizmos()
    {
        if (grid != null)
        {
            foreach (Node node in grid)
            {
                if (node.cost == NodeCost.impassableTerrain)
                {
                    Gizmos.color = Color.red;
                }
                else if (node.cost == NodeCost.roughTerrain)
                {
                    Gizmos.color = Color.blue;
                }
                else if (node.cost == NodeCost.defaultTerrain)
                {
                    Gizmos.color = Color.green;
                }
                Gizmos.DrawWireCube(
                    node.worldPosition, 
                    Vector3.one * nodeDiameter
                );
                Handles.Label(node.worldPosition, node.integration.ToString());
            }
        }
    }
}
