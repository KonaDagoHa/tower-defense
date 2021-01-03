using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class UnitSpawner : MonoBehaviour
{
    [SerializeField] private GameObject unitPrefab;
    public Map map;
    public FlowField flowField;
    [SerializeField] private int spawnCount;


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnUnits(spawnCount);
        }
    }

    private void SpawnUnits(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject unitGO = Instantiate(unitPrefab, transform);
            Vector3 localSpawnPosition = Vector3.zero; // spawn position of unit relative to unit spawner
            unitGO.transform.localPosition = localSpawnPosition;
        }
    }
}
