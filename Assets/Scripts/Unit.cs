using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// TODO: implement steering behaviors
    // http://www.red3d.com/cwr/steer/gdc99/
    // do this in a separate class
// TODO: instead of freezing rotation, allow rotation if unit is hit by something


public class Unit : MonoBehaviour
{
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private Transform legs;
    [SerializeField] private Transform feet;
    private Rigidbody selfRigidbody;
    private Collider selfCollider;

    // if moving normally, use Rigidbody.AddForce() with ForceMode.Acceleration
        // this is like doing selfRigidbody.velocity = 
        // use ForceMode.Force if you want to take mass into account (try this if units are different sizes)
    // if jumping or dashing, use ForceMode.VelocityChange
        // this is like doing selfRigidbody.velocity +=
        // use ForceMode.Impulse if you want to take mass into account
    
    // movement
    private float maxMoveSpeed = 15f;
    private Vector3 moveDirection = Vector3.zero;
    private float avoidanceRadius = 2;
    private Collider[] unitsToAvoid = new Collider[5];

    // flow field
    private FlowField flowField;
    private Node currentNode;

    // jumping
    private WaitForSeconds jumpDelay = new WaitForSeconds(2); // in seconds
    private bool canJump = true;

    // states
    private bool isRagdoll;
    private bool feetGrounded => Physics.CheckSphere(feet.position, 0.02f, terrainMask);
    private bool legsGrounded => Physics.CheckSphere(legs.position, 0.5f, terrainMask);
    private float initialRagdollAngularSpeed => Random.Range(5f, 10f);
    private float ragdollRecoveryJumpSpeed => Random.Range(5f, 10f);

    public void Initialize(FlowField field, Vector3 localPosition)
    {
        selfRigidbody = GetComponent<Rigidbody>();
        selfCollider = GetComponent<Collider>();
        flowField = field;
        transform.localPosition = localPosition;
        currentNode = flowField.WorldToNode(transform.position);
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
                Vector3 acceleration = moveDirection * maxMoveSpeed - selfRigidbody.velocity;
                selfRigidbody.AddForce(acceleration, ForceMode.Acceleration);
                
                Vector3 velocity = selfRigidbody.velocity;

                // rotate unit so that it faces current velocity
                // but only if the velocity is great enough () prevent units from rapidly rotating back and forth
                if (velocity.sqrMagnitude > 1)
                {

                    // also makes sure unit is upright
                    Quaternion moveRotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z)),
                        4
                    );
                    selfRigidbody.MoveRotation(moveRotation);
                }
            }
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
        {
            ToggleRagdoll(true);
            selfRigidbody.AddTorque(Random.insideUnitSphere * initialRagdollAngularSpeed, ForceMode.VelocityChange);
        }
    }

    private void FollowFlowField()
    {
        if (currentNode == flowField.targetNode)
        {
            // if currentNode is targetNode, move towards targetPosition located inside targetNode
            moveDirection = (flowField.targetPosition - transform.position).normalized;
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
        int numUnitsToAvoid = Physics.OverlapSphereNonAlloc(position, avoidanceRadius, unitsToAvoid, unitsMask);
        if (numUnitsToAvoid > 1)
        {
            Vector3 avoidanceDirection = Vector3.zero;
            for (int i = 0; i < numUnitsToAvoid; i++)
            {
                Collider unitCollider = unitsToAvoid[i];
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
            avoidanceDirection /= numUnitsToAvoid - 1;

            moveDirection += avoidanceDirection;
        }
    }

    private IEnumerator JumpCoolDown()
    {
        yield return jumpDelay;
        canJump = true;
    }

    private void Jump(Vector3 velocityChange)
    {
        if (canJump)
        {
            selfRigidbody.AddForceAtPosition(velocityChange, feet.position, ForceMode.VelocityChange);
            canJump = false;
            StartCoroutine(JumpCoolDown());
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
            Vector3 velocityChange = jumpDirection * ragdollRecoveryJumpSpeed;
            Jump(velocityChange);
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
