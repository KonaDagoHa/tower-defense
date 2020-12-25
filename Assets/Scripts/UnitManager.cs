using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class UnitManager : MonoBehaviour
{
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private FlowField flowField;
    private int invalidSpawnMask;

    private void Start()
    {
        invalidSpawnMask = LayerMask.GetMask("ImpassableTerrain");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnUnits(1);
        }
    }

    private void SpawnUnits(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Unit unit = Instantiate(unitPrefab, transform).GetComponent<Unit>();
            CapsuleCollider unitCollider = unit.GetComponent<CapsuleCollider>();
            Vector3 position;
            do
            {
                position = new Vector3(
                    Random.Range(-flowField.map.size.x / 2f, flowField.map.size.x / 2f),
                    unitCollider.height / 2f,
                    Random.Range(-flowField.map.size.y / 2f, flowField.map.size.y / 2f)
                );
            } while (Physics.CheckSphere(position, unitCollider.radius, invalidSpawnMask));
            unit.Initialize(flowField, position);
        }
    }
}
