using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


/*
 * How to create a flow field:
 * (1) CreateGrid()
 * (2) CreateCostField()
 * (3) CreateIntegrationField()
 * (4) foreach node in grid, node.UpdateFlowDirection()
 * 
 * Each step depends on the previous step, but not vice versa.
 * 
 * EXAMPLE:
 *     Completing step (1) requires completing steps (2), (3), and (4) immediately afterwards.
 *     Completing step (4) does NOT require completing steps (1), (2) or (3).
 * 
 * Therefore,
 *     Do step (1) when map.size OR nodeRadius changes
 *         Followed by (2), (3), and (4)
 *     Do step (2) when terrain objects change position
 *         Followed by (3) and (4)
 *     Do step (3) when targetPosition (destination of units) changes
 *         Followed by (4)
 *     Do step (4) when you want to update the flow vectors from the integration field in step (3)
 * 
 * Do NOT start at step (1) every time you want to update the flow field.
 * See if you can update it using a later step first then move backwards
 */
public class FlowField : MonoBehaviour
{
    [SerializeField] private LayerMask impassableTerrain;
    [SerializeField] private LayerMask roughTerrain;
    [SerializeField] private float nodeRadius = 0.5f;
    [SerializeField] private Transform target;
    public Map map;
    public Node[,] grid { get; private set; }
    public Node targetNode { get; private set; }
    public Vector3 targetPosition { get; private set; }
    private Vector2Int gridSize;
    private float nodeDiameter => nodeRadius * 2;

    private static class NodeCost
    {
        public const byte targetNode = 0;
        public const byte defaultTerrain = 1;
        public const byte impassableTerrain = byte.MaxValue;
        public const byte roughTerrain = 4;
    }

    public Node WorldToNode(Vector3 worldPosition)
    {
        // percentages of worldPosition from bottom left corner to top right corner of grid
            // percentX = 0 and percentY = 0 is bottom left corner
            // percentX = 1 and percentY = 1 is top right corner
        float percentX = Mathf.Clamp01((worldPosition.x + map.size.x / 2) / map.size.x);
        float percentY = Mathf.Clamp01((worldPosition.z + map.size.y / 2) / map.size.y);

        // indices of grid
        int x = Mathf.RoundToInt((gridSize.x - 1) * percentX);
        int y = Mathf.RoundToInt((gridSize.y - 1) * percentY);

        return grid[x, y];
    }

    private void Start()
    {
        CreateGrid();
        CreateCostField();
        targetPosition = target.position;
        CreateFlowField();
    }
    /*
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                targetPosition = hit.point;
                CreateFlowField();
            }
        }
    }
    */
    private void CreateGrid()
    {
        // grid will be centered on the map, but will not necessarily match size of map
        gridSize = new Vector2Int(
            Mathf.CeilToInt(map.size.x / nodeDiameter),
            Mathf.CeilToInt(map.size.y / nodeDiameter)
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
            node.UpdateNeighbors(grid, gridSize);
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

    private void CreateIntegrationField()
    {
        // reset integration values of all nodes
        foreach (Node node in grid)
        {
            node.integration = ushort.MaxValue;
        }
        // set integration of targetNode to 0
        targetNode = WorldToNode(targetPosition);
        targetNode.integration = 0;
        // create queue of open nodes and add target node to it
        Queue<Node> openNodes = new Queue<Node>();
        openNodes.Enqueue(targetNode);

        while (openNodes.Count > 0)
        {
            Node currentNode = openNodes.Dequeue();
            foreach (Node neighbor in currentNode.neighborsCardinal)
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
        CreateIntegrationField();
        foreach (Node node in grid)
        {
            node.UpdateFlowDirection();
        }
    }
    
    
    private void OnDrawGizmos()
    {
        if (grid != null)
        {
            foreach (Node node in grid)
            {
                if (node == targetNode)
                {
                    Gizmos.color = Color.black;
                }
                else if (node.cost == NodeCost.impassableTerrain)
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
                //Handles.Label(node.worldPosition, node.integration.ToString());
                //Gizmos.color = Color.white;
                //Gizmos.DrawRay(node.worldPosition, node.flowDirection);
                
            }
        }
    }
    
}
