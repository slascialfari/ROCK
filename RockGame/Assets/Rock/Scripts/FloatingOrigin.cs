using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps coordinates small so physics and rendering stay precise after kilometres of travel.
// Called right before a kick (the rock is nearly at rest then), it moves every root object
// so the rock sits back near the origin.
public static class FloatingOrigin
{
    public static event Action<Vector3> Shifted;

    public static bool RecenterIfNeeded(Vector3 focus, float threshold)
    {
        focus.y = 0f;
        if (focus.magnitude < threshold) return false;

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            root.transform.position -= focus;
        Physics.SyncTransforms();
        Shifted?.Invoke(focus);
        return true;
    }
}
