using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildMode : MonoBehaviour
{
    [SerializeField] private GameObject towerPrefab;

    private bool isActive = false;

    public void ToggleBuildMode()
    {
        isActive = !isActive;
    }
}
