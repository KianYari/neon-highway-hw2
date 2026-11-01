using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds every asset, prefab and the scene from code: Neon > Build Everything.
public static class NeonBuilder
{
    const string ArtDir = "Assets/Art";
    const string AudioDir = "Assets/Audio";
    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string SceneDir = "Assets/Scenes";
    const string ScenePath = SceneDir + "/Game.unity";
    const string ProductName = "NeonHighway";

    static readonly Color Cyan = new Color(0f, 0.94f, 1f);
    static readonly Color Magenta = new Color(1f, 0.17f, 0.84f);

    static Sprite whiteSprite, heartSprite, coinSprite, fillSprite, borderSprite;
    static Texture2D glowDot, windowWall, windowGlow, skyTexture;
    static Font uiFont;

    // ---------------------------------------------------------------- entry points

    [MenuItem("Neon/Build Everything")]
    public static void BuildAll()
    {
        foreach (string dir in new[] { ArtDir, AudioDir, MatDir, PrefabDir, SceneDir })
            Directory.CreateDirectory(dir);

        PlayerSettings.productName = "Neon Highway";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        // A new scene unloads unused assets, so do it first, before any asset is referenced.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MakeTextures();
        AudioClip[] clips = MakeAudio();
        Materials m = MakeMaterials();
        Fx fx = MakeEffects(m);
        Prefabs p = MakePrefabs(m, fx);
        BuildScene(m, clips, fx, p);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Neon: everything built. Open " + ScenePath + " and press Play.");
    }

    [MenuItem("Neon/Build Executables")]
    public static void BuildPlayers()
    {
        if (!File.Exists(ScenePath)) BuildAll();

        Build(BuildTarget.StandaloneOSX, "Builds/macOS/" + ProductName + ".app");
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/" + ProductName + ".exe");
    }

    // For batch mode: -executeMethod NeonBuilder.BuildEverythingBatch
    public static void BuildEverythingBatch()
    {
        BuildAll();
        BuildPlayers();
    }

    public static void BuildAllAndMac()
    {
        BuildAll();
        BuildMac();
    }

    public static void BuildMac()
    {
        Build(BuildTarget.StandaloneOSX, "Builds/macOS/" + ProductName + ".app");
    }

