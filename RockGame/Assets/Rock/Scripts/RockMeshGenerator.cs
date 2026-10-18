using System.Collections.Generic;
using UnityEngine;

// A new rock is a sphere chipped by random planar cuts, then squashed. Every parameter comes
// from the seed, so each player's rock is different. The shape is only used to create the
// rock; from then on RockErosion owns its geometry.
public sealed class RockShape
{
    readonly Vector3[] cutNormals;
    readonly float[] cutDepths;
    readonly Vector3 squash;
    readonly float scale = 1f;

    public RockShape(int seed, RockTuning t)
    {
        var rng = new System.Random(seed);
        int cuts = rng.Next(t.rockCutsMin, t.rockCutsMax + 1);
        squash = new Vector3(
            Mathf.Lerp(t.rockSquashMin.x, t.rockSquashMax.x, (float)rng.NextDouble()),
            Mathf.Lerp(t.rockSquashMin.y, t.rockSquashMax.y, (float)rng.NextDouble()),
            Mathf.Lerp(t.rockSquashMin.z, t.rockSquashMax.z, (float)rng.NextDouble()));
        cutNormals = new Vector3[cuts];
        cutDepths = new float[cuts];
        for (int i = 0; i < cuts; i++)
        {
            cutNormals[i] = RandomDirection(rng);
            cutDepths[i] = Mathf.Lerp(t.rockCutDepthMin, t.rockCutDepthMax, (float)rng.NextDouble());
        }

        // Normalize so the furthest point sits at radius 1, whatever the seed.
        float max = 0f;
        const int samples = 512;
        for (int i = 0; i < samples; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / samples;
            float r = Mathf.Sqrt(1f - y * y);
            float a = i * 2.39996323f;
            max = Mathf.Max(max, Surface(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)).magnitude);
        }
        scale = 1f / max;
    }

    public Vector3 Surface(Vector3 direction)
    {
        float r = 1f;
        for (int i = 0; i < cutNormals.Length; i++)
        {
            float d = Vector3.Dot(direction, cutNormals[i]);
            if (d > 1e-4f) r = Mathf.Min(r, cutDepths[i] / d);
        }
        return Vector3.Scale(direction * r, squash) * scale;
    }

    static Vector3 RandomDirection(System.Random rng)
    {
        while (true)
        {
            var v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
            float m = v.sqrMagnitude;
            if (m > 0.01f && m <= 1f) return v / Mathf.Sqrt(m);
        }
    }
}

public static class RockMeshGenerator
{
    // Unit-sphere icosphere with outward-facing triangles. Subdivision only appends vertices,
    // so the first vertices of a fine level are exactly the vertices of every coarser level
    // (12 at level 0, 42 at level 1, 162 at level 2, ...).
    public static void Icosphere(int subdivisions, List<Vector3> verts, List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        verts.AddRange(new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        });
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        tris.AddRange(new[]
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        });

        for (int s = 0; s < subdivisions; s++)
        {
            var cache = new Dictionary<long, int>();
            var next = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Midpoint(a, b, verts, cache), bc = Midpoint(b, c, verts, cache), ca = Midpoint(c, a, verts, cache);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris.Clear();
            tris.AddRange(next);
        }

        // Make every triangle face outward (Unity: front face where Cross(b-a, c-a) points).
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
        }
    }

    static int Midpoint(int i, int j, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
        if (cache.TryGetValue(key, out int index)) return index;
        index = verts.Count;
        verts.Add(((verts[i] + verts[j]) * 0.5f).normalized);
        cache[key] = index;
        return index;
    }
}
