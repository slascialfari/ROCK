using System.Collections.Generic;
using UnityEngine;

// The rock's living geometry: a fixed direction per vertex plus a height (distance from the
// centre) that erosion changes. Wear is a local smoothing of heights around a contact point:
// bumps above their neighbours drop the most, so sharp edges go first, and the surface
// gradually converges to a sphere. Chips cut a small flat facet instead.
public sealed class RockErosion
{
    const int RenderSubdivisions = 4; // 2562 vertices, 5120 triangles
    const int HullSubdivisions = 1;   // first 42 vertices -> convex collider (PhysX hull limit is 255 faces)

    static List<int> renderTriangles, hullTriangles;
    static int renderVertexCount, hullVertexCount;
    static int[][] neighbors;

    public int Seed { get; }
    public Mesh Mesh { get; }
    public Mesh HullMesh { get; }
    public float InitialRoughness { get; }
    public float Roundness { get; private set; }  // 0 = as jagged as when found, 1 = perfect sphere
    public float MeanRadius { get; private set; }
    public float Volume { get; private set; }
    public Vector3[] Directions => dirs;
    public float[] Heights => heights;

    readonly Vector3[] dirs;
    readonly float[] heights;
    readonly Vector3[] vertices;
    readonly Vector3[] hullVertices;
    readonly List<int> patch = new List<int>();
    readonly List<float> patchWeights = new List<float>();
    float[] scratch = new float[256];
    bool dirty = true;

    public static RockErosion CreateNew(int seed, RockTuning t)
    {
        EnsureTopology();
        var shape = new RockShape(seed, t);
        var sphere = new List<Vector3>();
        RockMeshGenerator.Icosphere(RenderSubdivisions, sphere, new List<int>());

        var rng = new System.Random(seed ^ 0x5bd1e995);
        var dirs = new Vector3[sphere.Count];
        var heights = new float[sphere.Count];
        for (int i = 0; i < sphere.Count; i++)
        {
            float jitter = 1f + ((float)rng.NextDouble() * 2f - 1f) * t.rockSurfaceJitter;
            Vector3 p = shape.Surface(sphere[i]) * (t.rockRadius * jitter);
            dirs[i] = p.normalized;
            heights[i] = p.magnitude;
        }
        Stats(heights, out float mean, out float roughness);
        return new RockErosion(seed, dirs, heights, roughness);
    }

    public static RockErosion FromSave(RockSaveData data)
    {
        EnsureTopology();
        if (data.Directions == null || data.Heights == null ||
            data.Directions.Length != renderVertexCount || data.Heights.Length != renderVertexCount)
            return null;
        return new RockErosion(data.Seed, data.Directions, data.Heights, data.InitialRoughness);
    }

    RockErosion(int seed, Vector3[] dirs, float[] heights, float initialRoughness)
    {
        Seed = seed;
        this.dirs = dirs;
        this.heights = heights;
        InitialRoughness = Mathf.Max(initialRoughness, 1e-5f);
        vertices = new Vector3[dirs.Length];
        hullVertices = new Vector3[hullVertexCount];

        Mesh = new Mesh { name = "Rock" };
        Mesh.MarkDynamic();
        HullMesh = new Mesh { name = "Rock Hull" };
        UpdateVertices();
        Mesh.SetVertices(vertices);
        Mesh.SetTriangles(renderTriangles, 0);
        HullMesh.SetVertices(hullVertices);
        HullMesh.SetTriangles(hullTriangles, 0);
        dirty = true;
        Apply();
    }

    public void Dispose()
    {
        if (Application.isPlaying)
        {
            Object.Destroy(Mesh);
            Object.Destroy(HullMesh);
        }
        else
        {
            Object.DestroyImmediate(Mesh);
            Object.DestroyImmediate(HullMesh);
        }
    }

    // Wear the patch facing localDirection. amount ~ number of smoothing steps of strength 1.
    public void Erode(Vector3 localDirection, float amount, float patchAngle, float fill)
    {
        if (amount <= 0f) return;
        CollectPatch(localDirection, Mathf.Cos(patchAngle * Mathf.Deg2Rad));
        if (patch.Count == 0) return;
        if (scratch.Length < patch.Count) scratch = new float[patch.Count * 2];

        // Large amounts are split into several stable steps (a step above 0.5 would oscillate).
        int passes = Mathf.Clamp(Mathf.CeilToInt(amount / 0.5f), 1, 8);
        float step = Mathf.Min(amount / passes, 0.5f);
        for (int p = 0; p < passes; p++)
        {
            for (int k = 0; k < patch.Count; k++)
            {
                int i = patch[k];
                int[] n = neighbors[i];
                float sum = 0f;
                for (int j = 0; j < n.Length; j++) sum += heights[n[j]];
                float delta = sum / n.Length - heights[i];
                if (delta > 0f) delta *= fill;
                scratch[k] = heights[i] + delta * step * patchWeights[k];
            }
            for (int k = 0; k < patch.Count; k++) heights[patch[k]] = scratch[k];
        }
        dirty = true;
    }

