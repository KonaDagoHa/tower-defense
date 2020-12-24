using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Map : MonoBehaviour
{
    public Vector2 size = new Vector2(50, 50);
    public Vector3 bottomLeft => 
        transform.position - Vector3.right * size.x / 2 - Vector3.forward * size.y / 2;
}

