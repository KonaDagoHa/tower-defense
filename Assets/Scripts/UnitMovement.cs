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
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private SteeringWeights weights = new SteeringWeights();

    private Unit selfUnit;
    private Rigidbody selfRigidbody;
    private Collider selfCollider;

    // map
    private Map map;
    private FlowField flowField;
    private Node currentNode;
    
    // unit detection
    private Collider[] unitsDetected = new Collider[8];
    private int numUnitsDetected;
    private float detectionRadius = 2;
    private float avoidanceRadius = 2;
    private float avoidanceFOV = 60; // unit must be within this field of view angle to be avoided

    // movement
    private float maxMoveSpeed = 15f;
    private float maxRotationSpeed = 200; // in degrees per second

    // steering
    [Serializable]
    public class SteeringWeights
    {
        public float flowField = 1;
        public float avoidUnits = 1;
    }

    private void Awake()
    {
        selfUnit = GetComponent<Unit>();
        UnitSpawner spawner = GetComponentInParent<UnitSpawner>();
        map = spawner.map;
        flowField = spawner.flowField;
    }

    private void Start()
    {
        selfRigidbody = selfUnit.selfRigidbody;
        selfCollider = selfUnit.selfCollider;
        currentNode = map.WorldToNode(transform.position);
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
        if (map.grid != null && !selfUnit.isRagdoll)
        {
            currentNode = map.WorldToNode(transform.position);
            Vector3 steeredVelocity = SteeredVelocity();
            
            // apply velocity to rigidbody
            Vector3 velocityChange = steeredVelocity - selfRigidbody.velocity;
            selfRigidbody.AddForce(velocityChange, ForceMode.Acceleration);

            // rotate unit so that it faces current velocity
            // but only if velocity is high enough (reduces shaking rapid when avoiding multiple objects)
            if (velocityChange.sqrMagnitude > 100)
            {
                // also makes sure unit is upright
                Quaternion moveRotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(new Vector3(steeredVelocity.x, 0, steeredVelocity.z)),
                    maxRotationSpeed * Time.deltaTime
                );
                selfRigidbody.MoveRotation(moveRotation);
            }
        }
    }

    // MOVEMENT BEHAVIORS (STEERING)

    private Vector3 SteeredVelocity()
    {
        Vector3 velocity = selfRigidbody.velocity;
        Vector3 steering = Vector3.zero;
        steering += FlowFieldSteering(velocity) * weights.flowField;
        steering += AvoidUnitsSteering(velocity) * weights.avoidUnits;
        velocity += steering;
        
        if (velocity.sqrMagnitude > maxMoveSpeed * maxMoveSpeed)
        {
            velocity = velocity.normalized * maxMoveSpeed;
        }

        return velocity;
    }
    
    private Vector3 FlowFieldSteering(Vector3 velocityToSteer)
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
        return desiredVelocity - velocityToSteer; // return the velocity needed to steer towards desired velocity
    }
    
    private Vector3 AvoidUnitsSteering(Vector3 velocityToSteer)
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
                        // check if unit is within field of view
                        Vector3 towardsUnit = -awayFromUnit;
                        float angleToUnit = Vector3.Angle(transform.forward, towardsUnit);
                        if (angleToUnit <= avoidanceFOV)
                        {
                            numUnitsToAvoid++;
                            // the closer the unitCollider is, the larger the magnitude of awayFromUnit
                            awayFromUnit /= awayFromUnitMagnitude;
                            desiredVelocity += awayFromUnit;
                        }
                    }
                }
            }
            
            if (numUnitsToAvoid > 0)
            {
                desiredVelocity /= numUnitsToAvoid;
                desiredVelocity *= maxMoveSpeed;
                return desiredVelocity - velocityToSteer;
            }
        }

        // if this statement is reached, it means there were no units within avoidance radius
        return Vector3.zero;
    }

}
