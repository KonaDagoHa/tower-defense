using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// TODO: implement steering behaviors
    // http://www.red3d.com/cwr/steer/gdc99/
    // do this in a separate class
// TODO: instead of freezing rotation, allow rotation if unit is hit by something


public class Unit : MonoBehaviour
{
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private Transform legs;
    [SerializeField] private Transform feet;
    public Rigidbody selfRigidbody { get; private set; }
    public Collider selfCollider { get; private set; }
    
    // map
    public Map map { get; private set; }
    public FlowField flowField { get; private set; }
    
    // movement
    private UnitMovement movement;
    
    // jumping
    private WaitForSeconds jumpDelay = new WaitForSeconds(2); // in seconds
    private bool canJump = true;

    // ragdoll
    public bool isRagdoll { get; private set; }
    private bool feetGrounded => Physics.CheckSphere(feet.position, 0.05f, terrainMask);
    private bool legsGrounded => Physics.CheckSphere(legs.position, 0.5f, terrainMask);
    private float initialRagdollAngularSpeed => Random.Range(5f, 10f);
    private float ragdollRecoveryJumpSpeed => Random.Range(5f, 10f);

    public void Initialize(FlowField f, Vector3 localPosition)
    {
        selfRigidbody = GetComponent<Rigidbody>();
        selfCollider = GetComponent<Collider>();
        transform.localPosition = localPosition;
        flowField = f;
        map = flowField.map;

        movement = GetComponent<UnitMovement>();
        movement.Initialize();
    }

    private void FixedUpdate()
    {
        // unit turns into ragdoll if and only if feetGrounded == false
        
        if (map.grid != null)
        {
            if (isRagdoll)
            {
                RecoverFromRagdoll();
            }
            else if (!feetGrounded)
            {
                ToggleRagdoll(true);
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
