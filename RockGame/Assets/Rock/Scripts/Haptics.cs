using System.Runtime.InteropServices;
using UnityEngine;

// Thin wrapper over UIImpactFeedbackGenerator (Assets/Plugins/iOS/RockHaptics.mm).
// No-op in the editor and on other platforms.
public static class Haptics
{
    public enum Style { Light = 0, Medium = 1, Heavy = 2, Soft = 3, Rigid = 4 }

    public static bool Enabled = true;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void RockHaptics_Impact(int style, float intensity);
#endif

    public static void Impact(Style style, float intensity)
    {
        if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
        RockHaptics_Impact((int)style, Mathf.Clamp01(intensity));
#endif
    }
}
