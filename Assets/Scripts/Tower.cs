using System;
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
    private WaitForSeconds shootDelay = new WaitForSeconds(0.5f);
    private float destroyProjectileTime = 1;
    private bool canShoot = true;
    private Vector3 shootDirection = Vector3.forward;

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Units"))
        {
            shootDirection = other.transform.position - shooter.position;
            shooter.forward = shootDirection;
            Shoot();
        }
    }

    private void Shoot()
    {
        if (canShoot)
        {
            GameObject projectileGO = Instantiate(projectilePrefab, projectileOrigin);
            Rigidbody projectileRB = projectileGO.GetComponent<Rigidbody>();
            projectileRB.AddForce(shootDirection * 15, ForceMode.VelocityChange);
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
