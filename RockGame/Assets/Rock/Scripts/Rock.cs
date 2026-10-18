using System;
using UnityEngine;

public class Rock : MonoBehaviour
{
    public Rigidbody Body { get; private set; }
    public RockErosion Erosion { get; private set; }
    public int Kicks { get; private set; }
    public float Distance { get; private set; }
    public int Chips { get; private set; }
    public bool IsSphereCollider => sphere.enabled;
    public bool CanKick => Body.linearVelocity.magnitude < t.kickableSpeed;

    // Multiplies all wear. 0 turns erosion off. Set from the dev menu.
    public float ErosionSpeed { get; set; } = 1f;

    public event Action<float> Impact; // impact speed (m/s)
    public event Action Chipped;
    public event Action Settled;       // came to rest after a kick

    RockTuning t;
    MeshFilter filter;
    MeshCollider hull;
    SphereCollider sphere;
    float lastContactTime = -1f;
    Vector3 lastContactPoint;
    bool restoreInterpolation;
    bool awaitingSettle;
    bool collidersDirty;
    float lastMeshUpdate, lastColliderUpdate;

    public void Init(RockTuning tuning, Material material, PhysicsMaterial surface, RockErosion erosion)
    {
        t = tuning;

        // Colliders go on before the MeshFilter has a mesh, otherwise Unity would grab the
        // 5k-triangle render mesh as a (non-convex) collider.
        filter = gameObject.AddComponent<MeshFilter>();
        hull = gameObject.AddComponent<MeshCollider>();
        hull.convex = true;
        hull.sharedMaterial = surface;
        sphere = gameObject.AddComponent<SphereCollider>();
        sphere.sharedMaterial = surface;
        sphere.enabled = false;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;

        Body = gameObject.AddComponent<Rigidbody>();
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Body.solverIterations = 12;
        Body.solverVelocityIterations = 4;
        Body.maxAngularVelocity = t.maxAngularVelocity;

        SetErosion(erosion);
        FloatingOrigin.Shifted += OnWorldShifted;
    }

    void OnDestroy() => FloatingOrigin.Shifted -= OnWorldShifted;

    public void RestoreStats(int kicks, float distance, int chips)
    {
        Kicks = kicks;
        Distance = distance;
        Chips = chips;
    }

    // Swap in a brand new rock at the given position, with all stats cleared.
    public void ResetTo(RockErosion erosion, Vector3 position)
    {
        var old = Erosion;
        SetErosion(erosion);
        old?.Dispose();
        RestoreStats(0, 0f, 0);
        awaitingSettle = false;
        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        Body.interpolation = RigidbodyInterpolation.None;
        restoreInterpolation = true;
        transform.SetPositionAndRotation(position, Quaternion.Euler(10f, UnityEngine.Random.Range(0f, 360f), 5f));
        Physics.SyncTransforms();
    }

    public void Kick(Vector3 direction, float power)
    {
        direction.y = 0f;
        direction.Normalize();
        float speed = Mathf.Lerp(t.kickSpeedMin, t.kickSpeedMax, power);
        float lift = Mathf.Lerp(t.kickLiftMin, t.kickLiftMax, power) * Mathf.Deg2Rad;
        Vector3 launch = direction * Mathf.Cos(lift) + Vector3.up * Mathf.Sin(lift);
        Vector3 rollAxis = Vector3.Cross(Vector3.up, direction);
        float rollingSpin = speed / Mathf.Max(Erosion.MeanRadius, 0.01f);

        Body.WakeUp();
        Body.AddForce(launch * speed, ForceMode.VelocityChange);
        Body.AddTorque(rollAxis * (rollingSpin * t.kickRollSpin)
                       + UnityEngine.Random.insideUnitSphere * (rollingSpin * t.kickRandomSpin),
                       ForceMode.VelocityChange);
        Kicks++;
        awaitingSettle = true;
    }

    void Update()
    {
        // Mesh at most 20x/s, collider at most 4x/s: erosion is slow, the eye won't notice.
        if (Time.time - lastMeshUpdate >= 0.05f && Erosion.Apply())
        {
            lastMeshUpdate = Time.time;
            collidersDirty = true;
        }
        if (collidersDirty && Time.time - lastColliderUpdate >= 0.25f) RefreshColliders();
    }

