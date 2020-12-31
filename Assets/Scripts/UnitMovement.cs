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

// how pathfinding will work:
// enemies will begin by their designated flow field (number of flow fields is equal to number of goals)
// enemies will also try to avoid other enemy units
// once enemy detects a friendly unit, enemy will disable FlowFieldSteering() and enable SeekSteering() to go towards friendly unit
// once friendly unit dies or is out of range, FlowFieldSteering() is enabled and SeekSteering() is disabled

// TODO: fix units shaking when standing still by disabling avoidance under some conditions

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

    // movement (done in FixedUpdate())
    private float maxMoveSpeed = 10f;
    
    // rotation (done in Update())
    private float maxRotationSpeed = 180; // in degrees per second

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
            Vector3 currentVelocity = selfRigidbody.velocity;
            Vector3 steeredVelocity = SteeredVelocity();

            // apply velocity to rigidbody
            Vector3 velocityChange = steeredVelocity - currentVelocity;
            selfRigidbody.AddForce(velocityChange, ForceMode.Acceleration);
            
            // rotate unit so that it faces current velocity (not steered velocity because it hasn't been processed by physics)
            // also makes sure unit is upright
            Quaternion moveRotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(currentVelocity.x, 0, currentVelocity.z)),
                maxRotationSpeed * Time.deltaTime
            );
            selfRigidbody.MoveRotation(moveRotation);
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
    
    // use this for long distance pathfinding
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
    
    // use this for short distance pathfinding (like following an enemy)
    private Vector3 SeekSteering(Vector3 velocityToSteer)
    {
        // TODO: IMPLEMENT THIS
        return Vector3.zero;
    }

    // use this for short distance pathfinding (like following an enemy)
    private Vector3 AvoidObstaclesSteering(Vector3 velocityToSteer)
    {
        // TODO: IMPLEMENT THIS
        return Vector3.zero;
    }
    
    // use this for all pathfinding
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
