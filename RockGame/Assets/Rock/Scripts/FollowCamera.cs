using UnityEngine;

// Low third-person camera. It mostly faces down the road and leans slightly toward the
// rock's travel direction; bounces are only partially followed so the view stays calm.
[RequireComponent(typeof(Camera))]
public class FollowCamera : MonoBehaviour
{
    Rock rock;
    RockTuning t;
    Camera cam;
    Vector3 positionVelocity, lookPoint, lookVelocity;
    Vector3 heading = Vector3.forward;
    float shake;

    public Vector3 Heading => heading;

    public void Init(Rock rock, RockTuning tuning)
    {
        this.rock = rock;
        t = tuning;
        cam = GetComponent<Camera>();
        lookPoint = LookTarget();
        transform.position = DesiredPosition();
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
        FloatingOrigin.Shifted += OnWorldShifted;
    }

    void OnDestroy() => FloatingOrigin.Shifted -= OnWorldShifted;

    public void Punch(float amount) => shake = Mathf.Max(shake, amount);

    void LateUpdate()
    {
        if (rock == null) return;
        float dt = Time.deltaTime;
        cam.fieldOfView = t.cameraFov;

        Vector3 v = rock.Body.linearVelocity;
        v.y = 0f;
        Vector3 targetHeading = Vector3.forward;
        if (v.z > 0.5f) targetHeading = Vector3.Slerp(Vector3.forward, v.normalized, t.cameraYawFollow);
        heading = Vector3.Slerp(heading, targetHeading, 1f - Mathf.Exp(-t.cameraYawSharpness * dt)).normalized;

        transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref positionVelocity, t.cameraSmoothTime);
        lookPoint = Vector3.SmoothDamp(lookPoint, LookTarget(), ref lookVelocity, t.cameraLookSmoothTime);

        Quaternion rotation = Quaternion.LookRotation(lookPoint - transform.position);
        if (shake > 0.001f)
        {
            float n = Time.time * 30f;
            Vector3 wobble = new Vector3(Mathf.PerlinNoise(n, 0f) - 0.5f, Mathf.PerlinNoise(0f, n) - 0.5f, 0f);
            rotation *= Quaternion.Euler(wobble * (2f * shake * t.cameraShake));
            shake = Mathf.MoveTowards(shake, 0f, dt * 4f);
        }
        transform.rotation = rotation;
    }

    Vector3 DesiredPosition()
    {
        Vector3 r = rock.transform.position;
        return new Vector3(r.x, 0f, r.z) - heading * t.cameraDistance
               + Vector3.up * (t.cameraHeight + r.y * t.cameraVerticalFollow);
    }

    Vector3 LookTarget()
    {
        Vector3 r = rock.transform.position;
        return new Vector3(r.x, r.y * t.cameraVerticalFollow + t.cameraLookHeight, r.z) + heading * t.cameraLookAhead;
    }

    void OnWorldShifted(Vector3 offset) => lookPoint -= offset;
}
