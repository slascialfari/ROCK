using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-click project setup so nothing has to be configured by hand:
//   ROCK > 1. Set Up Project       generates textures/materials, the Main scene, player settings, switches to iOS
//   ROCK > 2. Build iOS            exports the Xcode project to Builds/iOS and opens it
// Re-running setup is safe: it regenerates Assets/Rock/Generated and the scene, but keeps RockTuning.asset.
public static class RockProjectSetup
{
    const string Root = "Assets/Rock";
    const string Generated = Root + "/Generated";
    const string ScenePath = Root + "/Scenes/Main.unity";
    const string TuningPath = Root + "/RockTuning.asset";
    const string XcodePath = "Builds/iOS";

    [MenuItem("ROCK/1. Set Up Project", priority = 1)]
    public static void SetUp()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null || GraphicsSettings.defaultRenderPipeline == null)
        {
            EditorUtility.DisplayDialog("ROCK", "URP is not active. Create the project from the \"Universal 3D\" template.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(Root + "/Scenes");
        EnsureFolder(Generated);

        var tuning = AssetDatabase.LoadAssetAtPath<RockTuning>(TuningPath);
        if (tuning == null)
        {
            tuning = ScriptableObject.CreateInstance<RockTuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
        }

        EditorUtility.DisplayProgressBar("ROCK", "Generating textures…", 0.3f);
        Texture2D roadTexture, groundTexture;
        try
        {
            roadTexture = SaveTexture(RoadTexture(1024, 2048, tuning.roadWidth, tuning.segmentLength), Generated + "/T_Road.png");
            groundTexture = SaveTexture(GroundTexture(512, tuning.segmentLength / 4f), Generated + "/T_Ground.png");
        }
        finally { EditorUtility.ClearProgressBar(); }

        var rockMat = LitMaterial(Generated + "/M_Rock.mat", lit, null, new Color(0.47f, 0.45f, 0.42f), 0.18f);
        var roadMat = LitMaterial(Generated + "/M_Road.mat", lit, roadTexture, Color.white, 0.22f);
        var groundMat = LitMaterial(Generated + "/M_Ground.mat", lit, groundTexture, Color.white, 0.05f);
        var skyMat = SkyboxMaterial(Generated + "/M_Sky.mat");
        AssetDatabase.SaveAssets();

        ConfigureRenderPipeline();
        ConfigurePlayer();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var boot = new GameObject("ROCK").AddComponent<GameBootstrap>();
        boot.tuning = tuning;
        boot.rockMaterial = rockMat;
        boot.roadMaterial = roadMat;
        boot.groundMaterial = groundMat;
        boot.skyboxMaterial = skyMat;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
        {
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            else
                Debug.LogWarning("[ROCK] iOS Build Support is not installed. Add it in Unity Hub > Installs > (your editor) > Add modules.");
        }

        Debug.Log("[ROCK] Setup complete. Press Play and drag upward with the mouse to kick.");
    }

    [MenuItem("ROCK/2. Build iOS (Xcode project)", priority = 2)]
    public static void BuildIOS()
    {
        AdoptSigningTeamFromExistingXcodeProject();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = XcodePath,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[ROCK] iOS build failed: {report.summary.result}");
            return;
        }
        Debug.Log($"[ROCK] Xcode project written to {XcodePath}");
        if (!Application.isBatchMode)
            System.Diagnostics.Process.Start("open", $"\"{Path.GetFullPath(XcodePath)}/Unity-iPhone.xcodeproj\"");
    }

    // Simulates wear without physics to estimate how many kicks it takes to round a rock.
    // Assumes per kick: 4 impacts at 1.5–6 m/s and ~10 m of rolling, at random spots on the rock.
    // Real contacts favour protruding edges, so real rocks should round somewhat faster.
    [MenuItem("ROCK/Dev/Calibrate Erosion", priority = 100)]
    public static void CalibrateErosion()
    {
        var t = AssetDatabase.LoadAssetAtPath<RockTuning>(TuningPath);
        if (t == null) t = ScriptableObject.CreateInstance<RockTuning>();
        const int kicksPerStep = 2;  // wear is linear at these strengths, so batch kicks per step
        const int maxSteps = 25000;  // = 50,000 kicks
        int[] milestones = { 10, 25, 50, 75, 90, 95, 99 };
        var rng = new System.Random(123);
        Vector3 RandomDir()
        {
            while (true)
            {
                var v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                if (v.sqrMagnitude > 0.01f && v.sqrMagnitude <= 1f) return v.normalized;
            }
        }

        foreach (int seed in new[] { 11, 222, 3333 })
        {
            var e = RockErosion.CreateNew(seed, t);
            float startRadius = e.MeanRadius;
            var log = new System.Text.StringBuilder($"[ROCK] Calibration seed {seed}: ");
            int next = 0;
            for (int step = 1; step <= maxSteps && next < milestones.Length; step++)
            {
                for (int i = 0; i < 4; i++)
                {
                    float speed = Mathf.Lerp(1.5f, 6f, (float)rng.NextDouble());
                    e.Erode(RandomDir(), kicksPerStep * t.impactWear * Mathf.Pow(speed / 5f, 1.5f), t.erosionPatchAngle, t.erosionFill);
                }
                for (int i = 0; i < 5; i++)
                    e.Erode(RandomDir(), kicksPerStep * t.abrasionWear * 10f / 5f, t.erosionPatchAngle, t.erosionFill);

                if (step % 5 != 0) continue;
                e.Apply();
                while (next < milestones.Length && e.Roundness * 100f >= milestones[next])
                    log.Append($"{milestones[next++]}% @ {step * kicksPerStep:N0}  ");
            }
            if (next < milestones.Length) log.Append($"(stopped at {e.Roundness * 100f:0.0}% after {maxSteps * kicksPerStep:N0} kicks)  ");
            log.Append($"| radius {startRadius * 1000f:0.0} -> {e.MeanRadius * 1000f:0.0} mm");
            Debug.Log(log.ToString());
            e.Dispose();
        }
    }

    // Rebuilding replaces the Xcode project, which would drop the Team picked in Xcode.
    // Copy it into Player Settings once so every later build is signed automatically.
    static void AdoptSigningTeamFromExistingXcodeProject()
    {
        if (!string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID)) return;
        string pbxproj = XcodePath + "/Unity-iPhone.xcodeproj/project.pbxproj";
        if (!File.Exists(pbxproj)) return;
        var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(pbxproj), @"DEVELOPMENT_TEAM = ""?([A-Z0-9]{10})""?;");
        if (!match.Success) return;
        PlayerSettings.iOS.appleDeveloperTeamID = match.Groups[1].Value;
        AssetDatabase.SaveAssets();
        Debug.Log($"[ROCK] Using signing team {match.Groups[1].Value} from the existing Xcode project.");
    }

    static void ConfigureRenderPipeline()
    {
        // The rock is ~14 cm across; a shorter shadow distance gives its shadow far more resolution.
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = 30f;
                EditorUtility.SetDirty(urp);
            }
        }
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultUrp)
        {
            defaultUrp.shadowDistance = 30f;
            EditorUtility.SetDirty(defaultUrp);
        }
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.productName = "ROCK";
        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
        if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany") || id.Contains("Unity-Technologies"))
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.lascialfari.rock");

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.statusBarHidden = true;

        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.hideHomeButton = true;
        // Swipes start near the bottom edge; without this iOS steals them for the home gesture.
        PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
    }

    // ---------- Assets ----------

    static Material LitMaterial(string path, Shader shader, Texture2D texture, Color color, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material SkyboxMaterial(string path)
    {
        var shader = Shader.Find("Skybox/Procedural");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_SunSize", 0.035f);
        mat.SetFloat("_AtmosphereThickness", 0.9f);
        mat.SetColor("_SkyTint", new Color(0.5f, 0.55f, 0.6f));
        mat.SetColor("_GroundColor", new Color(0.62f, 0.62f, 0.6f));
        mat.SetFloat("_Exposure", 1.15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D SaveTexture(Texture2D texture, string path)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8; // essential: the road is seen at a grazing angle
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // One road segment: x across the road, y along it. Values are sRGB.
    static Texture2D RoadTexture(int width, int height, float roadWidth, float segmentLength)
    {
        var rng = new System.Random(1);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float mx = (x + 0.5f) / width * roadWidth;
            float my = (y + 0.5f) / height * segmentLength;
            float blotch = 0.55f * Tileable(mx, my, roadWidth, segmentLength, 0.35f)
                         + 0.30f * Tileable(mx, my, roadWidth, segmentLength, 1.3f)
                         + 0.15f * Tileable(mx, my, roadWidth, segmentLength, 5f);
            float grain = (float)rng.NextDouble();
            float v = 0.33f + (blotch - 0.5f) * 0.10f + (grain - 0.5f) * 0.10f;
            if (grain > 0.985f) v += 0.15f; // light aggregate
            if (grain < 0.01f) v -= 0.10f;  // pits

            float edge = Mathf.Min(mx, roadWidth - mx);
            bool edgeLine = edge > 0.25f && edge < 0.40f;
            bool centerDash = Mathf.Abs(mx - roadWidth * 0.5f) < 0.06f && my < 4.5f;
            if (edgeLine || centerDash)
            {
                float wear = Tileable(mx, my, roadWidth, segmentLength, 3f);
                v = Mathf.Lerp(v, 0.82f + (grain - 0.5f) * 0.08f, Mathf.SmoothStep(0.2f, 0.45f, wear) * 0.9f);
            }
            else if (edge < 0.25f)
            {
                v *= 0.85f; // crumbling edge
            }

            v = Mathf.Clamp01(v);
            pixels[y * width + x] = new Color(v, v * 0.99f, v * 0.97f);
        }
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Dry grass / dirt, tiling every `tile` metres.
    static Texture2D GroundTexture(int size, float tile)
    {
        var rng = new System.Random(2);
        var dirt = new Color(0.43f, 0.37f, 0.29f);
        var grass = new Color(0.50f, 0.47f, 0.30f);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float mx = (x + 0.5f) / size * tile;
            float my = (y + 0.5f) / size * tile;
            float n1 = Tileable(mx, my, tile, tile, 0.6f);
            float n2 = Tileable(mx, my, tile, tile, 2.5f);
            float n3 = Tileable(mx, my, tile, tile, 9f);
            float grassiness = Mathf.SmoothStep(0.35f, 0.65f, n1 * 0.7f + n2 * 0.3f);
            float shade = 0.85f + 0.3f * n3 + ((float)rng.NextDouble() - 0.5f) * 0.12f;
            Color c = Color.Lerp(dirt, grass, grassiness) * shade;
            c.a = 1f;
            pixels[y * size + x] = c;
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Perlin noise that wraps seamlessly with period (pw, ph).
    static float Tileable(float x, float y, float pw, float ph, float frequency)
    {
        float P(float a, float b) => Mathf.PerlinNoise(a * frequency + 100f, b * frequency + 100f);
        float u = x / pw, v = y / ph;
        float top = Mathf.Lerp(P(x, y), P(x - pw, y), u);
        float bottom = Mathf.Lerp(P(x, y - ph), P(x - pw, y - ph), u);
        return Mathf.Lerp(top, bottom, v);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
