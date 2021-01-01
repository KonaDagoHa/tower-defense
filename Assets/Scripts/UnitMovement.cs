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
    private float detectionRadius = 2;
    private float avoidanceRadius = 2;
    private float avoidanceFOV = 60; // unit must be within this field of view angle to be avoided

    // movement (done in FixedUpdate())
    private Vector3 targetPosition; // position that unit wants to go to
    private float maxVelocity = 5f; // controls how fast unit can move
    private float maxAcceleration = 10f; // controls how fast unit can reach max speed/velocity
    private Vector3 currentVelocity;
    private Vector3 currentAcceleration;

    private float maxAngularVelocity = 100; // TODO: implement angular kinematics for rotation


    // steering
    private float arrivalDistance = 2; // once unit is within this distance of the targetPosition, slow down gradually
    
    [Serializable]
    public class SteeringWeights
    {
        public float flowField = 1;
        public float avoidUnits = 1;
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

    // MOVEMENT BEHAVIORS (STEERING)

    private void UpdatePosition()
    {
        // units move by manipulating their acceleration (not velocity)
        targetPosition = flowField.targetPosition; // change/delete this later so that targetPosition is not assigned every fixedupdate
        Vector3 towardsTarget = targetPosition - transform.position;
        Vector3 towardsTargetXZ = new Vector3(towardsTarget.x, 0, towardsTarget.z); // ignore the y axis
        
        // TODO: change this so unit will only go towards targetPosition IF they are not avoiding something
        // this overwrites avoidance behavior, causing all units to clump together
        float sqrDistance = towardsTargetXZ.sqrMagnitude;
        if (sqrDistance <= arrivalDistance * arrivalDistance) // check if unit is within arrival distance
        {
            currentAcceleration = Vector3.zero; // overwrite acceleration
            currentVelocity = towardsTargetXZ.normalized * maxVelocity; // go towards target
            currentVelocity *= Mathf.Sqrt(sqrDistance) / arrivalDistance; // speed decreases as unit approaches targetPosition
        }
        else
        {
            currentAcceleration = SteeredAcceleration(currentAcceleration);
            currentVelocity += currentAcceleration * Time.deltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxVelocity);
        }
        
        // move unit according to new acceleration and velocity
        Vector3 positionChange = currentVelocity * Time.deltaTime +
                                 currentAcceleration * (0.5f * Time.deltaTime * Time.deltaTime);
        selfRigidbody.MovePosition(transform.position + positionChange);
    }
    
    private Vector3 SteeredAcceleration(Vector3 accelerationToSteer)
    {
        Vector3 steering = Vector3.zero; // change in acceleration
        steering += FlowFieldSteering(accelerationToSteer) * weights.flowField;
        steering += AvoidUnitsSteering(accelerationToSteer) * weights.avoidUnits;
        Vector3 steeredAcceleration = accelerationToSteer + steering;
        steeredAcceleration = Vector3.ClampMagnitude(steeredAcceleration, maxAcceleration);

        // return the steered acceleration (what the acceleration should be after applied steering behaviors)
        return steeredAcceleration;
    }

    // use this for long distance pathfinding
    private Vector3 FlowFieldSteering(Vector3 accelerationToSteer)
    {
        Vector3 currentPosition = transform.position;
        Node currentNode = map.WorldToNode(currentPosition);
        
        Vector3 desiredAcceleration;
        if (currentNode == flowField.targetNode)
        {
            // if currentNode is targetNode, move towards targetPosition located inside targetNode (gradually slow down)
            desiredAcceleration = flowField.targetPosition - currentPosition;
        }
        else
        {
            // if currentNode is NOT targetNode, follow currentNode's flowDirection (max speed)
            desiredAcceleration = currentNode.flowDirection;
        }

        desiredAcceleration *= maxAcceleration;
        desiredAcceleration = Vector3.ClampMagnitude(desiredAcceleration, maxAcceleration);
        Vector3 steering = desiredAcceleration - accelerationToSteer;
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
    private Vector3 AvoidUnitsSteering(Vector3 accelerationToSteer)
    {
        if (numUnitsDetected > 1)
        {
            Vector3 desiredAcceleration = Vector3.zero;
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
                            desiredAcceleration += awayFromUnit;
                        }
                    }
                }
            }
            
            if (numUnitsToAvoid > 0)
            {
                desiredAcceleration /= numUnitsToAvoid;
                desiredAcceleration *= maxAcceleration;
                Vector3 steering = desiredAcceleration - accelerationToSteer;
                // only steer on the xy plane
                return new Vector3(steering.x, 0, steering.z);
            }
        }

        // if this statement is reached, it means there were no units within avoidance radius
        return Vector3.zero;
    }

}
