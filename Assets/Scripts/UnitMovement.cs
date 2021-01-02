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

    // movement (done in FixedUpdate())
    private Vector3 targetPosition; // position that unit wants to go to
    private float maxVelocity = 5f; // controls how fast unit can move
    private float maxSteering = 0.5f; // controls how fast unit gets to maxVelocity (change in velocity = acceleration * time)
    private Vector3 currentVelocity;

    private float maxAngularVelocity = 300;
    private float maxAngularSteering = 100;
    private float currentAngularVelocity;


    // steering
    private bool isArriving;
    private float arrivalDistance = 2; // once this unit is within this distance of the targetPosition, slow down gradually
    private float separationDistance = 1; // other units within this distance will trigger the separation steering behavior

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
            UpdateRotation();
            /*
            // rotate unit so that it faces current velocity; also makes sure unit is upright
            Quaternion moveRotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(currentVelocity.x, 0, currentVelocity.z)),
                maxAngularVelocity * Time.deltaTime
            );
            selfRigidbody.MoveRotation(moveRotation);
            */
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
        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z);
        float angularSteering = Vector3.SignedAngle(forward, currentVelocity, Vector3.up);
        angularSteering = Mathf.Clamp(angularSteering, -maxAngularSteering, maxAngularSteering);
        float newAngularVelocity = currentAngularVelocity + angularSteering;
        newAngularVelocity = Mathf.Clamp(newAngularVelocity, -maxAngularVelocity, maxAngularVelocity);
        
        // rotate unit according to new angular velocity
        float angleChange = (currentAngularVelocity + newAngularVelocity) * (0.5f * Time.deltaTime);
        Vector3 newForward = Quaternion.Euler(0, angleChange, 0) * forward;
        selfRigidbody.MoveRotation(Quaternion.LookRotation(newForward, Vector3.up));
        currentAngularVelocity = newAngularVelocity;
    }

    private Vector3 TotalSteering()
    {
        Vector3 steering = Vector3.zero; // change in velocity = acceleration * time
        steering += ArrivalSteering();
        if (!isArriving)
        {
            steering += FlowFieldSteering() * weights.flowField;
        }
        
        steering += SeparationSteering() * weights.separation;
        steering /= 3; // find average length
        steering = Vector3.ClampMagnitude(steering, maxSteering);
        return steering;
    }

    private Vector3 ArrivalSteering()
    {
        Vector3 towardsTarget = targetPosition - transform.position;
        Vector3 towardsTargetXZ = new Vector3(towardsTarget.x, 0, towardsTarget.z); // ignore the y axis
        float sqrDistance = towardsTargetXZ.sqrMagnitude;
        if (sqrDistance <= arrivalDistance * arrivalDistance) // check if unit is within arrival distance
        {
            isArriving = true;
            Vector3 desiredVelocity = towardsTargetXZ.normalized * maxVelocity;
            desiredVelocity *= Mathf.Sqrt(sqrDistance) / arrivalDistance; // speed decreases as unit approaches targetPosition
            Vector3 steering = desiredVelocity - currentVelocity;
            return new Vector3(steering.x, 0, steering.z);
        }

        isArriving = false;
        // if this line is reached, unit was NOT within arrival distance
        return Vector3.zero;
    }

    // use this for long distance pathfinding
    private Vector3 FlowFieldSteering()
    {
        targetPosition = flowField.targetPosition; // change/delete this later
        Vector3 futurePosition = transform.position + currentVelocity * Time.deltaTime; // TODO: adjust future position by change Time.deltaTime to a higher value in seconds
        Node futureNode = map.WorldToNode(futurePosition);
        Vector3 desiredVelocity;
        if (futureNode == flowField.targetNode) // this is so unit doesn't just stop at the edge of target node
        {
            // if evaluated node is targetNode, move towards targetPosition located inside targetNode (gradually slow down)
            desiredVelocity = targetPosition - futurePosition;
        }
        else
        {
            // if evaluated node is NOT targetNode, follow flowDirection (max speed)
            desiredVelocity = futureNode.flowDirection;
        }

        desiredVelocity *= maxVelocity;
        desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, maxVelocity);
        Vector3 steering = desiredVelocity - currentVelocity; // change in velocity
        return new Vector3(steering.x, 0, steering.z);
    }
    
    // use this for short distance pathfinding (like following an enemy)
    private Vector3 SeekSteering()
    {
        // TODO: IMPLEMENT THIS
        return Vector3.zero;
    }

    // use this for short distance pathfinding (like following an enemy)
    private Vector3 AvoidObstaclesSteering()
    {
        // TODO: IMPLEMENT THIS
        return Vector3.zero;
    }
    
    // use this for all pathfinding
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
                    if (sqrMagnitude != 0 && sqrMagnitude <= separationDistance * separationDistance)
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
                desiredVelocity /= numUnitsValid; // find average magnitude
                desiredVelocity *= maxVelocity; // scale to maxVelocity
                desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, maxVelocity);
                Vector3 steering = desiredVelocity - currentVelocity;
                // only steer on the xz plane
                return new Vector3(steering.x, 0, steering.z);
            }
        }

        // if this statement is reached, it means there were no units within avoidance radius
        return Vector3.zero;
    }

}
