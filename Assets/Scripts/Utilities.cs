using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utilities
{
    private static int fixedFrameCount => Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
    
    // call this in Update() only
    public static bool FrameIsDivisibleBy(int n)
    {
        return Time.frameCount % n == 0;
    }
}
