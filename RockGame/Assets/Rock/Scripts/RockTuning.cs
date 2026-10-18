using UnityEngine;

// Every feel-related number lives here. Edit Assets/Rock/RockTuning.asset while in Play mode:
// ScriptableObject changes survive leaving Play mode, so tuning sessions are not lost.
[CreateAssetMenu(menuName = "ROCK/Tuning", fileName = "RockTuning")]
public class RockTuning : ScriptableObject
{
    [Header("New rock generation (each player's rock is random within these ranges)")]
    public float rockRadius = 0.07f;
    [Range(0, 24)] public int rockCutsMin = 8;
    [Range(0, 24)] public int rockCutsMax = 14;
    [Range(0.3f, 1f)] public float rockCutDepthMin = 0.55f;
    [Range(0.3f, 1f)] public float rockCutDepthMax = 0.85f;
    public Vector3 rockSquashMin = new Vector3(1.0f, 0.68f, 0.85f);
    public Vector3 rockSquashMax = new Vector3(1.25f, 0.9f, 1.05f);
    [Range(0f, 0.05f)] public float rockSurfaceJitter = 0.008f;

    [Header("Erosion (scaled by the dev menu's erosion speed)")]
    [Tooltip("Wear from one impact at 5 m/s.")]
    public float impactWear = 0.8f;
    [Tooltip("Wear per metre rolled or slid along the ground.")]
    public float abrasionWear = 0.2f;
    [Tooltip("Size of the worn patch around a contact point (degrees).")]
    public float erosionPatchAngle = 35f;
    [Tooltip("How much hollows fill in compared to how much bumps wear down (below 1 = the rock slowly shrinks).")]
    [Range(0f, 1f)] public float erosionFill = 0.5f;
    public float chipMinSpeed = 3.5f;
    [Range(0f, 1f)] public float chipChance = 0.12f;
    [Tooltip("Chip depth as a fraction of the local radius.")]
    public float chipDepthMin = 0.02f;
    public float chipDepthMax = 0.06f;
    [Tooltip("No more chips once the rock is this round.")]
    [Range(0f, 1f)] public float chipMaxRoundness = 0.95f;
    [Tooltip("Above this roundness the rock collides as a true sphere and rolls like a ball.")]
    [Range(0f, 1f)] public float sphereColliderRoundness = 0.9f;

    [Header("Rock physics")]
    [Tooltip("kg/m³ — mass follows the eroded volume.")]
    public float rockDensity = 2600f;
    [Tooltip("Rolling resistance coefficient: deceleration = this × g while on the ground.")]
    public float rollingResistance = 0.04f;
    public float rockFriction = 0.6f;
    public float rockStaticFriction = 0.75f;
    [Range(0f, 1f)] public float rockBounciness = 0.3f;
    public float linearDamping = 0.02f;
    public float angularDamping = 0.12f;
    public float maxAngularVelocity = 200f;
    [Tooltip("Below these speeds (while touching the ground) extra damping helps the rock come to rest instead of rocking forever.")]
    public float settleSpeed = 0.4f;
    public float settleAngularSpeed = 3f;
    public float settleLinearDamping = 1.5f;
    public float settleAngularDamping = 4f;

    [Header("Kick")]
    [Tooltip("The rock can only be kicked when slower than this (m/s).")]
    public float kickableSpeed = 1.2f;
    public float kickSpeedMin = 1.0f;
    public float kickSpeedMax = 12f;
    [Tooltip("Launch angle above the ground (degrees) for the weakest / strongest kick.")]
    public float kickLiftMin = 3f;
    public float kickLiftMax = 20f;
    [Tooltip("Forward roll added on kick, as a fraction of pure rolling spin.")]
    [Range(0f, 1f)] public float kickRollSpin = 0.35f;
    [Tooltip("Random tumble added on kick, as a fraction of pure rolling spin.")]
    [Range(0f, 1f)] public float kickRandomSpin = 0.35f;
    public float maxSteerAngle = 40f;
    [Range(0f, 1.5f)] public float steerSensitivity = 0.85f;

    [Header("Swipe (lengths in screen widths, speeds in screen widths / second; width = short side)")]
    public float swipeMinLength = 0.05f;
    public float swipeFullLength = 0.4f;
    public float swipeSpeedMin = 0.3f;
    public float swipeSpeedFull = 3.5f;
    public float swipePowerCurve = 1.3f;
    [Tooltip("Flick speed is measured over the last part of the swipe (seconds).")]
    public float swipeFlickWindow = 0.08f;

    [Header("Camera")]
    public float cameraFov = 68f;
    public float cameraDistance = 1.0f;
    public float cameraHeight = 0.45f;
    public float cameraLookHeight = 0.06f;
    public float cameraLookAhead = 2.0f;
    [Range(0f, 1f)] public float cameraVerticalFollow = 0.4f;
    public float cameraSmoothTime = 0.22f;
    public float cameraLookSmoothTime = 0.06f;
    [Tooltip("0 = camera always faces down the road, 1 = fully follows the rock's travel direction.")]
    [Range(0f, 1f)] public float cameraYawFollow = 0.3f;
    public float cameraYawSharpness = 2f;
    [Tooltip("Shake on a full-power kick, in degrees.")]
    public float cameraShake = 1.2f;

    [Header("World")]
    public float roadWidth = 7f;
    public float segmentLength = 14f;
    public float groundWidth = 300f;
    public int segmentsAhead = 22;
    public int segmentsBehind = 3;
    public float roadFriction = 0.7f;
    [Range(0f, 1f)] public float roadBounciness = 0.2f;
    [Tooltip("Recenter the world when the rock is this far from the origin (m).")]
    public float floatingOriginThreshold = 1000f;

    [Header("Haptics")]
    public float impactHapticMinSpeed = 0.8f;
    public float impactHapticFullSpeed = 6f;
}
