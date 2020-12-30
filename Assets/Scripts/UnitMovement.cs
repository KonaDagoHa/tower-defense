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

// BUG: AvoidUnits steering behavior somehow overwrites FollowFlowField and vice versa
// it seems like if both behaviors are active at once, they cancel out the velocity to zero

[RequireComponent(typeof(Unit))]
public class UnitMovement : MonoBehaviour
{
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private MovementWeights weights = new MovementWeights();

    private Unit selfUnit;
    private Rigidbody selfRigidbody;
    private Collider selfCollider;

    // map
    private Map map;
    private FlowField flowField;
    private Node currentNode;
    
    // unit detection
    private Collider[] unitsDetected = new Collider[5];
    private int numUnitsDetected;
    private float detectionRadius = 4;
    private float avoidanceRadius = 1.5f;

    // movement
    private float maxMoveSpeed = 15f;

    [Serializable]
    public class MovementWeights
    {
        public float followFlowField = 1;
        public float avoidUnits = 1;
    }

    public void Initialize()
    {
        selfUnit = GetComponent<Unit>();
        map = selfUnit.map;
        flowField = selfUnit.flowField;
        selfRigidbody = selfUnit.selfRigidbody;
        selfCollider = selfUnit.selfCollider;
        currentNode = map.WorldToNode(transform.position);
    }

    private void Update()
    {
        if (Utilities.FrameIsDivisibleBy(60))
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
        if (map.grid != null && !selfUnit.isRagdoll)
        {
            currentNode = map.WorldToNode(transform.position);
            Vector3 steeredVelocity = SteeredVelocity();
            
            // apply velocity to rigidbody
            Vector3 velocityChange = steeredVelocity - selfRigidbody.velocity;
            selfRigidbody.AddForce(velocityChange, ForceMode.Acceleration);


            // rotate unit so that it faces current velocity
            // but only if the velocityChange is great enough () prevent units from rapidly rotating back and forth
            if (steeredVelocity.x != 0 || steeredVelocity.z != 0)
            {
                // also makes sure unit is upright
                Quaternion moveRotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(new Vector3(steeredVelocity.x, 0, steeredVelocity.z)),
                    4
                );
                selfRigidbody.MoveRotation(moveRotation);
            }
        }
    }

    // MOVEMENT BEHAVIORS (STEERING)

    private Vector3 SteeredVelocity()
    {
        Vector3 velocity = selfRigidbody.velocity;
        velocity = FollowFlowField(velocity);
        velocity = AvoidUnits(velocity);

        if (velocity.sqrMagnitude > 1)
        {
            velocity = velocity.normalized * maxMoveSpeed;
        }

        return velocity;
    }
    
    private Vector3 FollowFlowField(Vector3 velocityToSteer)
    {
        Vector3 desiredVelocity;
        if (currentNode == flowField.targetNode)
        {
            // if currentNode is targetNode, move towards targetPosition located inside targetNode (gradually slow down)
            desiredVelocity = flowField.targetPosition - transform.position;
        }
        else
        {
            // if currentNode is NOT targetNode, follow currentNode's flowDirection (max speed)
            desiredVelocity = currentNode.flowDirection;
        }
        
        if (desiredVelocity.sqrMagnitude > 1)
        {
            desiredVelocity.Normalize();
        }

        desiredVelocity *= maxMoveSpeed;
        return (desiredVelocity - velocityToSteer) * weights.followFlowField;
    }
    
    private Vector3 AvoidUnits(Vector3 velocityToSteer)
    {
        if (numUnitsDetected > 1)
        {
            Vector3 desiredVelocity = Vector3.zero;
            int numUnitsToAvoid = 0;
            for (int i = 0; i < numUnitsDetected; i++)
            {
                Collider unitCollider = unitsDetected[i];
                // if unitCollider does not belong to the unit calling the function
                if (unitCollider != selfCollider)
                {
                    Vector3 awayFromUnit = transform.position - unitCollider.transform.position;
                    
                    float awayFromUnitMagnitude = awayFromUnit.magnitude;
                    // if awayFromUnit is not a zero vector AND the unit is within avoidanceRadius
                    if (awayFromUnitMagnitude != 0 && awayFromUnitMagnitude <= avoidanceRadius)
                    {
                        numUnitsToAvoid++;
                        // the closer the unitCollider is, the larger the magnitude of awayFromUnit
                        awayFromUnit /= awayFromUnitMagnitude;
                        desiredVelocity += awayFromUnit;
                    }
                }
            }
            
            if (numUnitsToAvoid > 0)
            {
                desiredVelocity /= numUnitsToAvoid;
                desiredVelocity *= maxMoveSpeed;
                return (desiredVelocity - velocityToSteer) * weights.avoidUnits;
            }
        }

        // if this statement is reached, it means there were no units within avoidance radius
        return velocityToSteer;
    }

}
