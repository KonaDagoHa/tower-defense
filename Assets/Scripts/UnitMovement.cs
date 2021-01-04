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

// note: some steering behaviors are incompatible (ex: obstacle avoidance and seek/follow flow field)
// only use obstacle avoidance when pathfinding is not required (ex: wandering and obstacle avoidance)

// right now, the only steering behaviors we need are follow flow field, arrival, and separation (and may cohesion + alignment, needs testing)
// FIXME: when in a crowd, units sometimes refuse to go through rough terrain even though doing so will be faster

// To fix issue where units "bounce" on each other when standing still, make sure the queue distance and radius is large enough to cover the valid units from separation steering

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
    
    // movement (done in FixedUpdate())
    private Vector3 targetPosition; // position that unit wants to go to
    private float maxVelocity = 5f; // controls how fast unit can move
    private float maxSteering = 0.3f; // controls how fast unit gets to maxVelocity (change in velocity = acceleration * time)
    private float maxAngularVelocity = 250; // controls how fast unit rotates (in degrees per second)
    private Vector3 currentVelocity;
    private Vector3 previousPosition;
    
    // steering behaviors
    private Collider[] unitsDetected = new Collider[8];
    private int numUnitsDetected;
    private float unitDetectionRadius = 4;
    private int steeringCount; // number of steering behaviors active
    // arrival
    private bool isArriving;
    private float arrivalRadius = 2; // once this unit is within this distance of the targetPosition, slow down gradually
    // flow field
    private float flowFieldPredictionTime = 0.2f; // how much time in seconds into the future the predicted position will be (between 0 and 0.5 works well)
    // queue
    private bool isQueueing;
    private float queueAheadDistance = 0.5f; // Vector3 ahead = transform.forward * queueAheadDistance (distance ahead of unit)
    private float queueRadius = 1;
    // separation
    private float separationRadius = 1f; // other units within this distance will trigger the separation steering behavior

    [Serializable]
    public class SteeringWeights
    {
        public float arrival = 1;
        public float flowField = 1;
        public float separation = 1;
        public float queue = 1;
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
        if (Utilities.FrameIsDivisibleBy(10))
        {
            if (!selfUnit.isRagdoll)
            {
                // get all units within detectionRadius
                numUnitsDetected = Physics.OverlapSphereNonAlloc(
                    transform.position, 
                    unitDetectionRadius, 
                    unitsDetected, 
                    unitsMask
                );
            }
        }
    }

    private void FixedUpdate()
    {
        previousPosition = transform.position;
        if (map.grid != null && !selfUnit.isRagdoll)
        {
            UpdatePosition();
            UpdateRotation();
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
        targetPosition = flowField.targetPosition; // delete this later
        Vector3 steering = TotalSteering();
        Vector3 newVelocity = currentVelocity + steering;
        newVelocity = Vector3.ClampMagnitude(newVelocity, maxVelocity);

        // move unit according to new velocity
        Vector3 positionChange = (currentVelocity + newVelocity) * (0.5f * Time.deltaTime);
        selfRigidbody.MovePosition(transform.position + positionChange);
        currentVelocity = newVelocity;
    }

    private void UpdateRotation()
    {
        // rotate unit so that it faces current velocity; also makes sure unit is upright
        float sqrVelocity = currentVelocity.sqrMagnitude;
        if (sqrVelocity > 1)
        {
            float rotationScale = sqrVelocity / (maxVelocity * maxVelocity); // angular velocity is highest when velocity is highest
            Quaternion newRotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(currentVelocity.x, 0, currentVelocity.z)),
                maxAngularVelocity * Time.deltaTime * rotationScale
            );
            selfRigidbody.MoveRotation(newRotation);
        }
        
    }

    private Vector3 TotalSteering()
    {
        steeringCount = 0;
        Vector3 steering = Vector3.zero; // change in velocity = acceleration * time
        
        steering += QueueSteering() * weights.queue; // this also updates isQueueing
        steering += SeparationSteering() * weights.separation;
        
        if (!isQueueing)
        {
            steering += ArrivalSteering() * weights.arrival; // this also updates isArriving
            if (!isArriving) // is moving according to flow field
            {
                steering += FlowFieldSteering() * weights.flowField;
            }
        }

        if (steeringCount > 0)
        {
            steering /= steeringCount; // find average length
            steering = Vector3.ClampMagnitude(steering, maxSteering);
            return steering;
        }
        return Vector3.zero;
    }

    // terrain/obstacles steering behaviors
    private Vector3 ArrivalSteering()
    {
        Vector3 towardsTarget = targetPosition - transform.position;
        Vector3 towardsTargetXZ = new Vector3(towardsTarget.x, 0, towardsTarget.z); // ignore the y axis
        float sqrDistance = towardsTargetXZ.sqrMagnitude;
        float sqrArrivalRadius = arrivalRadius * arrivalRadius;
        if (sqrDistance <= sqrArrivalRadius) // check if unit is within arrival distance
        {
            isArriving = true;
            Vector3 desiredVelocity = towardsTargetXZ.normalized * maxVelocity;
            desiredVelocity *= sqrDistance / sqrArrivalRadius; // speed decreases exponentially as unit approaches targetPosition
            // calculate steering
            Vector3 steering = desiredVelocity - currentVelocity;
            steeringCount++;
            return new Vector3(steering.x, 0, steering.z);
        }

        isArriving = false;
        // if this line is reached, unit was NOT within arrival distance
        return Vector3.zero;
    }

    private Vector3 FlowFieldSteering()
    {
        Vector3 predictedPosition = transform.position + currentVelocity * flowFieldPredictionTime;
        Node predictedNode = map.WorldToNode(predictedPosition);
        Vector3 desiredVelocity;
        if (predictedNode == flowField.targetNode) // this is so unit doesn't just stop at the edge of target node
        {
            // if evaluated node is targetNode, move towards targetPosition located inside targetNode (gradually slow down)
            desiredVelocity = targetPosition - predictedPosition;
        }
        else
        {
            // if evaluated node is NOT targetNode, follow flowDirection (max speed)
            desiredVelocity = predictedNode.flowDirection;
        }

        desiredVelocity *= maxVelocity;
        desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, maxVelocity);
        // calculate steering
        Vector3 steering = desiredVelocity - currentVelocity; // change in velocity
        if (steering != Vector3.zero)
        {
            steeringCount++;
            return new Vector3(steering.x, 0, steering.z);
        }

        return Vector3.zero;
    }

    // units steering behaviors

    private Vector3 QueueSteering()
    {
        if (numUnitsDetected > 1)
        {
            Vector3 ahead = transform.position + currentVelocity.normalized * queueAheadDistance; // position ahead of self unit
            for (int i = 0; i < numUnitsDetected; i++)
            {
                Collider unitCollider = unitsDetected[i];
                if (unitCollider != selfCollider)
                {
                    Vector3 unitPosition = unitCollider.transform.position;
                    Vector3 aheadToUnit = unitPosition - ahead;
                    //Vector3 selfToUnit = unitPosition - transform.position;
                    // slow down to zero velocity if there is a unit ahead of self
                    if (aheadToUnit.sqrMagnitude <= queueRadius * queueRadius)
                    {
                        /*
                        if (selfToUnit.sqrMagnitude <= queueRadius * queueRadius) // unit is very close to self (crowded)
                        {
                            currentVelocity *= 0.3f;
                        }
                        */
                        Vector3 steering = -currentVelocity;
                        if (steering != Vector3.zero)
                        {
                            isQueueing = true;
                            steeringCount++;
                            return new Vector3(steering.x, 0, steering.z);
                        }
                    }
                }
            }
        }

        isQueueing = false;
        // if this line is reached, there was no unit ahead of self unit
        return Vector3.zero;
    }
    
    private Vector3 AvoidUnitsSteering()
    {

        return Vector3.zero;
    }

    private Vector3 SeparationSteering()
    {
        if (numUnitsDetected > 1)
        {
            Vector3 desiredVelocity = Vector3.zero;
            int numUnitsValid = 0;
            for (int i = 0; i < numUnitsDetected; i++)
            {
                Collider unitCollider = unitsDetected[i];
                // if unitCollider does not belong to the unit calling the function
                if (unitCollider != selfCollider)
                {
                    Vector3 awayFromUnit = transform.position - unitCollider.transform.position;
                    
                    float sqrMagnitude = awayFromUnit.sqrMagnitude;
                    // if awayFromUnit is not a zero vector AND the unit is within avoidanceRadius
                    if (sqrMagnitude != 0 && sqrMagnitude <= separationRadius * separationRadius)// && CheckWithinFOV(unitCollider.transform.position, 90))
                    {
                        numUnitsValid++;
                        // scale so that closer to this unit => higher desiredVelocity magnitude
                        awayFromUnit /= sqrMagnitude; // this normalizes and scales magnitude to be between 0 and 1
                        desiredVelocity += awayFromUnit;
                    }
                }
            }
            
            if (numUnitsValid > 0)
            {
                desiredVelocity /= numUnitsValid; // find average magnitude (desiredVelocity is now between 0 and 1)
                desiredVelocity *= maxVelocity; // scale to maxVelocity (desiredVelocity should now be between 0 and maxVelocity)
                desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, maxVelocity);
                Vector3 steering = desiredVelocity - currentVelocity;
                if (steering != Vector3.zero)
                {
                    steeringCount++;
                    return new Vector3(steering.x, 0, steering.z);
                }

                return Vector3.zero;
            }
        }

        // if this statement is reached, it means there were no units within separation radius
        return Vector3.zero;
    }

}
