using System;
using System.Collections.Generic;
using UnityEngine;

// Turns a finger (or mouse) swipe into a kick request: screen-space direction + power 0..1.
// Power mostly comes from how fast the swipe is at release (the "flick"), scaled by its length.
public class SwipeInput : MonoBehaviour
{
    public event Action<Vector2, float> Swiped;

    // Returns true if a touch starting at this screen position belongs to UI, not to kicking.
    public Func<Vector2, bool> Blocker;

    struct Sample { public Vector2 Position; public float Time; }

    RockTuning t;
    readonly Queue<Sample> recent = new Queue<Sample>();
    bool down, tracking;
    Vector2 start;
    Sample last;

    public void Init(RockTuning tuning) => t = tuning;

    void Update()
    {
        if (t == null || !TouchPointer.Read(out bool pressed, out Vector2 position)) return;
        float now = Time.unscaledTime;

        if (pressed && !down)
        {
            down = true;
            tracking = Blocker == null || !Blocker(position);
            start = position;
            recent.Clear();
        }

        if (pressed && tracking)
        {
            last = new Sample { Position = position, Time = now };
            recent.Enqueue(last);
            while (recent.Count > 1 && recent.Peek().Time < now - t.swipeFlickWindow) recent.Dequeue();
        }
        else if (!pressed && down)
        {
            down = false;
            if (tracking && recent.Count > 0) Evaluate();
            tracking = false;
        }
    }

    void Evaluate()
    {
        // Measured against the short screen side, so swipes feel the same in any orientation.
        float h = Mathf.Min(Screen.width, Screen.height);
        Vector2 total = (last.Position - start) / h;
        float length = total.magnitude;
        if (length < t.swipeMinLength || total.y <= 0f) return;

        Sample oldest = recent.Peek();
        float dt = last.Time - oldest.Time;
        float flickSpeed = dt > 1e-3f ? ((last.Position - oldest.Position) / h).magnitude / dt : 0f;

        float speed01 = Mathf.InverseLerp(t.swipeSpeedMin, t.swipeSpeedFull, flickSpeed);
        float length01 = Mathf.InverseLerp(t.swipeMinLength, t.swipeFullLength, length);
        float power = Mathf.Pow(speed01, t.swipePowerCurve) * Mathf.Lerp(0.5f, 1f, length01);

        Swiped?.Invoke(total / length, Mathf.Clamp01(power));
    }
}
