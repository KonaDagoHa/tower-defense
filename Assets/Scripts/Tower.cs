﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: make it so tower linearly rotates towards unit before shooting

public class Tower : MonoBehaviour
{
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private Transform shooter;
    private WaitForSeconds shootDelay = new WaitForSeconds(0.1f);
    private float projectileSpeed = 15;
    private float destroyProjectileTime = 1;
    private bool canShoot = true;
    private float aimSpeed = 200;
    
    // shooting
    private Collider unitToShoot;
    
    // unit detection
    private Collider[] unitsDetected = new Collider[8];
    private int numUnitsDetected;
    private float detectionRadius = 10;
    
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
        Vector3 shootDirection = (unit.ClosestPoint(projectileOrigin.position) - shooter.position).normalized;
        
        shooter.rotation = Quaternion.RotateTowards(
            shooter.rotation,
            Quaternion.LookRotation(shootDirection),
            aimSpeed * Time.deltaTime
            );
        
        if (shooter.forward == shootDirection)
        {
            ShootProjectile(shootDirection);
        }
    }

    private void ShootProjectile(Vector3 shootDirection)
    {
        if (canShoot)
        {
            GameObject projectileGO = Instantiate(projectilePrefab, projectileOrigin);
            projectileGO.transform.SetParent(null);
            Rigidbody projectileRB = projectileGO.GetComponent<Rigidbody>();
            projectileRB.AddForce(shootDirection * projectileSpeed, ForceMode.VelocityChange);
            canShoot = false;
            StartCoroutine(ShootCooldown());
            Destroy(projectileGO, destroyProjectileTime);
        }
    }

    private IEnumerator ShootCooldown()
    {
        yield return shootDelay;
        canShoot = true;
    }
}
