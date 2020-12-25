using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    private Rigidbody rb;
    private FlowField flowField;
    private float moveSpeed = 10f;
    private Vector3 moveVelocity = Vector3.zero;

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
            Vector3 position = transform.position;
            Node currentNode = flowField.NodeFromWorldPosition(position);
            Vector3 moveDirection;
            if (currentNode == flowField.targetNode)
            {
                // if currentNode is targetNode, move towards targetPosition
                moveDirection = flowField.targetPosition - position;
                if (moveDirection.sqrMagnitude > 0.01f)
                {
                    moveDirection.Normalize();
                }
                else
                {
                    moveDirection = Vector3.zero;
                }
            }
            else
            {
                // if currentNode is NOT targetNode, follow currentNode's flowDirection
                moveDirection = currentNode.flowDirection;
            }
            moveVelocity = moveDirection * moveSpeed;
            rb.MovePosition(position + moveVelocity * Time.deltaTime);
        }
    }

    private void AvoidOtherUnits()
    {
        
    }
}
