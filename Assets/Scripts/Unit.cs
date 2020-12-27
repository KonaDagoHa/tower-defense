using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// TODO: implement steering behaviors
    // http://www.red3d.com/cwr/steer/gdc99/
    // do this in a separate class
// BUG: gravity is extremely weak when unit is moving via flow field
    // FIX: when unit is not grounded, unit goes into ragdoll mode until it is grounded again
        // this may cause problems with jumping 
            // EDIT: units will now turn into ragdolls when feet not grounded. this will not affect jumping because the units
            // should land on their feet after jumping
// TODO: instead of freezing rotation, allow rotation if unit is hit by something
// BUG: avoidance behavior makes units shake because units can't decide whether to follow flow field or avoid other units
    // Solution: follow flow field should be default behavior (only gets activated when there are no other behaviors)
        // OR when only certain other behaviors are active
        

public class Unit : MonoBehaviour
{
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private Transform legs;
    [SerializeField] private Transform feet;
    private Rigidbody selfRigidbody;
    private Collider selfCollider;
    
    // movement
    private float moveSpeed = 5f;
    private Vector3 moveDirection = Vector3.zero;
    private float avoidanceRadius = 2;

    // flow field
    private FlowField flowField;
    private Node currentNode;
    private bool reachedTarget = false;

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
        currentNode = flowField.WorldToNode(transform.position);

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
                ToggleRagdoll(true);
            }
            else // default behavior (not ragdoll)
            {
                currentNode = flowField.WorldToNode(transform.position);
                FollowFlowField();
                AvoidOtherUnits();
                
                // apply velocity to rigidbody
                if (moveDirection.sqrMagnitude > 1)
                {
                    moveDirection.Normalize();
                }
                Vector3 velocityChange = moveDirection * moveSpeed - selfRigidbody.velocity;
                selfRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
                
                Vector3 velocity = selfRigidbody.velocity;

                // rotate unit so that it faces current velocity
                if (velocity != Vector3.zero)
                {
                    // also makes sure unit is upright
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
        if (!reachedTarget)
        {
            if (currentNode == flowField.targetNode)
            {
                // if currentNode is targetNode, move towards targetPosition located inside targetNode
                moveDirection = flowField.targetPosition - transform.position;
                reachedTarget = true;
                StartCoroutine(ReachedTargetCooldown());
            }
            else
            {
                // if currentNode is NOT targetNode, follow currentNode's flowDirection
                moveDirection = currentNode.flowDirection;
            }
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
                    
                    float awayFromUnitMagnitude = awayFromUnit.magnitude;
                    // this will fix divide by zero errors (happens if two units have same position)
                    if (awayFromUnitMagnitude != 0)
                    {
                        // the closer the unitCollider is, the larger the magnitude of awayFromUnit
                        awayFromUnit /= awayFromUnitMagnitude;
                        avoidanceDirection += awayFromUnit;
                    }

                }

            }

            // Length - 1 because unitsToAvoid includes this unit as well as neighbors
            // this also normalizes avoidanceDirection because each awayFromUnit vector became a unit vector upon
                // dividing by its magnitude
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

    private IEnumerator ReachedTargetCooldown()
    {
        if (reachedTarget)
        {
            yield return new WaitForSeconds(2);
            reachedTarget = false;
        }
    }

    private void ToggleRagdoll(bool toggleOn)
    {
        if (toggleOn && !isRagdoll)
        {
            isRagdoll = true;
            selfRigidbody.constraints = RigidbodyConstraints.None;
        }
        else if (!toggleOn && isRagdoll)
        {
            isRagdoll = false;
            selfRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
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
            selfRigidbody.AddForceAtPosition(jumpDirection * Random.Range(5f, 10f), feet.position, ForceMode.VelocityChange);
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
            ToggleRagdoll(false);
        }
    }

}
