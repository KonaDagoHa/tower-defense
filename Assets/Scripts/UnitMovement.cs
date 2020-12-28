using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// if moving normally, use Rigidbody.AddForce() with ForceMode.Acceleration
    // this is like doing selfRigidbody.velocity = 
    // use ForceMode.Force if you want to take mass into account (try this if units are different sizes)
// if jumping or dashing, use ForceMode.VelocityChange
    // this is like doing selfRigidbody.velocity +=
    // use ForceMode.Impulse if you want to take mass into account

[RequireComponent(typeof(Unit))]
public class UnitMovement : MonoBehaviour
{
    private LayerMask unitsMask;
    private Unit selfUnit;
    private Rigidbody selfRigidbody;
    private Collider selfCollider;

    // flow field
    private FlowField flowField;
    private Node currentNode;
    
    // unit detection
    private Collider[] unitsDetected = new Collider[8];
    private int numUnitsDetected;
    private float detectionRadius = 4;
    private float avoidanceRadius = 2;
    
    // movement
    private float maxMoveSpeed = 15f;

    public void Initialize()
    {
        selfUnit = GetComponent<Unit>();
        flowField = selfUnit.flowField;
        selfRigidbody = selfUnit.selfRigidbody;
        selfCollider = selfUnit.selfCollider;
        currentNode = flowField.WorldToNode(transform.position);
    }

    private void Update()
    {
        if (Utilities.FrameIsDivisibleBy(30))
        {
            if (!selfUnit.isRagdoll)
            {
                // get all units within detectionRadius
                numUnitsDetected = Physics.OverlapSphereNonAlloc(
                    transform.position, 
                    detectionRadius, 
                    unitsDetected, 
                    unitsMask
                    );
            }
        }
    }

    private void FixedUpdate()
    {
        if (flowField.grid != null && !selfUnit.isRagdoll)
        {
            currentNode = flowField.WorldToNode(transform.position);
            Vector3 moveDirection = Vector3.zero;
            moveDirection += FollowFlowField();
            moveDirection += AvoidOtherUnits();
                
            if (moveDirection.sqrMagnitude > 1)
            {
                moveDirection.Normalize();
            }
                
            // apply velocity to rigidbody
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

    // MOVEMENT BEHAVIORS (STEERING)
    
    private Vector3 FollowFlowField()
    {
        Vector3 directionChange;
        if (currentNode == flowField.targetNode)
        {
            // if currentNode is targetNode, move towards targetPosition located inside targetNode
            directionChange = flowField.targetPosition - transform.position;
            if (directionChange.sqrMagnitude > 1)
            {
                directionChange.Normalize();
            }
        }
        else
        {
            // if currentNode is NOT targetNode, follow currentNode's flowDirection
            directionChange = currentNode.flowDirection;
        }
        
        // directionChange will always have magnitude 1 (this can be modified later by weights)
        return directionChange;
    }
    
    private Vector3 AvoidOtherUnits()
    {
        if (numUnitsDetected > 1)
        {
            Vector3 avoidanceDirection = Vector3.zero;
            int numUnitsToAvoid = 0;
            for (int i = 0; i < numUnitsDetected; i++)
            {
                Collider unitCollider = unitsDetected[i];
                // if unitCollider does not belong to the unit calling the function
                if (unitCollider != selfCollider)
                {
                    // TODO: to get better avoidance behavior, have the avoidanceDirection influence the units future position (ahead vector)
                    // rather than the unit's current position (awayFromUnit = aheadVector - unitCollider.transform.position)
                    // https://gamedevelopment.tutsplus.com/tutorials/understanding-steering-behaviors-collision-avoidance--gamedev-7777
                    
                    Vector3 awayFromUnit = transform.position - unitCollider.transform.position;
                    
                    float awayFromUnitMagnitude = awayFromUnit.magnitude;
                    // if awayFromUnit is not a zero vector AND the unit is within avoidanceRadius
                    if (awayFromUnitMagnitude != 0 && awayFromUnitMagnitude <= avoidanceRadius)
                    {
                        numUnitsToAvoid++;
                        // the closer the unitCollider is, the larger the magnitude of awayFromUnit
                        awayFromUnit /= awayFromUnitMagnitude;
                        avoidanceDirection += awayFromUnit;
                    }
                }
            }
            
            if (numUnitsToAvoid > 0)
            {
                avoidanceDirection /= numUnitsToAvoid;
                return avoidanceDirection;
            }
        }

        // if this statement is reached, it means there were no units within avoidance radius
        return Vector3.zero;
    }

}
