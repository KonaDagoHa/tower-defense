using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


/*
 * How to create a flow field:
 * (1) CreateGrid() in Map.cs
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
 *     Do step (1) when mapSize OR nodeRadius changes
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

[RequireComponent(typeof(Map))]
public class FlowField : MonoBehaviour
{
    [SerializeField] private LayerMask impassableTerrain;
    [SerializeField] private LayerMask roughTerrain;
    public Map map { get; private set; }
    public Node targetNode { get; private set; }
    public Vector3 targetPosition { get; private set; }

    private static class NodeCost
    {
        public const byte defaultTerrain = 1;
        public const byte impassableTerrain = byte.MaxValue;
        public const byte roughTerrain = 2;
    }

    private void Awake()
    {
        map = GetComponent<Map>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                CreateCostField();
                targetPosition = hit.point;
                CreateFlowField();
            }
        }
    }

    private void CreateCostField()
    {
        for (int x = 0; x < map.gridSize.x; x++)
        {
            for (int y = 0; y < map.gridSize.y; y++)
            {
                Node node = map.grid[x, y];
                // check for objects that would overlap the node at (x, y) on specific layers
                if (Physics.CheckSphere(node.worldPosition, map.nodeRadius, impassableTerrain))
                {
                    // node is impassable
                    node.cost = NodeCost.impassableTerrain;
                }
                else if (Physics.CheckSphere(node.worldPosition, map.nodeRadius, roughTerrain))
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
        foreach (Node node in map.grid)
        {
            node.integration = ushort.MaxValue;
        }
        // set integration of targetNode to 0
        targetNode = map.WorldToNode(targetPosition);
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
        foreach (Node node in map.grid)
        {
            node.UpdateFlowDirection();
        }
    }
    
    
    private void OnDrawGizmos()
    {
        if (map != null && map.grid != null)
        {
            foreach (Node node in map.grid)
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
                    Vector3.one * map.nodeDiameter
                );
                if (node == targetNode)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawCube(
                        node.worldPosition,
                        Vector3.one * map.nodeDiameter
                        );
                }
                
                //Handles.Label(node.worldPosition, node.integration.ToString());
                //Gizmos.color = Color.white;
                //Gizmos.DrawRay(node.worldPosition, node.flowDirection);
                
            }
        }
    }
    
}