    // Knock a flat chip off the side facing localDirection.
    public void Chip(Vector3 localDirection, float depthFraction)
    {
        int nearest = 0;
        float best = -2f;
        for (int i = 0; i < dirs.Length; i++)
        {
            float d = Vector3.Dot(dirs[i], localDirection);
            if (d > best) { best = d; nearest = i; }
        }
        float planeDistance = heights[nearest] * (1f - depthFraction);
        for (int i = 0; i < dirs.Length; i++)
        {
            float d = Vector3.Dot(dirs[i], localDirection);
            if (d > 0.3f) heights[i] = Mathf.Min(heights[i], planeDistance / d);
        }
        dirty = true;
    }

    // Pushes pending changes to the meshes. Returns true if anything changed.
    public bool Apply()
    {
        if (!dirty) return false;
        dirty = false;
        UpdateVertices();
        Mesh.SetVertices(vertices);
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
        HullMesh.SetVertices(hullVertices);
        HullMesh.RecalculateBounds();

        Stats(heights, out float mean, out float roughness);
        MeanRadius = mean;
        Roundness = Mathf.Clamp01(1f - roughness / InitialRoughness);

        float volume = 0f;
        for (int i = 0; i < renderTriangles.Count; i += 3)
            volume += Vector3.Dot(vertices[renderTriangles[i]], Vector3.Cross(vertices[renderTriangles[i + 1]], vertices[renderTriangles[i + 2]]));
        Volume = Mathf.Abs(volume) / 6f;
        return true;
    }

    void UpdateVertices()
    {
        for (int i = 0; i < dirs.Length; i++) vertices[i] = dirs[i] * heights[i];
        for (int i = 0; i < hullVertices.Length; i++) hullVertices[i] = vertices[i];
    }

    void CollectPatch(Vector3 direction, float cosRadius)
    {
        patch.Clear();
        patchWeights.Clear();
        for (int i = 0; i < dirs.Length; i++)
        {
            float d = Vector3.Dot(dirs[i], direction);
            if (d <= cosRadius) continue;
            float w = (d - cosRadius) / (1f - cosRadius);
            patch.Add(i);
            patchWeights.Add(w * w * (3f - 2f * w));
        }
    }

    // Roughness = how much the radius varies, relative to its average.
    static void Stats(float[] h, out float mean, out float roughness)
    {
        double sum = 0, sq = 0;
        for (int i = 0; i < h.Length; i++) { sum += h[i]; sq += (double)h[i] * h[i]; }
        mean = (float)(sum / h.Length);
        float variance = (float)(sq / h.Length - (double)mean * mean);
        roughness = Mathf.Sqrt(Mathf.Max(variance, 0f)) / Mathf.Max(mean, 1e-6f);
    }

    static void EnsureTopology()
    {
        if (neighbors != null) return;
        var verts = new List<Vector3>();
        renderTriangles = new List<int>();
        RockMeshGenerator.Icosphere(RenderSubdivisions, verts, renderTriangles);
        renderVertexCount = verts.Count;

        var hullVerts = new List<Vector3>();
        hullTriangles = new List<int>();
        RockMeshGenerator.Icosphere(HullSubdivisions, hullVerts, hullTriangles);
        hullVertexCount = hullVerts.Count;

        var sets = new HashSet<int>[renderVertexCount];
        for (int i = 0; i < sets.Length; i++) sets[i] = new HashSet<int>();
        for (int i = 0; i < renderTriangles.Count; i += 3)
        {
            int a = renderTriangles[i], b = renderTriangles[i + 1], c = renderTriangles[i + 2];
            sets[a].Add(b); sets[a].Add(c);
            sets[b].Add(a); sets[b].Add(c);
            sets[c].Add(a); sets[c].Add(b);
        }
        neighbors = new int[renderVertexCount][];
        for (int i = 0; i < sets.Length; i++) neighbors[i] = new List<int>(sets[i]).ToArray();
    }
}