    void FixedUpdate()
    {
        if (restoreInterpolation)
        {
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            restoreInterpolation = false;
        }

        float dt = Time.fixedDeltaTime;
        Vector3 v = Body.linearVelocity;
        float speed = v.magnitude;
        Vector3 horizontal = new Vector3(v.x, 0f, v.z);
        float horizontalSpeed = horizontal.magnitude;
        Distance += horizontalSpeed * dt;

        bool grounded = Time.time - lastContactTime < 0.1f;
        if (grounded && speed > 0.05f)
        {
            // Abrasion while rolling or sliding.
            Erode(lastContactPoint, speed * dt * t.abrasionWear);

            // PhysX has no rolling resistance; without it a round rock would roll for ever.
            if (horizontalSpeed > 0.01f)
                Body.AddForce(-horizontal / horizontalSpeed * Mathf.Min(t.rollingResistance * 9.81f, horizontalSpeed / dt), ForceMode.Acceleration);
        }

        bool settling = grounded && speed < t.settleSpeed && Body.angularVelocity.magnitude < t.settleAngularSpeed;
        Body.linearDamping = settling ? t.settleLinearDamping : t.linearDamping;
        Body.angularDamping = settling ? t.settleAngularDamping : t.angularDamping;
        Body.maxAngularVelocity = t.maxAngularVelocity;

        if (awaitingSettle && grounded && speed < t.settleSpeed)
        {
            awaitingSettle = false;
            Settled?.Invoke();
        }

        // Safety net: if the rock ever tunnels through the floor, put it back on the road.
        if (Body.position.y < -5f)
        {
            Body.position = new Vector3(Body.position.x, t.rockRadius + 0.2f, Body.position.z);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        lastContactTime = Time.time;
        float speed = collision.relativeVelocity.magnitude;
        if (collision.contactCount > 0)
        {
            Vector3 point = collision.GetContact(0).point;
            lastContactPoint = point;
            Erode(point, t.impactWear * Mathf.Pow(speed / 5f, 1.5f));
            TryChip(point, speed);
        }
        Impact?.Invoke(speed);
    }

    void OnCollisionStay(Collision collision)
    {
        lastContactTime = Time.time;
        if (collision.contactCount > 0) lastContactPoint = collision.GetContact(0).point;
    }

    void Erode(Vector3 worldPoint, float amount)
    {
        if (ErosionSpeed <= 0f || amount <= 0f) return;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f) return;
        Erosion.Erode(local.normalized, amount * ErosionSpeed, t.erosionPatchAngle, t.erosionFill);
    }

    void TryChip(Vector3 worldPoint, float speed)
    {
        if (ErosionSpeed <= 0f || speed < t.chipMinSpeed) return;
        float roundnessFactor = Mathf.Clamp01(1f - Erosion.Roundness / Mathf.Max(t.chipMaxRoundness, 0.01f));
        float chance = t.chipChance * Mathf.Min(1f, ErosionSpeed) * roundnessFactor;
        if (UnityEngine.Random.value >= chance) return;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (local.sqrMagnitude < 1e-8f) return;
        Erosion.Chip(local.normalized, UnityEngine.Random.Range(t.chipDepthMin, t.chipDepthMax));
        Chips++;
        Chipped?.Invoke();
    }

    void SetErosion(RockErosion erosion)
    {
        Erosion = erosion;
        filter.sharedMesh = erosion.Mesh;
        RefreshColliders();
    }

    void RefreshColliders()
    {
        bool round = Erosion.Roundness >= t.sphereColliderRoundness;
        sphere.enabled = round;
        sphere.radius = Erosion.MeanRadius;
        sphere.center = Vector3.zero;
        hull.enabled = !round;
        if (!round)
        {
            hull.sharedMesh = null; // force PhysX to re-cook the eroded hull
            hull.sharedMesh = Erosion.HullMesh;
        }
        Body.mass = Mathf.Max(0.05f, Erosion.Volume * t.rockDensity);
        collidersDirty = false;
        lastColliderUpdate = Time.time;
    }

    // A world shift teleports the rock; skip interpolation for one step so it doesn't
    // visibly slide from its old position.
    void OnWorldShifted(Vector3 offset)
    {
        lastContactPoint -= offset;
        Body.interpolation = RigidbodyInterpolation.None;
        restoreInterpolation = true;
    }
}
