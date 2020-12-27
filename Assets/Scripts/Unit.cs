using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private Transform groundCheck;
    private Rigidbody selfRigidbody;
    private Collider selfCollider;
    private FlowField flowField;
    private float moveSpeed = 5f;
    private Vector3 moveDirection = Vector3.zero;
    private float avoidanceRadius = 3;
    private bool isRagdoll;

    public void Initialize(FlowField field, Vector3 localPosition)
    {
        selfRigidbody = GetComponent<Rigidbody>();
        selfCollider = GetComponent<Collider>();
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
        if (flowField.grid != null && !isRagdoll)
        {
            FollowFlowField();
            AvoidOtherUnits();
            if (moveDirection.sqrMagnitude > 1)
            {
                moveDirection.Normalize();
            }

            Vector3 velocityChange = moveDirection * moveSpeed - selfRigidbody.velocity;
            selfRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }

    private void FollowFlowField()
    {
        Vector3 position = transform.position;
        Node currentNode = flowField.WorldToNode(position);
        if (currentNode == flowField.targetNode)
        {
            // if currentNode is targetNode, move towards currentNode's center
            moveDirection = flowField.targetPosition - position;
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
        if (unitsToAvoid.Length > 1)
        {
            Vector3 avoidanceDirection = Vector3.zero;
            foreach (Collider unitCollider in unitsToAvoid)
            {
                // if unitCollider does not belong to the unit calling the function
                if (unitCollider != selfCollider)
                {
                    Vector3 awayFromUnit = position - unitCollider.transform.position;
                    // the closer the unitCollider is, the larger the magnitude of awayFromUnit
                    awayFromUnit /= awayFromUnit.sqrMagnitude;
                    avoidanceDirection += awayFromUnit;
                }

            }

            // Length - 1 because unitsToAvoid includes this unit as well as neighbors
            avoidanceDirection /= unitsToAvoid.Length - 1;
            
            moveDirection += avoidanceDirection;
        }
    }

    private void ToggleRagdoll()
    {
        isRagdoll = !isRagdoll;
        if (isRagdoll)
        {
            selfRigidbody.constraints = RigidbodyConstraints.None;
        }
        else
        {
            selfRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            // get up (if fallen over)
            selfRigidbody.MoveRotation(Quaternion.Euler(0, 0, 0));
        }
    }
}
