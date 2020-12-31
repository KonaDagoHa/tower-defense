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

    // jumping
    private WaitForSeconds jumpDelay = new WaitForSeconds(2); // in seconds
    private bool canJump = true;

    // ragdoll
    public bool isRagdoll { get; private set; }
    private bool feetGrounded => Physics.CheckSphere(feet.position, 0.05f, terrainMask);
    private bool legsGrounded => Physics.CheckSphere(legs.position, 0.5f, terrainMask);
    private float initialRagdollAngularSpeed => Random.Range(5f, 10f); // this is used when hit by projectile
    private float ragdollRecoveryJumpSpeed => Random.Range(4f, 8f); // increase to increase recovery speed
    private float ragdollRecoveryRotationSpeed = 100; // in degrees per second (increase to increase recovery speed)
    private WaitForSeconds ragdollRecoveryDelay = new WaitForSeconds(1f); // how much time to wait before beginning to recover

    private void Awake()
    {
        selfRigidbody = GetComponent<Rigidbody>();
        selfCollider = GetComponent<Collider>();
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
        {
            SetRagdoll(true);
            selfRigidbody.AddTorque(Random.insideUnitSphere * initialRagdollAngularSpeed, ForceMode.VelocityChange);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Units"))
        {
            if (!other.rigidbody.isKinematic)
            {
                SetRagdoll(true);
            }
        }
        
    }

    private IEnumerator JumpCoolDown()
    {
        yield return jumpDelay;
        canJump = true;
    }

    private void Jump(Vector3 velocityChange)
    {
        // this only works when in ragdoll mode
        if (canJump)
        {
            selfRigidbody.AddForceAtPosition(velocityChange, feet.position, ForceMode.VelocityChange);
            canJump = false;
            StartCoroutine(JumpCoolDown());
        }
        
    }

    private void SetRagdoll(bool isActive)
    {
        if (isActive && !isRagdoll)
        {
            isRagdoll = true;
            selfRigidbody.isKinematic = false;
            StartCoroutine(RecoverFromRagdoll());
        }
        else if (!isActive && isRagdoll)
        {
            isRagdoll = false;
            selfRigidbody.isKinematic = true;
        }
    }

    private IEnumerator RecoverFromRagdoll()
    {
        // wait a short time before starting recovery so that isGrounded doesn't immediately return true and turn off ragdoll
        yield return ragdollRecoveryDelay;

        // strategy for recovery:
        // (1) jump upward at varying angles in an attempt to get air time (as well as dodge incoming projectiles)
        // (2) while in air after jumping, rotate self to be upright
        // (3) once landed, check if feet have touched the ground; if so, exit ragdoll state
        
        while (!feetGrounded)
        {
            if (legsGrounded)
            {
                if (canJump)
                {
                    // unit will jump upward at varying angles
                    Vector3 jumpDirection = Quaternion.Euler(
                        Random.Range(-45f, 45f),
                        Random.Range(-45f, 45f),
                        Random.Range(-45f, 45f)) * Vector3.up;
                    Vector3 velocityChange = jumpDirection * ragdollRecoveryJumpSpeed;
                    Jump(velocityChange);
                }
            }
            else // unit is in air
            {
                // unit try to adjust its rotation to be upright (so that feet can touch ground)
                Quaternion moveRotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.Euler(0, transform.eulerAngles.y, 0),
                    ragdollRecoveryRotationSpeed * Time.deltaTime
                );
                selfRigidbody.MoveRotation(moveRotation);
            
            }

            yield return new WaitForFixedUpdate();
        }
        
        SetRagdoll(false);
    }

}
