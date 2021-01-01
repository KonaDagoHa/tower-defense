using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.UIElements;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

// how pathfinding will work:
// enemies will begin by their designated flow field (number of flow fields is equal to number of goals)
// enemies will also try to avoid other enemy units
// once enemy detects a friendly unit, enemy will disable FlowFieldSteering() and enable SeekSteering() to go towards friendly unit
// once friendly unit dies or is out of range, FlowFieldSteering() is enabled and SeekSteering() is disabled


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

    // unit detection
    private Collider[] unitsDetected = new Collider[8];
    private int numUnitsDetected;
    private float detectionRadius = 10;

    // movement (done in FixedUpdate())
    private Vector3 targetPosition; // position that unit wants to go to
    private float maxVelocity = 5f; // controls how fast unit can move
    private float maxAcceleration = 10; // controls how fast unit can reach max speed/velocity (lower values produce more curved paths; high values more responsive movement)
    private Vector3 currentVelocity;
    private Vector3 currentAcceleration;

    private float maxAngularVelocity = 100; // TODO: implement angular kinematics for rotation


    // steering
    private float arrivalDistance = 2; // once this unit is within this distance of the targetPosition, slow down gradually
    private float separationDistance = 10; // other units within this distance will trigger the separation steering behavior

    [Serializable]
    public class SteeringWeights
    {
        public float flowField = 1;
        public float separation = 1;
    }

    private void Awake()
    {
        selfUnit = GetComponent<Unit>();
        selfRigidbody = selfUnit.selfRigidbody;
        selfCollider = selfUnit.selfCollider;
        UnitSpawner spawner = GetComponentInParent<UnitSpawner>();
        map = spawner.map;
        flowField = spawner.flowField;
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
            UpdatePosition();

            // rotate unit so that it faces current velocity; also makes sure unit is upright
            Quaternion moveRotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(currentVelocity.x, 0, currentVelocity.z)),
                maxAngularVelocity * Time.deltaTime
            );
            selfRigidbody.MoveRotation(moveRotation);
        }
    }

    private bool CheckWithinFOV(Vector3 position, float fov)
    {
        // check if provided position is within this unit's FOV (within a cone coming out of unit)
        Vector3 towardsPosition = position - transform.position;
        float angleToPosition = Vector3.Angle(transform.forward, towardsPosition);
        return angleToPosition <= fov;
    }
    
    // MOVEMENT BEHAVIORS (STEERING)

    private void UpdatePosition()
    {
        targetPosition = flowField.targetPosition;
        // units move by manipulating their acceleration (not velocity)
        Vector3 towardsTarget = targetPosition - transform.position;
        Vector3 towardsTargetXZ = new Vector3(towardsTarget.x, 0, towardsTarget.z); // ignore the y axis
        
        float sqrDistance = towardsTargetXZ.sqrMagnitude;
        if (sqrDistance <= arrivalDistance * arrivalDistance) // check if unit is within arrival distance
        {
            currentAcceleration = Vector3.zero; // overwrite acceleration
            currentVelocity = towardsTargetXZ.normalized * maxVelocity; // go towards target
            currentVelocity *= Mathf.Sqrt(sqrDistance) / arrivalDistance; // speed decreases as unit approaches targetPosition
        }
        else
        {
            currentAcceleration = SteeredAcceleration();
            currentVelocity += currentAcceleration * Time.deltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxVelocity);
        }

        // move unit according to new acceleration and velocity
        Vector3 positionChange = currentVelocity * Time.deltaTime +
                                 currentAcceleration * (0.5f * Time.deltaTime * Time.deltaTime);
        selfRigidbody.MovePosition(transform.position + positionChange);
    }

    private Vector3 SteeredAcceleration()
    {
        Vector3 steering = Vector3.zero; // change in acceleration
        steering += FlowFieldSteering() * weights.flowField;
        steering += SeparationSteering() * weights.separation;
        Vector3 steeredAcceleration = currentAcceleration + steering;
        steeredAcceleration = Vector3.ClampMagnitude(steeredAcceleration, maxAcceleration);

        // return the steered acceleration (what the acceleration should be after applied steering behaviors)
        return steeredAcceleration;
    }

    private Vector3 ArrivalSteering()
    {
        Vector3 towardsTarget = targetPosition - transform.position;
        Vector3 towardsTargetXZ = new Vector3(towardsTarget.x, 0, towardsTarget.z); // ignore the y axis
        float sqrDistance = towardsTargetXZ.sqrMagnitude;
        if (sqrDistance <= arrivalDistance * arrivalDistance) // unit is within arrival distance (is arriving)
        {
            currentAcceleration = Vector3.zero; // overwrite acceleration
            currentVelocity = towardsTargetXZ.normalized * maxVelocity; // go towards target
            currentVelocity *= Mathf.Sqrt(sqrDistance) / arrivalDistance; // speed decreases as unit approaches targetPosition
        }

        return Vector3.zero;
    }

    // use this for long distance pathfinding
    private Vector3 FlowFieldSteering()
    {
        targetPosition = flowField.targetPosition; // change/delete this later
        Vector3 futurePosition = transform.position + currentVelocity * Time.deltaTime; // TODO: adjust future position by change Time.deltaTime to a higher value in seconds
        Node futureNode = map.WorldToNode(futurePosition);
        Vector3 desiredAcceleration;
        if (futureNode == flowField.targetNode)
        {
            // if evaluated node is targetNode, move towards targetPosition located inside targetNode (gradually slow down)
            desiredAcceleration = targetPosition - futurePosition;
        }
        else
        {
            // if evaluated node is NOT targetNode, follow flowDirection (max speed)
            desiredAcceleration = futureNode.flowDirection;
        }

        desiredAcceleration *= maxAcceleration;
        desiredAcceleration = Vector3.ClampMagnitude(desiredAcceleration, maxAcceleration);
        Vector3 steering = desiredAcceleration - currentAcceleration;
        return new Vector3(steering.x, 0, steering.z);
    }
    
    // use this for short distance pathfinding (like following an enemy)
    private Vector3 SeekSteering(Vector3 accelerationToSteer)
    {
        // TODO: IMPLEMENT THIS
        return Vector3.zero;
    }

    // use this for short distance pathfinding (like following an enemy)
    private Vector3 AvoidObstaclesSteering(Vector3 accelerationToSteer)
    {
        // TODO: IMPLEMENT THIS
        return Vector3.zero;
    }
    
    // use this for all pathfinding
    private Vector3 SeparationSteering()
    {
        if (numUnitsDetected > 1)
        {
            Vector3 desiredAcceleration = Vector3.zero;
            int numUnitsValid = 0;
            for (int i = 0; i < numUnitsDetected; i++)
            {
                Collider unitCollider = unitsDetected[i];
                // if unitCollider does not belong to the unit calling the function
                if (unitCollider != selfCollider)
                {
                    Vector3 awayFromUnit = transform.position - unitCollider.transform.position;
                    
                    float awayFromUnitMagnitude = awayFromUnit.magnitude;
                    // if awayFromUnit is not a zero vector AND the unit is within avoidanceRadius
                    if (awayFromUnitMagnitude != 0 && awayFromUnitMagnitude <= separationDistance)
                    {
                        numUnitsValid++;
                        desiredAcceleration += awayFromUnit;
                    }
                }
            }
            
            if (numUnitsValid > 0)
            {
                desiredAcceleration /= numUnitsValid; // find average magnitude
                desiredAcceleration *= maxAcceleration;
                desiredAcceleration = Vector3.ClampMagnitude(desiredAcceleration, maxAcceleration);
                Vector3 steering = desiredAcceleration - currentAcceleration;
                // only steer on the xy plane
                return new Vector3(steering.x, 0, steering.z);
            }
        }

        // if this statement is reached, it means there were no units within avoidance radius
        return Vector3.zero;
    }

}
