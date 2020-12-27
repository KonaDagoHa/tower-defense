using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private LayerMask unitsMask;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private Transform shooter;
    private float detectionRadius = 5;
    private Collider[] unitsToShoot = new Collider[1];
    private WaitForSeconds shootDelay = new WaitForSeconds(0.01f);
    private float destoryProjectileAfter = 1;
    private bool canShoot = true;

    private void Update()
    {
        ShootAtUnits();
    }

    private void ShootAtUnits()
    {
        if (canShoot)
        {
            if (Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, unitsToShoot, unitsMask) > 0)
            {
                Vector3 shootDirection = unitsToShoot[0].transform.position - transform.position;
                shooter.forward = shootDirection;
                GameObject projectileGO = Instantiate(projectilePrefab, projectileOrigin);
                Rigidbody projectileRB = projectileGO.GetComponent<Rigidbody>();
                projectileRB.AddForce(shootDirection * 15, ForceMode.VelocityChange);
                canShoot = false;
                StartCoroutine(ShootCooldown());
                Destroy(projectileGO, destoryProjectileAfter);
            }
        }
    }

    private IEnumerator ShootCooldown()
    {
        yield return shootDelay;
        canShoot = true;
    }
}
