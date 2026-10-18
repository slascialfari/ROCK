using UnityEngine;

// A ring of identical road segments that leapfrog to stay around the rock, plus one large,
// seamless physics floor (separate box colliders per segment would cause bumps at the seams).
public class EndlessRoad : MonoBehaviour
{
    Transform target;
    RockTuning t;
    Transform[] segments;
    long[] segmentIndex;
    Transform floor;
    double shiftedZ; // total distance the world has been recentered along the road

    public void Init(Transform target, RockTuning tuning, Material roadMaterial, Material groundMaterial, PhysicsMaterial surface)
    {
        this.target = target;
        t = tuning;

        // Ground UVs tile every quarter segment so the texture is continuous across segments.
        float groundTile = t.segmentLength / 4f;
        Mesh roadMesh = Quad(t.roadWidth, t.segmentLength, Vector2.one, 0f);
        Mesh groundMesh = Quad(t.groundWidth, t.segmentLength, new Vector2(t.groundWidth / groundTile, 4f), -0.02f);

        int count = t.segmentsAhead + t.segmentsBehind + 1;
        segments = new Transform[count];
        segmentIndex = new long[count];
        for (int i = 0; i < count; i++)
        {
            var segment = new GameObject($"Segment {i}").transform;
            segment.SetParent(transform, false);
            AddSurface(segment, "Road", roadMesh, roadMaterial);
            AddSurface(segment, "Ground", groundMesh, groundMaterial);
            segments[i] = segment;
            segmentIndex[i] = long.MinValue;
        }

        floor = new GameObject("Physics Floor").transform;
        floor.SetParent(transform, false);
        var box = floor.gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(t.groundWidth, 1f, 1000f);
        box.center = new Vector3(0f, -0.5f, 0f);
        box.sharedMaterial = surface;

        FloatingOrigin.Shifted += OnWorldShifted;
        UpdateSegments(force: true);
    }

    void OnDestroy() => FloatingOrigin.Shifted -= OnWorldShifted;

    void LateUpdate() => UpdateSegments(force: false);

    void UpdateSegments(bool force)
    {
        if (target == null) return;
        float localZ = transform.InverseTransformPoint(target.position).z;
        long first = (long)System.Math.Floor((localZ + shiftedZ) / t.segmentLength) - t.segmentsBehind;
        int n = segments.Length;
        for (long i = first; i < first + n; i++)
        {
            int slot = (int)(((i % n) + n) % n);
            if (!force && segmentIndex[slot] == i) continue;
            segmentIndex[slot] = i;
            segments[slot].localPosition = new Vector3(0f, 0f, (float)(i * (double)t.segmentLength - shiftedZ));
        }

        if (force || Mathf.Abs(localZ - floor.localPosition.z) > 250f)
            floor.localPosition = new Vector3(0f, 0f, localZ);
    }

    // FloatingOrigin moved this root back along z; absorb that into shiftedZ instead, so
    // segment positions stay small numbers forever.
    void OnWorldShifted(Vector3 offset)
    {
        shiftedZ += offset.z;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, p.y, p.z + offset.z);
        UpdateSegments(force: true);
        Physics.SyncTransforms();
    }

    static void AddSurface(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
    }

    // Upward-facing quad from z = 0 to z = length, centered on x.
    static Mesh Quad(float width, float length, Vector2 uvScale, float y)
    {
        float hw = width * 0.5f;
        var mesh = new Mesh { name = "Quad" };
        mesh.vertices = new[] { new Vector3(-hw, y, 0f), new Vector3(hw, y, 0f), new Vector3(-hw, y, length), new Vector3(hw, y, length) };
        mesh.uv = new[] { Vector2.zero, new Vector2(uvScale.x, 0f), new Vector2(0f, uvScale.y), uvScale };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
