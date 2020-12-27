using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// BUG: gravity is extremely weak when unit is moving via flow field
    // FIX: when unit is not grounded, unit goes into ragdoll mode until it is grounded again
        // this may cause problems with jumping 
            // EDIT: units will not turn into ragdolls when feet not grounded. this will not affect jumping because the units
            // should land on their feet after jumping
// TODO: instead of freezing rotation, allow rotation if unit is hit by something
// TODO: when unit recovers from ragdoll (feetGrounded) in RecoverFromRagdoll(), interpolate the rotation to be upright

public class Unit : MonoBehaviour
{
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private Transform legs;
    [SerializeField] private Transform feet;
    private Rigidbody selfRigidbody;
    private Collider selfCollider;
    private FlowField flowField;
    private float moveSpeed = 5f;
    private Vector3 moveDirection = Vector3.zero;
    private float avoidanceRadius = 3;
    
    // jumping
    private WaitForSeconds jumpDelay = new WaitForSeconds(2); // in seconds
    private bool canJump = true;

    // states
    private bool isRagdoll;
    private bool feetGrounded => Physics.CheckSphere(feet.position, 0.02f, terrainMask);
    private bool legsGrounded => Physics.CheckSphere(legs.position, 0.5f, terrainMask);

    public void Initialize(FlowField field, Vector3 localPosition)
    {
        selfRigidbody = GetComponent<Rigidbody>();
        selfCollider = GetComponent<Collider>();
        flowField = field;
        transform.localPosition = localPosition;
        StartCoroutine(JumpCoolDown());
    }

    private void FixedUpdate()
    {
        // unit turns into ragdoll if and only if feetGrounded == false
        
        if (flowField.grid != null)
        {
            if (isRagdoll)
            {
                RecoverFromRagdoll();
            }
            else if (!feetGrounded)
            {
                TurnOnRagdoll();
            }
            else // default behavior (not ragdoll)
            {
                FollowFlowField();
                AvoidOtherUnits();
                if (moveDirection.sqrMagnitude > 1)
                {
                    moveDirection.Normalize();
                }

                Vector3 velocityChange = moveDirection * moveSpeed - selfRigidbody.velocity;
                selfRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
                Vector3 velocity = selfRigidbody.velocity;
                if (velocity != Vector3.zero)
                {
                    Quaternion moveRotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z)),
                        5
                    );
                    selfRigidbody.MoveRotation(moveRotation);
                }
            }
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
                    // this will fix divide by zero errors (happens if two units have same position)
                    float awayFromUnitSqrMagnitude = awayFromUnit.sqrMagnitude;
                    if (awayFromUnitSqrMagnitude == 0)
                    {
                        awayFromUnitSqrMagnitude = 0.01f;
                    }
                    // the closer the unitCollider is, the larger the magnitude of awayFromUnit
                    awayFromUnit /= awayFromUnitSqrMagnitude;
                    avoidanceDirection += awayFromUnit;
                    
                }

            }

            // Length - 1 because unitsToAvoid includes this unit as well as neighbors
            avoidanceDirection /= unitsToAvoid.Length - 1;
            
            moveDirection += avoidanceDirection;
        }
    }

    private IEnumerator JumpCoolDown()
    {
        while (true)
        {
            if (!canJump)
            {
                yield return jumpDelay;
                canJump = true;
            }
            else
            {
                yield return null;
            }
        }
    }

    private void TurnOnRagdoll()
    {
        if (!isRagdoll)
        {
            isRagdoll = true;
            selfRigidbody.constraints = RigidbodyConstraints.None;
        }
    }

    private void RecoverFromRagdoll()
    {
        if (legsGrounded && canJump)
        {
            // unit will jump upward at slight angles
            Vector3 jumpDirection = Quaternion.Euler(
                Random.Range(-45f, 45f),
                Random.Range(-45f, 45f),
                Random.Range(-45f, 45f)) * Vector3.up;
            selfRigidbody.AddForceAtPosition(jumpDirection * Random.Range(5, 10), feet.position, ForceMode.VelocityChange);
            canJump = false;
        }
        else // unit is in air
        {
            // unit try to adjust its rotation to be upright (so that feet can touch ground)
            Quaternion moveRotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.Euler(0, transform.eulerAngles.y, 0),
                1.5f
            );
            selfRigidbody.MoveRotation(moveRotation);
        }
        
        if (feetGrounded)
        {
            TurnOffRagdoll();
        }
    }

    private void TurnOffRagdoll()
    {
        if (isRagdoll)
        {
            isRagdoll = false;
            selfRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            selfRigidbody.MoveRotation(Quaternion.Euler(0, transform.eulerAngles.y, 0));
        }
    }
    
}
