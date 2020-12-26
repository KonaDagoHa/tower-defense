using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class UnitSpawner : MonoBehaviour
{
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private FlowField flowField;

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
            Vector3 localSpawnPosition = Vector3.zero; // spawn position of unit relative to unit spawner
            unit.Initialize(flowField, localSpawnPosition);
        }
    }
}
