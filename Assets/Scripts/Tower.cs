using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: make it so tower linearly rotates towards unit before shooting

public class Tower : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private Transform shooter;
    private WaitForSeconds shootDelay = new WaitForSeconds(0.5f);
    private float projectileSpeed = 15;
    private float destroyProjectileTime = 1;
    private bool canShoot = true;
    private float aimSpeed = 200;

    // on trigger stay is not that efficient because multiple AimShooterTowards() is being called multiple times per frame
    // TODO: replace OnTriggerStay with a detection method that prioritizes closest enemies within detection range
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Units"))
        {
            Vector3 shootDirection = (other.ClosestPoint(projectileOrigin.position) - shooter.position).normalized;
            AimShooterTowards(shootDirection);
        }
    }

    private void AimShooterTowards(Vector3 shootDirection)
    {
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