    static void Build(BuildTarget target, string location)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(location));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = location,
            target = target,
            options = BuildOptions.None,
        });
        Debug.Log("Neon build " + target + ": " + report.summary.result + " -> " + location);
        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    // ---------------------------------------------------------------- textures and audio

    static void MakeTextures()
    {
        skyTexture = AssetFactory.NightSky(ArtDir + "/sky_night.png");
        AssetFactory.WindowTextures(ArtDir + "/windows_wall.png", ArtDir + "/windows_glow.png", out windowWall, out windowGlow);
        glowDot = AssetFactory.GlowDot(ArtDir + "/glow_dot.png");
        whiteSprite = AssetFactory.WhiteSprite(ArtDir + "/ui_white.png");
        heartSprite = AssetFactory.HeartSprite(ArtDir + "/ui_heart.png");
        coinSprite = AssetFactory.CoinSprite(ArtDir + "/ui_coin.png");
        fillSprite = AssetFactory.RoundedRect(ArtDir + "/ui_fill.png", false);
        borderSprite = AssetFactory.RoundedRect(ArtDir + "/ui_border.png", true);
    }

    static AudioClip[] MakeAudio()
    {
        return new[]
        {
            AssetFactory.SaveClip(AudioDir + "/music.wav", AssetFactory.Music()),
            AssetFactory.SaveClip(AudioDir + "/engine.wav", AssetFactory.Engine()),
            AssetFactory.SaveClip(AudioDir + "/crash.wav", AssetFactory.Crash()),
            AssetFactory.SaveClip(AudioDir + "/coin.wav", AssetFactory.Coin()),
            AssetFactory.SaveClip(AudioDir + "/fuel.wav", AssetFactory.Fuel()),
            AssetFactory.SaveClip(AudioDir + "/low_fuel.wav", AssetFactory.LowFuel()),
            AssetFactory.SaveClip(AudioDir + "/firework.wav", AssetFactory.Firework()),
            AssetFactory.SaveClip(AudioDir + "/game_over.wav", AssetFactory.GameOver()),
            AssetFactory.SaveClip(AudioDir + "/level_up.wav", AssetFactory.LevelUp()),
            AssetFactory.SaveClip(AudioDir + "/click.wav", AssetFactory.Click()),
        };
    }

    // ---------------------------------------------------------------- materials

    class Materials
    {
        public Material road, ground, barrier, pole, lampGlow, building, laneWhite, neonCyan, neonMagenta;
        public Material tire, glass, playerPaint, darkTrim;
        public Material headlight, tailLight, hazard, underglow;
        public Material coinGold, fuelRed, fuelCap, sky;
        public Material particleAdd, particleBright, particleAlpha;
    }

    static Material Lit(string name, Color color, float metallic, float smoothness, Color emission = default, float intensity = 0f)
    {
        string path = MatDir + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        // Emission needs the keyword so the per-object color changes at runtime work too.
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material ParticleMat(string name, string shader, float tint = 0.5f)
    {
        string path = MatDir + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find(shader);
        mat.mainTexture = glowDot;
        mat.SetColor("_TintColor", new Color(tint, tint, tint, 0.5f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Materials MakeMaterials()
    {
        Materials m = new Materials();
        m.road = Lit("Road", new Color(0.045f, 0.045f, 0.06f), 0f, 0.82f);
        m.ground = Lit("Ground", new Color(0.02f, 0.025f, 0.04f), 0f, 0.1f);
        m.barrier = Lit("Barrier", new Color(0.18f, 0.2f, 0.26f), 0.6f, 0.5f);
        m.pole = Lit("Pole", new Color(0.12f, 0.13f, 0.17f), 0.7f, 0.4f);
        m.lampGlow = Lit("LampGlow", new Color(1f, 0.8f, 0.5f), 0f, 0.5f, new Color(1f, 0.72f, 0.38f), 5f);
        m.laneWhite = Lit("LaneWhite", new Color(0.8f, 0.8f, 0.85f), 0f, 0.4f, new Color(0.8f, 0.85f, 1f), 0.9f);
        m.neonCyan = Lit("NeonCyan", Cyan, 0f, 0.5f, Cyan, 3.5f);
        m.neonMagenta = Lit("NeonMagenta", Magenta, 0f, 0.5f, Magenta, 3.5f);

        m.building = Lit("Building", Color.white, 0f, 0.25f, Color.white, 1.8f);
        m.building.SetTexture("_BaseMap", windowWall);
        m.building.SetTexture("_EmissionMap", windowGlow);
        m.building.SetColor("_EmissionColor", Color.white * 1.8f);

        m.tire = Lit("Tire", new Color(0.03f, 0.03f, 0.035f), 0f, 0.3f);
        m.glass = Lit("Glass", new Color(0.02f, 0.03f, 0.06f), 0.2f, 0.95f);
        m.playerPaint = Lit("PlayerPaint", new Color(0.04f, 0.12f, 0.55f), 0.6f, 0.85f);
        m.darkTrim = Lit("DarkTrim", new Color(0.05f, 0.05f, 0.07f), 0.5f, 0.5f);
        m.headlight = Lit("Headlight", Color.white, 0f, 0.9f, new Color(0.9f, 0.95f, 1f), 6f);
        m.tailLight = Lit("TailLight", new Color(0.6f, 0f, 0f), 0f, 0.9f, new Color(1f, 0.05f, 0.05f), 4f);
        m.hazard = Lit("Hazard", new Color(1f, 0.4f, 0f), 0f, 0.9f, Color.black, 0f);
        m.underglow = Lit("Underglow", Cyan, 0f, 0.5f, Cyan, 3f);

        m.coinGold = Lit("CoinGold", new Color(1f, 0.75f, 0.1f), 0.8f, 0.7f, new Color(1f, 0.7f, 0.1f), 1.6f);
        m.fuelRed = Lit("FuelRed", new Color(0.9f, 0.08f, 0.08f), 0.2f, 0.6f, new Color(1f, 0.1f, 0.05f), 1.4f);
        m.fuelCap = Lit("FuelCap", new Color(1f, 0.85f, 0.1f), 0.3f, 0.6f, new Color(1f, 0.8f, 0.1f), 1.2f);

        m.particleAdd = ParticleMat("ParticleAdditive", "Legacy Shaders/Particles/Additive");
        m.particleBright = ParticleMat("ParticleBright", "Legacy Shaders/Particles/Additive", 1.1f);
        m.particleAlpha = ParticleMat("ParticleAlpha", "Legacy Shaders/Particles/Alpha Blended");

        string skyPath = MatDir + "/NightSky.mat";
        m.sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (m.sky == null)
        {
            m.sky = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(m.sky, skyPath);
        }
        m.sky.SetTexture("_MainTex", skyTexture);
        m.sky.SetFloat("_Mapping", 1f);
        m.sky.SetFloat("_ImageType", 0f);
        m.sky.SetFloat("_Exposure", 1.1f);
        EditorUtility.SetDirty(m.sky);
        return m;
    }

    // ---------------------------------------------------------------- primitives

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool shadows = true)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool shadows = true)
        => Prim(PrimitiveType.Cube, parent, name, pos, scale, mat, shadows);

    static Light MakeLight(Transform parent, string name, LightType type, Vector3 pos, Color color, float intensity, float range, float spot = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (type == LightType.Spot) l.spotAngle = spot;
        return l;
    }

    static GameObject SavePrefab(GameObject go, string name)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------------------------------------------------------- particle effects

    class Fx
    {
        public PooledFx firework, impact, explosion, collect;
        public GameObject exhaustSmoke, exhaustFlame;
    }

    static ParticleSystem NewSystem(Transform parent, string name, Material mat, bool world)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.trailMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static Gradient FadeGradient(Color color, float startAlpha = 1f, float holdUntil = 0.08f)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(startAlpha, 0.04f), new GradientAlphaKey(startAlpha, holdUntil), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static void Burst(ParticleSystem ps, int count, float lifeMin, float lifeMax, float speedMin, float speedMax,
        float sizeMin, float sizeMax, float gravity, float radius)
    {
        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.gravityModifier = gravity;
        main.maxParticles = Mathf.Max(count * 2, 64);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeGradient(Color.white);
    }

    static void HoldBright(ParticleSystem ps)
    {
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.color = FadeGradient(Color.white, 1f, 0.55f);
    }

    static void ShrinkOverLife(ParticleSystem ps, float end)
    {
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, end));
    }

    static Fx MakeEffects(Materials m)
    {
        Fx fx = new Fx();

        // Sky firework: a big sphere of streaking sparks plus a slower glitter.
        {
            GameObject go = new GameObject("FireworkBurst");
            ParticleSystem sparks = NewSystem(go.transform, "Sparks", m.particleBright, true);
            Burst(sparks, 220, 1.8f, 2.8f, 14f, 30f, 4f, 6.5f, 0.3f, 0.5f);
            ShrinkOverLife(sparks, 0.25f);
            HoldBright(sparks);
            ParticleSystem.TrailModule trails = sparks.trails;
            trails.enabled = true;
            trails.ratio = 0.55f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.35f);
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.6f);
            trails.inheritParticleColor = true;
            trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, 0.8f));

            ParticleSystem glitter = NewSystem(go.transform, "Glitter", m.particleBright, true);
            Burst(glitter, 90, 2.2f, 3.4f, 3f, 12f, 2.5f, 4f, 0.12f, 0.5f);
            ShrinkOverLife(glitter, 0f);
            HoldBright(glitter);

            PooledFx pooled = go.AddComponent<PooledFx>();
            pooled.lifetime = 3.6f;
            pooled.scrollWithWorld = false;
            fx.firework = SavePrefab(go, "FireworkBurst").GetComponent<PooledFx>();
        }

        // Collision sparks and a puff of smoke.
        {
            GameObject go = new GameObject("ImpactFx");
            ParticleSystem sparks = NewSystem(go.transform, "Sparks", m.particleAdd, false);
            Burst(sparks, 60, 0.5f, 1.1f, 6f, 17f, 0.4f, 0.9f, 1.4f, 0.4f);
            ShrinkOverLife(sparks, 0.1f);

            ParticleSystem smoke = NewSystem(go.transform, "Smoke", m.particleAlpha, false);
            Burst(smoke, 14, 0.9f, 1.5f, 1f, 3.5f, 1.8f, 3.2f, -0.05f, 0.6f);
            ParticleSystem.ColorOverLifetimeModule smokeCol = smoke.colorOverLifetime;
            smokeCol.color = FadeGradient(new Color(0.25f, 0.22f, 0.3f), 0.55f);
            ParticleSystem.MainModule smokeMain = smoke.main;
            smokeMain.startColor = new Color(0.3f, 0.28f, 0.34f, 1f);

            PooledFx pooled = go.AddComponent<PooledFx>();
            pooled.lifetime = 1.7f;
            pooled.scrollWithWorld = true;
            fx.impact = SavePrefab(go, "ImpactFx").GetComponent<PooledFx>();
        }

        // Car explosion.
        {
            GameObject go = new GameObject("ExplosionFx");
            ParticleSystem fire = NewSystem(go.transform, "Fire", m.particleAdd, false);
            Burst(fire, 40, 0.5f, 1.1f, 2f, 9f, 3.5f, 7f, -0.1f, 0.8f);
            ShrinkOverLife(fire, 1.4f);

            ParticleSystem sparks = NewSystem(go.transform, "Sparks", m.particleAdd, false);
            Burst(sparks, 160, 0.8f, 1.8f, 8f, 26f, 0.5f, 1.2f, 1.2f, 0.5f);
            ShrinkOverLife(sparks, 0.1f);

            ParticleSystem smoke = NewSystem(go.transform, "Smoke", m.particleAlpha, false);
            Burst(smoke, 30, 1.6f, 2.6f, 1f, 5f, 3f, 6f, -0.08f, 0.8f);
            ParticleSystem.ColorOverLifetimeModule smokeCol = smoke.colorOverLifetime;
            smokeCol.color = FadeGradient(new Color(0.2f, 0.18f, 0.22f), 0.7f);
            ParticleSystem.MainModule smokeMain = smoke.main;
            smokeMain.startColor = new Color(0.22f, 0.2f, 0.25f, 1f);

            PooledFx pooled = go.AddComponent<PooledFx>();
            pooled.lifetime = 3f;
            pooled.scrollWithWorld = true;
            fx.explosion = SavePrefab(go, "ExplosionFx").GetComponent<PooledFx>();
        }

        // Pick-up sparkle (tinted by the item).
        {
            GameObject go = new GameObject("CollectFx");
            ParticleSystem ring = NewSystem(go.transform, "Sparkle", m.particleAdd, false);
            Burst(ring, 28, 0.45f, 0.8f, 3f, 8f, 0.5f, 1.0f, 0f, 0.3f);
            ShrinkOverLife(ring, 0f);

            ParticleSystem flash = NewSystem(go.transform, "Flash", m.particleAdd, false);
            Burst(flash, 1, 0.25f, 0.25f, 0f, 0f, 3.5f, 3.5f, 0f, 0.01f);
            ShrinkOverLife(flash, 0f);

            PooledFx pooled = go.AddComponent<PooledFx>();
            pooled.lifetime = 1f;
            pooled.scrollWithWorld = true;
            fx.collect = SavePrefab(go, "CollectFx").GetComponent<PooledFx>();
        }

        // Exhaust: world space particles shot backwards, so they stream away behind the car.
        {
            GameObject smoke = new GameObject("ExhaustSmoke");
            ParticleSystem ps = NewSystem(smoke.transform, "Smoke", m.particleAlpha, true);
            ConfigureExhaust(ps, 0.7f, 5f, 8f, 0.25f, 1.4f, 22f, new Color(0.45f, 0.4f, 0.6f, 1f), 0.22f);
            fx.exhaustSmoke = SavePrefab(smoke, "ExhaustSmoke");

            GameObject flame = new GameObject("ExhaustFlame");
            ParticleSystem fl = NewSystem(flame.transform, "Flame", m.particleAdd, true);
            ConfigureExhaust(fl, 0.14f, 6f, 10f, 0.35f, 0.1f, 45f, new Color(1f, 0.5f, 0.15f, 1f), 0.9f);
            fx.exhaustFlame = SavePrefab(flame, "ExhaustFlame");
        }

        return fx;
    }

    static void ConfigureExhaust(ParticleSystem ps, float life, float speedMin, float speedMax, float sizeStart, float sizeEnd,
        float rate, Color color, float alpha)
    {
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeStart, sizeStart * 1.4f);
        main.startColor = color;
        main.maxParticles = 150;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.04f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, Mathf.Max(0.1f, sizeEnd / sizeStart)));

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeGradient(Color.white, alpha);
    }

    // ---------------------------------------------------------------- prefabs

    class Prefabs
    {
        public RoadSegment road;
        public PlayerCar player;
        public StaticCar[] staticCars;
        public MovingCar[] movingCars;
        public Coin coin;
        public FuelCan fuel;
    }

    static Prefabs MakePrefabs(Materials m, Fx fx)
    {
        Prefabs p = new Prefabs();
        p.road = MakeRoadSegment(m);
        p.player = MakePlayer(m, fx);
        p.staticCars = new[] { MakeVan(m, fx), MakeTruck(m, fx) };
        p.movingCars = new[]
        {
            MakeSedan(m, fx, "Sedan", new Color(0.7f, 0.04f, 0.1f), Magenta),
            MakeHatch(m, fx, "Hatch", new Color(0.05f, 0.5f, 0.45f), Cyan),
        };
        p.coin = MakeCoin(m, fx);
        p.fuel = MakeFuelCan(m, fx);
        return p;
    }

    static RoadSegment MakeRoadSegment(Materials m)
    {
        float len = RoadSegment.Length;
        GameObject root = new GameObject("RoadSegment");

        Box(root.transform, "Asphalt", new Vector3(0f, -0.1f, len * 0.5f), new Vector3(11f, 0.2f, len), m.road, false);
        Box(root.transform, "GroundLeft", new Vector3(-45.5f, -0.35f, len * 0.5f), new Vector3(80f, 0.2f, len), m.ground, false);
        Box(root.transform, "GroundRight", new Vector3(45.5f, -0.35f, len * 0.5f), new Vector3(80f, 0.2f, len), m.ground, false);

        foreach (float side in new[] { -1f, 1f })
        {
            Box(root.transform, "EdgeLine", new Vector3(side * 4.95f, 0.012f, len * 0.5f), new Vector3(0.16f, 0.02f, len), m.laneWhite, false);
            Box(root.transform, "NeonStrip", new Vector3(side * 5.3f, 0.03f, len * 0.5f), new Vector3(0.14f, 0.06f, len),
                side < 0 ? m.neonCyan : m.neonMagenta, false);
            Box(root.transform, "Barrier", new Vector3(side * 5.9f, 0.35f, len * 0.5f), new Vector3(0.3f, 0.7f, len), m.barrier);
            Box(root.transform, "BarrierGlow", new Vector3(side * 5.74f, 0.55f, len * 0.5f), new Vector3(0.02f, 0.06f, len),
                side < 0 ? m.neonCyan : m.neonMagenta, false);

            foreach (float z in new[] { 3f, 15f })
                Box(root.transform, "LaneDash", new Vector3(side * 1.6f, 0.012f, z + 2.5f), new Vector3(0.16f, 0.02f, 5f), m.laneWhite, false);

            // One street light per side, staggered so there is one every 12 m.
            float lightZ = side < 0 ? 6f : 18f;
            Box(root.transform, "Pole", new Vector3(side * 6.6f, 3.5f, lightZ), new Vector3(0.2f, 7f, 0.2f), m.pole);
            Box(root.transform, "Arm", new Vector3(side * 5.5f, 7f, lightZ), new Vector3(2.4f, 0.12f, 0.12f), m.pole);
            Box(root.transform, "Lamp", new Vector3(side * 4.4f, 6.9f, lightZ), new Vector3(0.7f, 0.1f, 0.35f), m.lampGlow, false);
            MakeLight(root.transform, "LampLight", LightType.Point, new Vector3(side * 4.4f, 6.4f, lightZ),
                new Color(1f, 0.72f, 0.42f), 5.5f, 20f);
        }

        List<Transform> left = new List<Transform>();
        List<Transform> right = new List<Transform>();
        List<Renderer> renderers = new List<Renderer>();
        for (int i = 0; i < 3; i++)
        {
            GameObject b = Box(root.transform, "BuildingL" + i, Vector3.zero, Vector3.one, m.building);
            left.Add(b.transform);
            renderers.Add(b.GetComponent<Renderer>());
        }
        for (int i = 0; i < 3; i++)
        {
            GameObject b = Box(root.transform, "BuildingR" + i, Vector3.zero, Vector3.one, m.building);
            right.Add(b.transform);
            renderers.Add(b.GetComponent<Renderer>());
        }

        RoadSegment seg = root.AddComponent<RoadSegment>();
        seg.leftBuildings = left.ToArray();
        seg.rightBuildings = right.ToArray();
        seg.buildingRenderers = renderers.ToArray();

        return SavePrefab(root, "RoadSegment").GetComponent<RoadSegment>();
    }

    static void AddWheels(Transform parent, Material tire, Material rim, float x, float zFront, float zBack, float radius, List<Transform> list)
    {
        foreach (float sx in new[] { -1f, 1f })
        {
            foreach (float z in new[] { zFront, zBack })
            {
                GameObject w = Prim(PrimitiveType.Cylinder, parent, "Wheel", new Vector3(sx * x, radius, z),
                    new Vector3(radius * 2f, 0.13f, radius * 2f), tire);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Cylinder, w.transform, "Rim", new Vector3(0f, -sx * 1.05f, 0f), new Vector3(0.6f, 0.1f, 0.6f), rim, false);
                list?.Add(w.transform);
            }
        }
    }

    static GameObject NewVehicleRoot(string name, Vector3 colliderSize, Vector3 colliderCenter)
    {
        GameObject root = new GameObject(name);
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = colliderSize;
        col.center = colliderCenter;
        return root;
    }

    static PlayerCar MakePlayer(Materials m, Fx fx)
    {
        GameObject root = NewVehicleRoot("PlayerCar", new Vector3(1.9f, 1.3f, 4.2f), new Vector3(0f, 0.75f, 0f));
        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        Transform b = body.transform;

        GameObject chassis = Box(b, "Chassis", new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 0.5f, 4.3f), m.playerPaint);
        Box(b, "Nose", new Vector3(0f, 0.5f, 2.0f), new Vector3(1.8f, 0.3f, 0.8f), m.playerPaint);
        Box(b, "Cabin", new Vector3(0f, 1.05f, -0.3f), new Vector3(1.55f, 0.5f, 2.1f), m.glass);
        GameObject roof = Box(b, "Roof", new Vector3(0f, 1.32f, -0.3f), new Vector3(1.5f, 0.08f, 1.7f), m.playerPaint);
        GameObject spoiler = Box(b, "Spoiler", new Vector3(0f, 1.05f, -2.05f), new Vector3(1.8f, 0.08f, 0.5f), m.darkTrim);
        Box(b, "SpoilerPostL", new Vector3(-0.6f, 0.9f, -2.0f), new Vector3(0.08f, 0.35f, 0.12f), m.darkTrim);
        Box(b, "SpoilerPostR", new Vector3(0.6f, 0.9f, -2.0f), new Vector3(0.08f, 0.35f, 0.12f), m.darkTrim);
        Box(b, "Stripe", new Vector3(0f, 0.86f, 0.6f), new Vector3(0.3f, 0.01f, 2.8f), m.neonCyan, false);

        List<Transform> wheels = new List<Transform>();
        AddWheels(b, m.tire, m.neonMagenta, 0.98f, 1.4f, -1.4f, 0.38f, wheels);

        foreach (float sx in new[] { -1f, 1f })
        {
            Box(b, "Headlight", new Vector3(sx * 0.65f, 0.65f, 2.17f), new Vector3(0.4f, 0.14f, 0.08f), m.headlight, false);
            Box(b, "TailLight", new Vector3(sx * 0.62f, 0.72f, -2.17f), new Vector3(0.5f, 0.12f, 0.06f), m.tailLight, false);
            Light head = MakeLight(b, "HeadlightBeam", LightType.Spot, new Vector3(sx * 0.65f, 0.7f, 2.2f), new Color(0.85f, 0.92f, 1f), 14f, 55f, 55f);
            head.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
        }

        GameObject glow = Box(b, "Underglow", new Vector3(0f, 0.2f, 0f), new Vector3(1.7f, 0.04f, 3.8f), m.underglow, false);
        Light underLight = MakeLight(b, "UnderglowLight", LightType.Point, new Vector3(0f, 0.15f, 0f), Cyan, 3f, 6.5f);

        List<ParticleSystem> exhaust = new List<ParticleSystem>();
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cylinder, b, "ExhaustPipe", new Vector3(sx * 0.5f, 0.32f, -2.18f), new Vector3(0.14f, 0.1f, 0.14f), m.darkTrim, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            foreach (GameObject prefab in new[] { fx.exhaustSmoke, fx.exhaustFlame })
            {
                GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                e.transform.localPosition = new Vector3(sx * 0.5f, 0.32f, -2.3f);
                e.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                ParticleSystem ps = e.GetComponentInChildren<ParticleSystem>();
                exhaust.Add(ps);
            }
        }

        PlayerCar car = root.AddComponent<PlayerCar>();
        car.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/CarControls.inputactions");
        car.body = b;
        car.wheels = wheels.ToArray();
        car.bodyRenderers = new[] { chassis.GetComponent<Renderer>(), roof.GetComponent<Renderer>(), spoiler.GetComponent<Renderer>() };
        car.blinkRenderers = body.GetComponentsInChildren<Renderer>();
        car.underglowLight = underLight;
        car.underglowRenderer = glow.GetComponent<Renderer>();
        car.exhaust = exhaust.ToArray();
        car.explosionFx = fx.explosion;
        return SavePrefab(root, "PlayerCar").GetComponent<PlayerCar>();
    }

    static void AddTailLights(Transform b, Materials m, float z, float x, float y)
    {
        foreach (float sx in new[] { -1f, 1f })
            Box(b, "TailLight", new Vector3(sx * x, y, z), new Vector3(0.45f, 0.18f, 0.06f), m.tailLight, false);
    }

    static StaticCar MakeVan(Materials m, Fx fx)
    {
        Material paint = Lit("VanPaint", new Color(0.62f, 0.66f, 0.74f), 0.3f, 0.55f);
        GameObject root = NewVehicleRoot("Van", new Vector3(2f, 1.8f, 4.6f), new Vector3(0f, 1f, 0f));
        Transform b = root.transform;

        Box(b, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(2f, 1.5f, 4.6f), paint);
        Box(b, "Windshield", new Vector3(0f, 1.45f, 2.31f), new Vector3(1.8f, 0.5f, 0.05f), m.glass, false);
        Box(b, "Stripe", new Vector3(0f, 1.1f, 0f), new Vector3(2.02f, 0.18f, 4.62f), m.neonMagenta, false);
        AddWheels(b, m.tire, m.darkTrim, 0.95f, 1.5f, -1.5f, 0.4f, null);
        AddTailLights(b, m, -2.31f, 0.7f, 0.65f);

        List<Renderer> hazards = new List<Renderer>();
        foreach (float sx in new[] { -1f, 1f })
            foreach (float z in new[] { -2.32f, 2.32f })
                hazards.Add(Box(b, "Hazard", new Vector3(sx * 0.88f, 0.95f, z), new Vector3(0.22f, 0.22f, 0.06f), m.hazard, false).GetComponent<Renderer>());

        StaticCar car = root.AddComponent<StaticCar>();
        car.hazardLights = hazards.ToArray();
        car.impactFx = fx.impact;
        return SavePrefab(root, "Van").GetComponent<StaticCar>();
    }

    static StaticCar MakeTruck(Materials m, Fx fx)
    {
        Material cab = Lit("TruckCab", new Color(0.85f, 0.35f, 0.05f), 0.3f, 0.5f);
        Material cargo = Lit("TruckCargo", new Color(0.2f, 0.26f, 0.34f), 0.2f, 0.4f);
        GameObject root = NewVehicleRoot("Truck", new Vector3(2.15f, 2.4f, 5f), new Vector3(0f, 1.3f, -0.15f));
        Transform b = root.transform;

        Box(b, "Cab", new Vector3(0f, 1.0f, 1.45f), new Vector3(2f, 1.5f, 1.8f), cab);
        Box(b, "Windshield", new Vector3(0f, 1.45f, 2.36f), new Vector3(1.8f, 0.5f, 0.05f), m.glass, false);
        Box(b, "Cargo", new Vector3(0f, 1.4f, -1.0f), new Vector3(2.15f, 2.1f, 3.3f), cargo);
        Box(b, "CargoStripe", new Vector3(0f, 1.9f, -1.0f), new Vector3(2.17f, 0.2f, 3.32f), m.neonCyan, false);
        AddWheels(b, m.tire, m.darkTrim, 0.98f, 1.5f, -1.7f, 0.42f, null);
        AddTailLights(b, m, -2.66f, 0.8f, 0.6f);

        List<Renderer> hazards = new List<Renderer>();
        foreach (float sx in new[] { -1f, 1f })
        {
            hazards.Add(Box(b, "Hazard", new Vector3(sx * 0.9f, 0.9f, -2.66f), new Vector3(0.22f, 0.22f, 0.06f), m.hazard, false).GetComponent<Renderer>());
            hazards.Add(Box(b, "Hazard", new Vector3(sx * 0.9f, 0.7f, 2.38f), new Vector3(0.22f, 0.22f, 0.06f), m.hazard, false).GetComponent<Renderer>());
        }

        StaticCar car = root.AddComponent<StaticCar>();
        car.hazardLights = hazards.ToArray();
        car.impactFx = fx.impact;
        return SavePrefab(root, "Truck").GetComponent<StaticCar>();
    }

    static MovingCar MakeSedan(Materials m, Fx fx, string name, Color paintColor, Color neon)
    {
        Material paint = Lit(name + "Paint", paintColor, 0.6f, 0.8f);
        Material glow = Lit(name + "Glow", neon, 0f, 0.5f, neon, 3f);
        GameObject root = NewVehicleRoot(name, new Vector3(1.85f, 1.3f, 4.2f), new Vector3(0f, 0.75f, 0f));
        Transform b = root.transform;

        Box(b, "Chassis", new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.55f, 4.2f), paint);
        Box(b, "Cabin", new Vector3(0f, 1.08f, -0.2f), new Vector3(1.5f, 0.45f, 2.0f), m.glass);
        Box(b, "Roof", new Vector3(0f, 1.33f, -0.2f), new Vector3(1.45f, 0.07f, 1.6f), paint);
        Box(b, "Underglow", new Vector3(0f, 0.22f, 0f), new Vector3(1.6f, 0.04f, 3.6f), glow, false);
        AddWheels(b, m.tire, m.darkTrim, 0.93f, 1.35f, -1.35f, 0.37f, null);
        AddTailLights(b, m, -2.11f, 0.6f, 0.72f);
        foreach (float sx in new[] { -1f, 1f })
            Box(b, "Headlight", new Vector3(sx * 0.62f, 0.68f, 2.11f), new Vector3(0.4f, 0.12f, 0.06f), m.headlight, false);

        MovingCar car = root.AddComponent<MovingCar>();
        car.forwardSpeed = 10f;
        car.impactFx = fx.impact;
        return SavePrefab(root, name).GetComponent<MovingCar>();
    }

    static MovingCar MakeHatch(Materials m, Fx fx, string name, Color paintColor, Color neon)
    {
        Material paint = Lit(name + "Paint", paintColor, 0.5f, 0.75f);
        Material glow = Lit(name + "Glow", neon, 0f, 0.5f, neon, 3f);
        GameObject root = NewVehicleRoot(name, new Vector3(1.8f, 1.5f, 3.9f), new Vector3(0f, 0.85f, 0f));
        Transform b = root.transform;

        Box(b, "Chassis", new Vector3(0f, 0.62f, 0f), new Vector3(1.75f, 0.6f, 3.9f), paint);
        Box(b, "Cabin", new Vector3(0f, 1.17f, -0.35f), new Vector3(1.5f, 0.6f, 2.5f), m.glass);
        Box(b, "Roof", new Vector3(0f, 1.49f, -0.35f), new Vector3(1.45f, 0.07f, 2.3f), paint);
        Box(b, "Underglow", new Vector3(0f, 0.22f, 0f), new Vector3(1.55f, 0.04f, 3.3f), glow, false);
        AddWheels(b, m.tire, m.darkTrim, 0.9f, 1.25f, -1.25f, 0.36f, null);
        AddTailLights(b, m, -1.96f, 0.6f, 0.78f);
        foreach (float sx in new[] { -1f, 1f })
            Box(b, "Headlight", new Vector3(sx * 0.6f, 0.7f, 1.96f), new Vector3(0.38f, 0.12f, 0.06f), m.headlight, false);

        MovingCar car = root.AddComponent<MovingCar>();
        car.forwardSpeed = 10f;
        car.impactFx = fx.impact;
        return SavePrefab(root, name).GetComponent<MovingCar>();
    }

    static Coin MakeCoin(Materials m, Fx fx)
    {
        GameObject root = new GameObject("Coin");
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.4f, 2f, 1.2f);
        col.center = new Vector3(0f, 1f, 0f);

        GameObject visual = Prim(PrimitiveType.Cylinder, root.transform, "Visual", new Vector3(0f, 1f, 0f), new Vector3(0.9f, 0.06f, 0.9f), m.coinGold, false);
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Cylinder, visual.transform, "Ring", new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.6f, 0.7f), m.coinGold, false);

        Coin coin = root.AddComponent<Coin>();
        coin.visual = visual.transform;
        coin.collectFx = fx.collect;
        coin.fxColor = new Color(1f, 0.8f, 0.15f);
        coin.bobHeight = 0.12f;
        return SavePrefab(root, "Coin").GetComponent<Coin>();
    }

    static FuelCan MakeFuelCan(Materials m, Fx fx)
    {
        GameObject root = new GameObject("FuelCan");
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.6f, 2f, 1.4f);
        col.center = new Vector3(0f, 1f, 0f);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        Box(visual.transform, "Can", Vector3.zero, new Vector3(0.75f, 0.95f, 0.4f), m.fuelRed, false);
        Box(visual.transform, "Handle", new Vector3(-0.2f, 0.55f, 0f), new Vector3(0.28f, 0.14f, 0.2f), m.fuelCap, false);
        Box(visual.transform, "Cap", new Vector3(0.22f, 0.55f, 0f), new Vector3(0.18f, 0.16f, 0.18f), m.fuelCap, false);
        Box(visual.transform, "Stripe", Vector3.zero, new Vector3(0.78f, 0.18f, 0.42f), m.fuelCap, false);
        MakeLight(root.transform, "Glow", LightType.Point, new Vector3(0f, 1.2f, 0f), new Color(1f, 0.25f, 0.1f), 3f, 5f);

        FuelCan can = root.AddComponent<FuelCan>();
        can.visual = visual.transform;
        can.collectFx = fx.collect;
        can.fxColor = new Color(0.2f, 1f, 0.5f);
        can.bobHeight = 0.15f;
        can.spinSpeed = 100f;
        return SavePrefab(root, "FuelCan").GetComponent<FuelCan>();
    }

    // ---------------------------------------------------------------- scene

    static void BuildScene(Materials m, AudioClip[] clips, Fx fx, Prefabs p)
    {
        Scene scene = SceneManager.GetActiveScene();
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/CarControls.inputactions");

        // Lighting: night sky, moon, blue fog.
        RenderSettings.skybox = m.sky;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.14f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0075f;
        RenderSettings.fogColor = new Color(0.05f, 0.035f, 0.12f);

        GameObject moonGo = new GameObject("Moonlight");
        moonGo.transform.rotation = Quaternion.Euler(48f, 190f, 0f);
        Light moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.45f, 0.55f, 1f);
        moon.intensity = 0.55f;
        moon.shadows = LightShadows.Soft;
        RenderSettings.sun = moon;

        // Camera
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 450f;
        cam.fieldOfView = 62f;
        camGo.AddComponent<AudioListener>();
        UniversalAdditionalCameraData camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        CameraFollow follow = camGo.AddComponent<CameraFollow>();

        // Post processing: bloom is what makes the neon and the fireworks glow.
        CreateVolume();

        // Systems
        GameObject systems = new GameObject("Systems");
        systems.AddComponent<PoolManager>();
        RoadGenerator road = systems.AddComponent<RoadGenerator>();
        road.segmentPrefab = p.road;
        ObstacleSpawner spawner = systems.AddComponent<ObstacleSpawner>();
        spawner.staticCars = p.staticCars;
        spawner.movingCars = p.movingCars;
        spawner.coin = p.coin;
        spawner.fuelCan = p.fuel;
        FireworksManager fireworks = systems.AddComponent<FireworksManager>();
        fireworks.burstPrefab = fx.firework;

        AudioManager audio = systems.AddComponent<AudioManager>();
        audio.music = clips[0];
        audio.engine = clips[1];
        audio.crash = clips[2];
        audio.coin = clips[3];
        audio.fuel = clips[4];
        audio.lowFuel = clips[5];
        audio.firework = clips[6];
        audio.gameOver = clips[7];
        audio.levelUp = clips[8];
        audio.click = clips[9];

        // Player
        GameObject playerGo = (GameObject)PrefabUtility.InstantiatePrefab(p.player.gameObject);
        PlayerCar player = playerGo.GetComponent<PlayerCar>();
        player.cameraRig = follow;
        follow.target = player.transform;

        GameManager gm = systems.AddComponent<GameManager>();
        gm.inputActions = actions;
        gm.player = player;
        gm.road = road;
        gm.spawner = spawner;
        gm.fireworks = fireworks;

        BuildUi(actions);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void CreateVolume()
    {
        const string path = "Assets/Settings/NeonVolumeProfile.asset";
        AssetDatabase.DeleteAsset(path);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.6f);
        bloom.scatter.Override(0.72f);
        bloom.highQualityFiltering.Override(true);
        bloom.tint.Override(new Color(1f, 0.9f, 1f));

        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        ColorAdjustments adjust = profile.Add<ColorAdjustments>(true);
        adjust.contrast.Override(14f);
        adjust.saturation.Override(12f);
        adjust.postExposure.Override(0.15f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);

        ChromaticAberration ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(0.08f);

        EditorUtility.SetDirty(profile);

        GameObject go = new GameObject("Global Volume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        volume.sharedProfile = profile;
    }

    // ---------------------------------------------------------------- UI

    static Font UiFont()
    {
        if (uiFont != null) return uiFont;
        try { uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (uiFont == null)
        {
            try { uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return uiFont;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Text NewText(Transform parent, string name, string text, int size, Color color, TextAnchor align,
        Vector2 anchor, Vector2 pos, Vector2 box, FontStyle style = FontStyle.Bold, bool glow = true)
    {
        RectTransform rt = NewRect(name, parent);
        Anchor(rt, anchor, pos, box);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = UiFont();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (glow)
        {
            Outline o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(color.r, color.g, color.b, 0.35f);
            o.effectDistance = new Vector2(2f, -2f);
        }
        return t;
    }

    static Image NewImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewRect(name, parent);
        Anchor(rt, anchor, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Button NewButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color accent,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rt = NewRect(name, parent);
        Anchor(rt, anchor, pos, size);

        Image fill = rt.gameObject.AddComponent<Image>();
        fill.sprite = fillSprite;
        fill.type = Image.Type.Sliced;
        fill.color = new Color(0.03f, 0.03f, 0.1f, 0.88f);

        RectTransform borderRt = NewRect("Border", rt);
        Stretch(borderRt);
        Image border = borderRt.gameObject.AddComponent<Image>();
        border.sprite = borderSprite;
        border.type = Image.Type.Sliced;
        border.color = accent;
        border.raycastTarget = false;

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = border;
        ColorBlock colors = b.colors;
        colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        colors.fadeDuration = 0.08f;
        b.colors = colors;

        Text t = NewText(rt, "Label", label, 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero;
        t.rectTransform.offsetMax = Vector2.zero;

        UnityEventTools.AddPersistentListener(b.onClick, action);
        return b;
    }

    static void BuildUi(InputActionAsset actions)
    {
        GameObject canvasGo = new GameObject("UI", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        UIManager ui = canvasGo.AddComponent<UIManager>();
        ui.inputActions = actions;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
        InputSystemUIInputModule module = es.AddComponent<InputSystemUIInputModule>();
        ui.uiModule = module;

        Transform root = canvasGo.transform;
        Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f), tc = new Vector2(0.5f, 1f);
        Vector2 br = new Vector2(1f, 0f), bc = new Vector2(0.5f, 0f), c = new Vector2(0.5f, 0.5f);

        // ----- HUD
        RectTransform hud = NewRect("HUD", root);
        Stretch(hud);
        ui.hudPanel = hud.gameObject;

        Image flash = NewImage(hud, "DamageFlash", whiteSprite, new Color(1f, 0.05f, 0.05f, 0f), c, Vector2.zero, Vector2.zero);
        Stretch(flash.rectTransform);
        ui.damageFlash = flash;

        List<Image> hearts = new List<Image>();
        for (int i = 0; i < PlayerCar.MaxLives; i++)
        {
            Image h = NewImage(hud, "Life" + i, heartSprite, new Color(1f, 0.15f, 0.35f), tl, new Vector2(40f + i * 66f, -36f), new Vector2(56f, 56f));
            hearts.Add(h);
        }
        ui.lifeIcons = hearts.ToArray();

        NewText(hud, "FuelLabel", "FUEL", 28, Cyan, TextAnchor.MiddleLeft, tl, new Vector2(42f, -118f), new Vector2(120f, 36f));
        Image fuelBack = NewImage(hud, "FuelBack", fillSprite, new Color(0f, 0f, 0.05f, 0.7f), tl, new Vector2(40f, -156f), new Vector2(440f, 34f));
        fuelBack.type = Image.Type.Sliced;
        Image fuelFill = NewImage(fuelBack.transform, "Fill", whiteSprite, new Color(0.1f, 1f, 0.55f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(432f, 26f));
        fuelFill.type = Image.Type.Filled;
        fuelFill.fillMethod = Image.FillMethod.Horizontal;
        fuelFill.fillOrigin = 0;
        ui.fuelFill = fuelFill;
        Image fuelBorder = NewImage(fuelBack.transform, "Border", borderSprite, Cyan, c, Vector2.zero, Vector2.zero);
        fuelBorder.type = Image.Type.Sliced;
        Stretch(fuelBorder.rectTransform);

        ui.lowFuelText = NewText(hud, "LowFuel", "LOW FUEL", 56, new Color(1f, 0.2f, 0.25f), TextAnchor.MiddleCenter, c, new Vector2(0f, 120f), new Vector2(600f, 80f));

        NewText(hud, "ScoreLabel", "SCORE", 26, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter, tc, new Vector2(0f, -28f), new Vector2(300f, 34f));
        ui.scoreText = NewText(hud, "Score", "0", 84, Color.white, TextAnchor.MiddleCenter, tc, new Vector2(0f, -78f), new Vector2(600f, 100f));
        ui.highScoreText = NewText(hud, "HighScore", "BEST 0", 30, new Color(1f, 0.82f, 0.25f), TextAnchor.MiddleCenter, tc, new Vector2(0f, -176f), new Vector2(400f, 40f));

        NewImage(hud, "CoinIcon", coinSprite, new Color(1f, 0.8f, 0.15f), tr, new Vector2(-40f, -36f), new Vector2(52f, 52f));
        ui.coinsText = NewText(hud, "Coins", "x 0", 48, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight, tr, new Vector2(-100f, -36f), new Vector2(200f, 56f));
        ui.levelText = NewText(hud, "Level", "LEVEL 1", 40, Magenta, TextAnchor.MiddleRight, tr, new Vector2(-40f, -102f), new Vector2(300f, 50f));

        ui.speedText = NewText(hud, "Speed", "0", 120, Color.white, TextAnchor.MiddleRight, br, new Vector2(-150f, 100f), new Vector2(360f, 130f));
        NewText(hud, "SpeedUnit", "KM/H", 34, Cyan, TextAnchor.MiddleLeft, br, new Vector2(-30f, 78f), new Vector2(120f, 40f));

        ui.levelBanner = NewText(hud, "LevelBanner", "LEVEL 2", 110, Magenta, TextAnchor.MiddleCenter, c, new Vector2(0f, 215f), new Vector2(900f, 140f));

        // ----- main menu (right side, the car stays visible on the left)
        RectTransform menu = NewRect("MainMenu", root);
        Stretch(menu);
        ui.mainMenuPanel = menu.gameObject;
        Vector2 col = new Vector2(480f, 0f);
        Image menuDim = NewImage(menu, "Backdrop", fillSprite, new Color(0f, 0f, 0.05f, 0.6f), c, col + new Vector2(0f, -40f), new Vector2(960f, 960f));
        menuDim.type = Image.Type.Sliced;
        NewText(menu, "Title", "<color=#00F0FF>NEON</color>\n<color=#FF2BD6>HIGHWAY</color>", 150, Color.white, TextAnchor.MiddleCenter, c, col + new Vector2(0f, 230f), new Vector2(900f, 360f));
        NewText(menu, "Subtitle", "ENDLESS NIGHT DRIVE", 36, new Color(0.75f, 0.8f, 1f), TextAnchor.MiddleCenter, c, col + new Vector2(0f, 20f), new Vector2(700f, 50f), FontStyle.Normal);
        NewButton(menu, "StartButton", "START", c, col + new Vector2(0f, -100f), new Vector2(480f, 100f), Cyan, ui.OnStartClicked);
        NewButton(menu, "QuitButton", "QUIT", c, col + new Vector2(0f, -225f), new Vector2(480f, 100f), Magenta, ui.OnQuitClicked);
        ui.menuHighScoreText = NewText(menu, "MenuBest", "BEST 0", 40, new Color(1f, 0.82f, 0.25f), TextAnchor.MiddleCenter, c, col + new Vector2(0f, -330f), new Vector2(500f, 50f));
        NewText(menu, "Hint", "A / D  or  LEFT / RIGHT  to change lane      ESC  to pause", 28, new Color(0.7f, 0.75f, 0.9f), TextAnchor.MiddleCenter, bc, new Vector2(0f, 40f), new Vector2(1400f, 40f), FontStyle.Normal);

        // ----- pause
        RectTransform pause = NewRect("PauseMenu", root);
        Stretch(pause);
        ui.pausePanel = pause.gameObject;
        Image pauseDim = NewImage(pause, "Dim", whiteSprite, new Color(0f, 0f, 0.04f, 0.65f), c, Vector2.zero, Vector2.zero);
        Stretch(pauseDim.rectTransform);
        NewText(pause, "Title", "PAUSED", 120, Cyan, TextAnchor.MiddleCenter, c, new Vector2(0f, 230f), new Vector2(900f, 160f));
        NewButton(pause, "ResumeButton", "RESUME", c, new Vector2(0f, 60f), new Vector2(520f, 100f), Cyan, ui.OnResumeClicked);
        NewButton(pause, "RestartButton", "RESTART", c, new Vector2(0f, -65f), new Vector2(520f, 100f), Magenta, ui.OnRestartClicked);
        NewButton(pause, "MenuButton", "MAIN MENU", c, new Vector2(0f, -190f), new Vector2(520f, 100f), new Color(0.6f, 0.65f, 1f), ui.OnMenuClicked);

        // ----- game over
        RectTransform over = NewRect("GameOver", root);
        Stretch(over);
        ui.gameOverPanel = over.gameObject;
        Image overDim = NewImage(over, "Dim", whiteSprite, new Color(0.05f, 0f, 0.02f, 0.7f), c, Vector2.zero, Vector2.zero);
        Stretch(overDim.rectTransform);
        NewText(over, "Title", "GAME OVER", 130, new Color(1f, 0.2f, 0.3f), TextAnchor.MiddleCenter, c, new Vector2(0f, 300f), new Vector2(1200f, 170f));
        ui.gameOverReasonText = NewText(over, "Reason", "CAR DESTROYED", 38, new Color(0.8f, 0.82f, 1f), TextAnchor.MiddleCenter, c, new Vector2(0f, 210f), new Vector2(700f, 50f), FontStyle.Normal);
        NewText(over, "ScoreLabel", "SCORE", 30, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter, c, new Vector2(0f, 140f), new Vector2(300f, 40f));
        ui.finalScoreText = NewText(over, "FinalScore", "0", 130, Color.white, TextAnchor.MiddleCenter, c, new Vector2(0f, 60f), new Vector2(800f, 160f));
        ui.finalHighScoreText = NewText(over, "FinalBest", "BEST 0", 44, new Color(1f, 0.82f, 0.25f), TextAnchor.MiddleCenter, c, new Vector2(0f, -35f), new Vector2(600f, 60f));
        ui.newRecordText = NewText(over, "NewRecord", "NEW RECORD!", 54, new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter, c, new Vector2(0f, -100f), new Vector2(700f, 70f));
        NewButton(over, "RestartButton", "RESTART", c, new Vector2(-280f, -230f), new Vector2(500f, 100f), Cyan, ui.OnRestartClicked);
        NewButton(over, "MenuButton", "MAIN MENU", c, new Vector2(280f, -230f), new Vector2(500f, 100f), Magenta, ui.OnMenuClicked);
    }
}
