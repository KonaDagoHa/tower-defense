using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Tower : MonoBehaviour
{
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private Transform shooter;
    
    // aiming
    private float aimSpeed = 100;
    private Collider unitToShoot;

    // shooting
    private WaitForSeconds shootRate = new WaitForSeconds(0.1f);
    private bool canShoot = true;
    private float projectileSpeed = 15;
    private float destroyProjectileTime = 1;

    // unit detection
    private Collider[] unitsDetected = new Collider[8];
    private int numUnitsDetected;
    private float detectionRadius = 15;

    private void Update()
    {
        if (Utilities.FrameIsDivisibleBy(30))
        {
            // get all units within detectionRadius
            numUnitsDetected = Physics.OverlapSphereNonAlloc(
                transform.position, 
                detectionRadius, 
                unitsDetected, 
                unitsMask
            );
            
            unitToShoot = ClosestUnitDetected();
        }

        if (numUnitsDetected > 0)
        {
            AimShooterTowards(unitToShoot);
        }
    }

    private Collider ClosestUnitDetected()
    {
        // find the closest unit within detectionRadius
        if (numUnitsDetected > 0)
        {
            Collider closestUnit = unitsDetected[0];
            float smallestSqrMagnitude = (closestUnit.transform.position - transform.position).sqrMagnitude;
            for (int i = 1; i < numUnitsDetected; i++)
            {
                float sqrMagnitude = (unitsDetected[i].transform.position - transform.position).sqrMagnitude;
                if (sqrMagnitude < smallestSqrMagnitude)
                {
                    closestUnit = unitsDetected[i];
                    smallestSqrMagnitude = sqrMagnitude;
                }
            }

            return closestUnit;
        }

        // if this statement is reached, no unit was detected
        return null;
    }

    private void AimShooterTowards(Collider unit)
    {
        Vector3 direction = DirectionToShoot(unit);

        shooter.rotation = Quaternion.RotateTowards(
            shooter.rotation,
            Quaternion.LookRotation(direction),
            aimSpeed * Time.deltaTime
            );

        if (shooter.forward == direction)
        {
            ShootProjectile(direction);
        }
    }

    private Vector3 DirectionToShoot(Collider unit)
    {
        Vector3 origin = projectileOrigin.position;
        Vector3 target = unit.ClosestPoint(origin);
        Vector3 targetVelocity = unit.attachedRigidbody.velocity;

        float distanceToTarget = (target - origin).magnitude;
        float timeToTarget = distanceToTarget / projectileSpeed;
        
        Vector3 predictedPosition = target + targetVelocity * timeToTarget;

        float gravityOffset = 0.5f * Physics.gravity.y * timeToTarget * timeToTarget;
        
        Vector3 directionToShoot = predictedPosition - shooter.position;
        directionToShoot.y -= gravityOffset;

        return directionToShoot.normalized;
    }

    private void ShootProjectile(Vector3 direction)
    {
        if (canShoot)
        {
            GameObject projectileGO = Instantiate(projectilePrefab, projectileOrigin);
            projectileGO.transform.SetParent(null);
            Rigidbody projectileRB = projectileGO.GetComponent<Rigidbody>();
            projectileRB.AddForce(direction * projectileSpeed, ForceMode.VelocityChange);
            canShoot = false;
            StartCoroutine(ShootCooldown());
            Destroy(projectileGO, destroyProjectileTime);
        }
    }

    private IEnumerator ShootCooldown()
    {
        yield return shootRate;
        canShoot = true;
    }

    private void OnDrawGizmos()
    {
        if (unitToShoot)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(unitToShoot.ClosestPoint(projectileOrigin.position), 0.3f);
        }
        
    }
}
