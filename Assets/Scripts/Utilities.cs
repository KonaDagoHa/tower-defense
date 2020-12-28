using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utilities
{
    // returns true if current frame is divisible by n
    public static bool FrameIsDivisibleBy(int n)
    {
        return Time.frameCount % n == 0;
    }
}
