using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The only object in the scene. Builds the whole game in code at startup and wires the
// pieces together: swipe -> kick -> rock physics -> camera / haptics / HUD.
public class GameBootstrap : MonoBehaviour
{
    public RockTuning tuning;
    public Material rockMaterial;
    public Material roadMaterial;
    public Material groundMaterial;
    public Material skyboxMaterial;

    Rock rock;
    EndlessRoad road;
    FollowCamera followCamera;
    float lastImpactHapticTime;

    void Awake()
    {
        Application.targetFrameRate = 60; // iOS defaults to 30
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Time.fixedDeltaTime = 1f / 60f;
        Physics.bounceThreshold = 0.8f;
        if (tuning == null) tuning = ScriptableObject.CreateInstance<RockTuning>();

        CreateLighting();
        CreatePostProcessing();

        var rockSurface = new PhysicsMaterial("Rock")
        {
            dynamicFriction = tuning.rockFriction,
            staticFriction = tuning.rockStaticFriction,
            bounciness = tuning.rockBounciness,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Average,
        };
        var roadSurface = new PhysicsMaterial("Road")
        {
            dynamicFriction = tuning.roadFriction,
            staticFriction = tuning.roadFriction,
            bounciness = tuning.roadBounciness,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Average,
        };

        // Continue with the player's own rock, or find a new random one on first launch.
        var save = RockSave.Load();
        var erosion = save != null ? RockErosion.FromSave(save) : null;
        if (erosion == null) save = null;
        erosion ??= RockErosion.CreateNew(NewSeed(), tuning);

        rock = new GameObject("Rock").AddComponent<Rock>();
        rock.transform.position = new Vector3(0f, tuning.rockRadius + 0.05f, 0f);
        rock.transform.rotation = Quaternion.Euler(10f, 30f, 5f);
        rock.Init(tuning, OrFallback(rockMaterial, new Color(0.47f, 0.45f, 0.42f)), rockSurface, erosion);
        if (save != null) rock.RestoreStats(save.Kicks, save.Distance, save.Chips);
        rock.Impact += OnRockImpact;
        rock.Chipped += () => Haptics.Impact(Haptics.Style.Heavy, 1f);
        rock.Settled += SaveGame;

        road = new GameObject("Road").AddComponent<EndlessRoad>();
        road.Init(rock.transform, tuning,
            OrFallback(roadMaterial, new Color(0.3f, 0.3f, 0.3f)),
            OrFallback(groundMaterial, new Color(0.45f, 0.41f, 0.3f)),
            roadSurface);

        CreateCamera();

        var devMenu = gameObject.AddComponent<DevMenu>();
        devMenu.Init(rock, ResetRock);

        var swipe = gameObject.AddComponent<SwipeInput>();
        swipe.Init(tuning);
        swipe.Swiped += OnSwipe;
        swipe.Blocker = devMenu.Captures;

        gameObject.AddComponent<Hud>().Init(rock);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveGame();
    }

    void OnApplicationQuit() => SaveGame();

    void SaveGame()
    {
        if (rock == null) return;
        var e = rock.Erosion;
        RockSave.Save(new RockSaveData
        {
            Seed = e.Seed,
            Kicks = rock.Kicks,
            Distance = rock.Distance,
            Chips = rock.Chips,
            InitialRoughness = e.InitialRoughness,
            Directions = e.Directions,
            Heights = e.Heights,
        });
    }

    void ResetRock()
    {
        Vector3 p = rock.transform.position;
        rock.ResetTo(RockErosion.CreateNew(NewSeed(), tuning),
            new Vector3(road.transform.position.x, tuning.rockRadius + 0.1f, p.z));
        SaveGame();
    }

    static int NewSeed() => Random.Range(1, int.MaxValue);

    void OnSwipe(Vector2 screenDirection, float power)
    {
        if (!rock.CanKick) return;
        FloatingOrigin.RecenterIfNeeded(rock.transform.position, tuning.floatingOriginThreshold);

        float angle = Mathf.Atan2(screenDirection.x, screenDirection.y) * Mathf.Rad2Deg * tuning.steerSensitivity;
        angle = Mathf.Clamp(angle, -tuning.maxSteerAngle, tuning.maxSteerAngle);
        Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * followCamera.Heading;

        rock.Kick(direction, power);
        followCamera.Punch(power);
        Haptics.Impact(Haptics.Style.Rigid, Mathf.Lerp(0.45f, 1f, power));
    }

    void OnRockImpact(float speed)
    {
        if (speed < tuning.impactHapticMinSpeed || Time.time - lastImpactHapticTime < 0.06f) return;
        lastImpactHapticTime = Time.time;
        float strength = Mathf.InverseLerp(tuning.impactHapticMinSpeed, tuning.impactHapticFullSpeed, speed);
        Haptics.Impact(Haptics.Style.Soft, Mathf.Lerp(0.2f, 0.8f, strength));
    }

    void CreateLighting()
    {
        // Low sun behind-left of the camera: long shadow stretching ahead of the rock.
        var sunObject = new GameObject("Sun");
        sunObject.transform.rotation = Quaternion.Euler(22f, 35f, 0f);
        var sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.92f, 0.8f);
        sun.intensity = 1.6f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        RenderSettings.sun = sun;

        if (skyboxMaterial != null) RenderSettings.skybox = skyboxMaterial;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.56f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.20f);

        // Haze swallows the far end of the road, which sells "endless".
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.011f;
        RenderSettings.fogColor = new Color(0.74f, 0.78f, 0.82f);
    }

    void CreateCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        go.AddComponent<AudioListener>();
        followCamera = go.AddComponent<FollowCamera>();
        followCamera.Init(rock, tuning);
    }

    static void CreatePostProcessing()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.value = 0.25f;
        color.contrast.value = 8f;
        color.saturation.value = -8f;
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 0.25f;
        bloom.threshold.value = 1.1f;
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.22f;
        vignette.smoothness.value = 0.5f;

        var volume = new GameObject("Post Processing").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // Lets the scene run even if the setup menu was never used (e.g. bootstrap added by hand).
    static Material OrFallback(Material material, Color color)
    {
        if (material != null) return material;
        var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fallback.SetColor("_BaseColor", color);
        return fallback;
    }
}
