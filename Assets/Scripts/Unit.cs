using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [SerializeField] private LayerMask unitsMask;
    private Rigidbody rb;
    private FlowField flowField;
    private float moveSpeed = 5f;
    private Vector3 moveDirection = Vector3.zero;
    private float avoidanceRadius = 1;

    public void Initialize(FlowField field, Vector3 position)
    {
        rb = GetComponent<Rigidbody>();
        flowField = field;
        transform.position = position;
    }

    private void FixedUpdate()
    {
        if (flowField.grid != null)
        {
            FollowFlowField();
            AvoidOtherUnits();
            Vector3 velocity = moveDirection * moveSpeed;
            rb.velocity = velocity;
        }
    }

    private void FollowFlowField()
    {
        Vector3 position = transform.position;
        Node currentNode = flowField.NodeFromWorldPosition(position);
        if (currentNode == flowField.targetNode)
        {
            // if currentNode is targetNode, move towards targetPosition
            moveDirection = (flowField.targetPosition - position).normalized;
        }
        else
        {
            // if currentNode is NOT targetNode, follow currentNode's flowDirection
            moveDirection = currentNode.flowDirection;
        }
    }

    private void AvoidOtherUnits()
    {
        Vector3 position = transform.position;
        // get all other units within avoidanceRadius
        Collider[] unitsToAvoid = Physics.OverlapSphere(position, avoidanceRadius, unitsMask);
        Vector3 avoidanceDirection = Vector3.zero;
        foreach (Collider unit in unitsToAvoid)
        {
            avoidanceDirection += position - unit.ClosestPoint(transform.position);
        }

        avoidanceDirection.Normalize();

        moveDirection += avoidanceDirection;
        
        moveDirection.Normalize();
    }
}
