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

    public void Initialize(FlowField field, Vector3 localPosition)
    {
        rb = GetComponent<Rigidbody>();
        flowField = field;
        transform.localPosition = localPosition;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ToggleRagdoll();
        }
    }

    private void FixedUpdate()
    {
        if (flowField.grid != null && rb.isKinematic)
        {
            FollowFlowField();
            //AvoidOtherUnits();
            rb.MovePosition(transform.position + moveDirection * (moveSpeed * Time.deltaTime));
        }
    }

    private void FollowFlowField()
    {
        Vector3 position = transform.position;
        Node currentNode = flowField.WorldToNode(position);
        if (currentNode.cost == FlowField.NodeCost.targetNode)
        {
            // if currentNode is targetNode, move towards currentNode's center
            moveDirection = currentNode.worldPosition - position;
            // moveDirection should only be normalized if magnitude is greater than 1
            if (moveDirection.sqrMagnitude > 1)
            {
                // this will prevent unit from "dashing" toward targetPosition once it touches targetNode
                moveDirection.Normalize();
            }
        }
        else
        {
            // if currentNode is NOT targetNode, follow currentNode's flowDirection
            moveDirection = currentNode.flowDirection;
        }
    }

    private void ToggleRagdoll()
    {
        rb.isKinematic = !rb.isKinematic;
        if (rb.isKinematic) // switching to kinematic
        {
            // get up (if fallen over)
            rb.MoveRotation(Quaternion.Euler(0, 0, 0));
        }
        else // switching to non-kinematic (dynamic)
        {
            
        }
    }

    private void AvoidOtherUnits()
    {
        Vector3 position = transform.position;
        // get all other units within avoidanceRadius
        Collider[] unitsToAvoid = Physics.OverlapSphere(position, avoidanceRadius, unitsMask);
        Vector3 avoidanceDirection = Vector3.zero;
        if (unitsToAvoid.Length > 1)
        {
            moveDirection = Vector3.zero;

            if (unitsToAvoid.Length <= 2)
            {
                foreach (Collider unit in unitsToAvoid)
                {
                    avoidanceDirection += position - unit.transform.position;
                }
            
                avoidanceDirection /= unitsToAvoid.Length;
                avoidanceDirection = Quaternion.Euler(0, -45, 0) * avoidanceDirection;
                avoidanceDirection.y = 0;
            
                moveDirection = avoidanceDirection;
            }
            else
            {
                moveDirection = Vector3.zero;
            }
            
        }
    }
}
