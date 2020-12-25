using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    private Rigidbody rb;
    private FlowField flowField;
    private float moveSpeed = 5f;

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
            Vector3 moveDirection = currentNode.flowDirection;
            rb.MovePosition(position + moveDirection * (moveSpeed * Time.deltaTime));
        }
    }
}
