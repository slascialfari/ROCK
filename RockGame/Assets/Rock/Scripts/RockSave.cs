using System;
using System.IO;
using UnityEngine;

public sealed class RockSaveData
{
    public int Seed;
    public int Kicks;
    public float Distance;
    public int Chips;
    public float InitialRoughness;
    public Vector3[] Directions;
    public float[] Heights;
}

// The player's rock on disk: a small binary file in the app's private storage.
// Written to a temp file first, so a crash mid-save can never corrupt the rock.
public static class RockSave
{
    const int Version = 1;

    static string FilePath => Path.Combine(Application.persistentDataPath, "rock.sav");

    public static RockSaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            using var r = new BinaryReader(File.OpenRead(FilePath));
            if (r.ReadInt32() != Version) return null;
            var d = new RockSaveData
            {
                Seed = r.ReadInt32(),
                Kicks = r.ReadInt32(),
                Distance = r.ReadSingle(),
                Chips = r.ReadInt32(),
                InitialRoughness = r.ReadSingle(),
            };
            int n = r.ReadInt32();
            d.Directions = new Vector3[n];
            d.Heights = new float[n];
            for (int i = 0; i < n; i++)
            {
                d.Directions[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                d.Heights[i] = r.ReadSingle();
            }
            return d;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ROCK] Could not load save: {e.Message}");
            return null;
        }
    }

    public static void Save(RockSaveData d)
    {
        try
        {
            string temp = FilePath + ".tmp";
            using (var w = new BinaryWriter(File.Create(temp)))
            {
                w.Write(Version);
                w.Write(d.Seed);
                w.Write(d.Kicks);
                w.Write(d.Distance);
                w.Write(d.Chips);
                w.Write(d.InitialRoughness);
                w.Write(d.Heights.Length);
                for (int i = 0; i < d.Heights.Length; i++)
                {
                    w.Write(d.Directions[i].x);
                    w.Write(d.Directions[i].y);
                    w.Write(d.Directions[i].z);
                    w.Write(d.Heights[i]);
                }
            }
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(temp, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ROCK] Could not save: {e.Message}");
        }
    }
}
