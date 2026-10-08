using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Authors the Last Stand project as real assets: weapon models, pickup and enemy prefabs (Assets/Resources), the Player prefab,
// and the Asźod yard itself as plain GameObjects saved into Assets/Scenes/LastStand.unity.
// Runs once automatically on first import; re-run from Tools > Last Stand > Build Scene (this REBUILDS the scene, discarding hand edits to it).
public static class LastStandBuilder
{
    const string ScenePath = "Assets/Scenes/LastStand.unity";
    const string Gen = "Assets/Generated";
    const int Version = 2;                       // bump to make Unity rebuild the scene automatically
    const string VersionFile = "Assets/Generated/builder_version.txt";
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    static Texture2D panelTex, floorTex;

    static readonly Color olive = new Color(0.24f, 0.3f, 0.2f), metal = new Color(0.17f, 0.18f, 0.2f), steel = new Color(0.55f, 0.57f, 0.6f),
                          black = new Color(0.05f, 0.05f, 0.06f), wood = new Color(0.36f, 0.22f, 0.12f), cyan = new Color(0.3f, 0.85f, 1f),
                          gold = new Color(0.9f, 0.7f, 0.2f), plasmaBlue = new Color(0.3f, 0.8f, 1f), dark = new Color(0.12f, 0.12f, 0.16f);

    // ---------- Entry points ----------

    [InitializeOnLoadMethod]
    static void AutoBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool("LastStandBuildTried" + Version, false) || !NeedsBuild()) return;
            SessionState.SetBool("LastStandBuildTried" + Version, true);   // never loop if something fails
            BuildAll();
        };
    }

    static bool NeedsBuild()
    {
        if (!File.Exists("Assets/Resources/Enemies/Grunt.prefab") || !File.Exists("Assets/Resources/Weapons/ar.prefab")) return true;
        if (!File.Exists(ScenePath)) return true;
        if (!File.Exists(VersionFile) || File.ReadAllText(VersionFile).Trim() != Version.ToString()) return true;
        return !File.ReadAllText(ScenePath).Contains("m_Name: Level\n");
    }

    [MenuItem("Tools/Last Stand/Build Scene")]
    public static void BuildAll()
    {
        try
        {
            foreach (var d in new[] { Gen + "/Materials", Gen + "/Textures", "Assets/Resources/Weapons", "Assets/Resources/Pickups", "Assets/Resources/Enemies", "Assets/Prefabs", "Assets/Scenes" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            matCache.Clear();

            panelTex = MakeTexture("Panel", new Color(0.8f, 0.8f, 0.85f), new Color(0.55f, 0.55f, 0.62f));
            floorTex = MakeTexture("Floor", new Color(0.8f, 0.8f, 0.78f), new Color(0.45f, 0.45f, 0.45f));

            foreach (var def in Weapons.All) BuildWeaponPrefab(def);
            BuildPickupPrefabs();
            BuildEnemyPrefabs();
            var player = BuildPlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene(player);
            File.WriteAllText(VersionFile, Version.ToString());
            Debug.Log("Last Stand: scene, prefabs and weapon models built. Press Play on Assets/Scenes/LastStand.unity.");
        }
        catch (System.Exception e) { Debug.LogError("Last Stand build failed: " + e); }
    }

    // ---------- Asset helpers ----------

    static Texture2D MakeTexture(string name, Color tile, Color line)
    {
        string path = Gen + "/Textures/" + name + ".png";
        if (!File.Exists(path))
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rng = new System.Random(5);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < 3 || y < 3;
                    float n = 0.94f + (float)rng.NextDouble() * 0.06f;
                    var c = (edge ? line : tile) * n; c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static string F(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }

    static Material Mat(Color c, float glow = 0f, Texture2D tex = null, Vector2 tile = default(Vector2), float gloss = 0.25f, string shader = "Standard")
    {
        tile = new Vector2(Mathf.Round(tile.x * 2f) / 2f, Mathf.Round(tile.y * 2f) / 2f);
        string key = shader.Replace('/', '_') + "_" + ColorUtility.ToHtmlStringRGBA(c) + "_g" + F(glow) + (tex ? "_" + tex.name + "_" + F(tile.x) + "x" + F(tile.y) : "");
        Material m;
        if (matCache.TryGetValue(key, out m) && m) return m;
        string path = Gen + "/Materials/" + key + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find(shader)) { color = c };
            if (shader == "Standard") m.SetFloat("_Glossiness", gloss);
            if (tex) { m.mainTexture = tex; m.mainTextureScale = tile; }
            if (glow > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
        }
        matCache[key] = m;
        return m;
    }

    static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool collider = false, Vector3 euler = default(Vector3))
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localEulerAngles = euler;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Shorthand with a plain color (and optional glow)
    static GameObject G(Transform parent, PrimitiveType t, string name, Vector3 pos, Vector3 scale, Color color, float glow = 0f, Vector3 euler = default(Vector3), bool collider = false)
    {
        return Prim(t, parent, name, pos, scale, Mat(color, glow), collider, euler);
    }

    static GameObject SavePrefab(GameObject root, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Light AddLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(parent, false); l.transform.localPosition = pos;
        l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity;
        return l;
    }

    // ---------- Weapon models (each weapon is visually distinct; same prefab is used in hand and on the ground) ----------

    static void BuildWeaponPrefab(WeaponDef def) { SavePrefab(WeaponModel(def.id), "Assets/Resources/Weapons/" + def.id + ".prefab"); }

    static GameObject WeaponModel(string id)
    {
        var root = new GameObject(id);
        var t = root.transform;
        var cyl = PrimitiveType.Cylinder; var cube = PrimitiveType.Cube;
        var along = new Vector3(90f, 0f, 0f);   // cylinders point along Z
        Vector3 mz;

        switch (id)
        {
            case "ar":
                G(t, cube, "Body", new Vector3(0, 0, 0.25f), new Vector3(0.08f, 0.12f, 0.5f), olive);
                G(t, cube, "Handguard", new Vector3(0, -0.005f, 0.58f), new Vector3(0.07f, 0.09f, 0.25f), metal);
                G(t, cyl, "Barrel", new Vector3(0, 0.01f, 0.82f), new Vector3(0.025f, 0.12f, 0.025f), black, 0f, along);
                G(t, cube, "CarryHandle", new Vector3(0, 0.1f, 0.2f), new Vector3(0.03f, 0.05f, 0.28f), metal);
                G(t, cube, "Mag", new Vector3(0, -0.13f, 0.3f), new Vector3(0.05f, 0.17f, 0.08f), metal, 0f, new Vector3(-12f, 0, 0));
                G(t, cube, "Grip", new Vector3(0, -0.1f, 0.08f), new Vector3(0.04f, 0.12f, 0.05f), black, 0f, new Vector3(15f, 0, 0));
                G(t, cube, "Stock", new Vector3(0, -0.01f, -0.12f), new Vector3(0.06f, 0.1f, 0.22f), olive);
                mz = new Vector3(0, 0.01f, 0.96f); break;
            case "dmr":
                G(t, cube, "Body", new Vector3(0, 0, 0.3f), new Vector3(0.06f, 0.1f, 0.62f), olive);
                G(t, cube, "Handguard", new Vector3(0, 0, 0.75f), new Vector3(0.05f, 0.08f, 0.3f), metal);
                G(t, cyl, "Barrel", new Vector3(0, 0.01f, 1.1f), new Vector3(0.02f, 0.18f, 0.02f), black, 0f, along);
                G(t, cyl, "Scope", new Vector3(0, 0.095f, 0.35f), new Vector3(0.04f, 0.13f, 0.04f), black, 0f, along);
                G(t, cyl, "Lens", new Vector3(0, 0.095f, 0.485f), new Vector3(0.032f, 0.005f, 0.032f), cyan, 2f, along);
                G(t, cube, "Mount", new Vector3(0, 0.06f, 0.35f), new Vector3(0.02f, 0.04f, 0.1f), metal);
                G(t, cube, "Mag", new Vector3(0, -0.1f, 0.28f), new Vector3(0.045f, 0.13f, 0.07f), metal);
                G(t, cube, "Grip", new Vector3(0, -0.09f, 0.08f), new Vector3(0.04f, 0.12f, 0.05f), black, 0f, new Vector3(15f, 0, 0));
                G(t, cube, "Stock", new Vector3(0, -0.03f, -0.14f), new Vector3(0.05f, 0.12f, 0.3f), olive);
                mz = new Vector3(0, 0.01f, 1.3f); break;
            case "pistol":
                G(t, cube, "Slide", new Vector3(0, 0.03f, 0.13f), new Vector3(0.045f, 0.065f, 0.3f), steel);
                G(t, cube, "Frame", new Vector3(0, -0.01f, 0.1f), new Vector3(0.04f, 0.04f, 0.22f), metal);
                G(t, cube, "Grip", new Vector3(0, -0.08f, 0.02f), new Vector3(0.04f, 0.14f, 0.065f), black, 0f, new Vector3(14f, 0, 0));
                G(t, cyl, "Barrel", new Vector3(0, 0.03f, 0.3f), new Vector3(0.013f, 0.03f, 0.013f), black, 0f, along);
                mz = new Vector3(0, 0.03f, 0.33f); break;
            case "shotgun":
                G(t, cube, "Receiver", new Vector3(0, 0, 0.12f), new Vector3(0.07f, 0.1f, 0.3f), steel);
                G(t, cyl, "Barrel", new Vector3(0, 0.025f, 0.6f), new Vector3(0.03f, 0.3f, 0.03f), black, 0f, along);
                G(t, cyl, "MagTube", new Vector3(0, -0.025f, 0.55f), new Vector3(0.026f, 0.25f, 0.026f), steel, 0f, along);
                G(t, cube, "Pump", new Vector3(0, -0.045f, 0.5f), new Vector3(0.065f, 0.06f, 0.2f), wood);
                G(t, cube, "Stock", new Vector3(0, -0.05f, -0.18f), new Vector3(0.06f, 0.12f, 0.38f), wood, 0f, new Vector3(-8f, 0, 0));
                mz = new Vector3(0, 0.025f, 0.92f); break;
            case "sniper":
                var bluegray = new Color(0.22f, 0.27f, 0.32f);
                G(t, cube, "Body", new Vector3(0, 0, 0.3f), new Vector3(0.065f, 0.09f, 0.8f), bluegray);
                G(t, cyl, "Barrel", new Vector3(0, 0.01f, 1.0f), new Vector3(0.025f, 0.3f, 0.025f), black, 0f, along);
                G(t, cube, "Brake", new Vector3(0, 0.01f, 1.31f), new Vector3(0.05f, 0.05f, 0.07f), metal);
                G(t, cyl, "Scope", new Vector3(0, 0.1f, 0.35f), new Vector3(0.05f, 0.2f, 0.05f), black, 0f, along);
                G(t, cyl, "Lens", new Vector3(0, 0.1f, 0.555f), new Vector3(0.04f, 0.005f, 0.04f), cyan, 2f, along);
                G(t, cube, "Mount", new Vector3(0, 0.06f, 0.35f), new Vector3(0.02f, 0.04f, 0.15f), metal);
                G(t, cube, "Mag", new Vector3(0, -0.09f, 0.25f), new Vector3(0.045f, 0.1f, 0.08f), metal);
                G(t, cube, "Stock", new Vector3(0, -0.04f, -0.28f), new Vector3(0.06f, 0.14f, 0.4f), bluegray);
                mz = new Vector3(0, 0.01f, 1.35f); break;
            case "sword":
                G(t, cyl, "Hilt", new Vector3(0, 0, 0.1f), new Vector3(0.025f, 0.1f, 0.025f), black, 0f, along);
                G(t, cube, "Guard", new Vector3(0, 0, 0.22f), new Vector3(0.2f, 0.04f, 0.05f), steel);
                G(t, cube, "ProngL", new Vector3(-0.1f, 0, 0.3f), new Vector3(0.02f, 0.03f, 0.16f), cyan, 3f);
                G(t, cube, "ProngR", new Vector3(0.1f, 0, 0.3f), new Vector3(0.02f, 0.03f, 0.16f), cyan, 3f);
                G(t, cube, "Blade", new Vector3(0, 0, 0.75f), new Vector3(0.012f, 0.1f, 0.9f), cyan, 3.5f);
                G(t, cube, "Core", new Vector3(0, 0, 0.75f), new Vector3(0.014f, 0.04f, 0.88f), Color.white, 4f);
                AddLight(t, new Vector3(0, 0, 0.7f), cyan, 5f, 1f);
                mz = new Vector3(0, 0, 1.2f); break;
            case "concussion":
                var purple = new Color(0.35f, 0.25f, 0.55f);
                G(t, cube, "Body", new Vector3(0, 0, 0.3f), new Vector3(0.13f, 0.15f, 0.55f), purple);
                G(t, cube, "Fin", new Vector3(0, 0.11f, 0.25f), new Vector3(0.04f, 0.06f, 0.4f), purple * 0.7f);
                G(t, cyl, "Barrel", new Vector3(0, 0, 0.72f), new Vector3(0.075f, 0.12f, 0.075f), black, 0f, along);
                G(t, cyl, "Ring1", new Vector3(0, 0, 0.62f), new Vector3(0.1f, 0.012f, 0.1f), cyan, 3f, along);
                G(t, cyl, "Ring2", new Vector3(0, 0, 0.78f), new Vector3(0.1f, 0.012f, 0.1f), cyan, 3f, along);
                G(t, cube, "Grip", new Vector3(0, -0.13f, 0.15f), new Vector3(0.05f, 0.14f, 0.06f), black, 0f, new Vector3(12f, 0, 0));
                G(t, cube, "Rear", new Vector3(0, -0.02f, -0.06f), new Vector3(0.1f, 0.12f, 0.15f), purple);
                mz = new Vector3(0, 0, 0.9f); break;
            default: // turret
                G(t, cube, "Body", new Vector3(0, 0.05f, 0.25f), new Vector3(0.18f, 0.2f, 0.55f), metal);
                G(t, cyl, "Shroud", new Vector3(0, 0.06f, 0.65f), new Vector3(0.07f, 0.2f, 0.07f), steel, 0f, along);
                G(t, cyl, "Barrel", new Vector3(0, 0.06f, 1.0f), new Vector3(0.03f, 0.2f, 0.03f), black, 0f, along);
                G(t, cube, "AmmoBox", new Vector3(0.17f, -0.03f, 0.2f), new Vector3(0.13f, 0.14f, 0.22f), olive);
                G(t, cube, "Handle", new Vector3(0, 0.2f, 0.25f), new Vector3(0.04f, 0.05f, 0.3f), black);
                G(t, cyl, "LegL", new Vector3(-0.12f, -0.2f, 0.1f), new Vector3(0.025f, 0.2f, 0.025f), steel, 0f, new Vector3(0, 0, -25f));
                G(t, cyl, "LegR", new Vector3(0.12f, -0.2f, 0.1f), new Vector3(0.025f, 0.2f, 0.025f), steel, 0f, new Vector3(0, 0, 25f));
                G(t, cyl, "LegBack", new Vector3(0, -0.2f, -0.05f), new Vector3(0.025f, 0.2f, 0.025f), steel, 0f, new Vector3(-25f, 0, 0));
                mz = new Vector3(0, 0.06f, 1.2f); break;
        }
        var m = new GameObject("Muzzle"); m.transform.SetParent(t, false); m.transform.localPosition = mz;
        return root;
    }

    // ---------- Pickups ----------

    static void BuildPickupPrefabs()
    {
        foreach (var def in Weapons.All)
        {
            var root = new GameObject("Pickup_" + def.id);
            var pk = root.AddComponent<Pickup>(); pk.kind = Pickup.Kind.Weapon; pk.weaponId = def.id;
            var model = WeaponModel(def.id);
            model.name = "Model"; model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * 1.15f; model.transform.localPosition = new Vector3(0, 0, -0.45f);
            G(root.transform, PrimitiveType.Cylinder, "Marker", new Vector3(0, -0.35f, 0), new Vector3(0.55f, 0.01f, 0.55f), cyan, 2f);
            AddLight(root.transform, new Vector3(0, 0.3f, 0), new Color(1f, 0.9f, 0.6f), 5f, 1.1f);
            SavePrefab(root, "Assets/Resources/Pickups/" + def.id + ".prefab");
        }

        // Health pack: white box with a red cross
        var hp = new GameObject("Pickup_Health"); var hpk = hp.AddComponent<Pickup>(); hpk.kind = Pickup.Kind.Health;
        G(hp.transform, PrimitiveType.Cube, "Box", Vector3.zero, new Vector3(0.6f, 0.45f, 0.6f), Color.white);
        var red = new Color(1f, 0.15f, 0.15f);
        G(hp.transform, PrimitiveType.Cube, "CrossA", Vector3.zero, new Vector3(0.36f, 0.47f, 0.14f), red, 2f);
        G(hp.transform, PrimitiveType.Cube, "CrossB", Vector3.zero, new Vector3(0.14f, 0.47f, 0.36f), red, 2f);
        AddLight(hp.transform, new Vector3(0, 0.4f, 0), new Color(1f, 0.3f, 0.3f), 4f, 1.2f);
        SavePrefab(hp, "Assets/Resources/Pickups/Health.prefab");

        var ds = new GameObject("Pickup_DropShield"); var dpk = ds.AddComponent<Pickup>(); dpk.kind = Pickup.Kind.DropShield;
        G(ds.transform, PrimitiveType.Cylinder, "Disc", Vector3.zero, new Vector3(0.45f, 0.1f, 0.45f), cyan, 2f);
        G(ds.transform, PrimitiveType.Sphere, "Dome", new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.2f, 0.3f), Color.white, 1.5f);
        AddLight(ds.transform, new Vector3(0, 0.4f, 0), cyan, 4f, 1.2f);
        SavePrefab(ds, "Assets/Resources/Pickups/DropShield.prefab");
    }

    // ---------- Enemies ----------

    static void BuildEnemyPrefabs()
    {
        BuildEnemy(Enemy.Kind.Grunt, 0.4f, 1.8f, 1f, BuildGrunt);
        BuildEnemy(Enemy.Kind.Elite, 0.5f, 2.4f, 1f, t => BuildElite(t, new Color(0.2f, 0.35f, 0.8f), false, false, false));
        BuildEnemy(Enemy.Kind.Ranger, 0.5f, 2.4f, 1f, t => BuildElite(t, new Color(0.2f, 0.55f, 0.3f), false, true, false));
        BuildEnemy(Enemy.Kind.General, 0.5f, 2.4f, 1.1f, t => BuildElite(t, new Color(0.7f, 0.55f, 0.15f), true, false, false));
        BuildEnemy(Enemy.Kind.Zealot, 0.5f, 2.4f, 1.1f, t => BuildElite(t, new Color(0.9f, 0.9f, 0.8f), true, false, true));
        BuildEnemy(Enemy.Kind.Wraith, 2.3f, 3f, 1f, BuildWraith);
        BuildEnemy(Enemy.Kind.Banshee, 0f, 0f, 1f, BuildBanshee);
    }

    // Ground enemies get a CharacterController so walls, containers and stairs actually stop them
    static void BuildEnemy(Enemy.Kind kind, float radius, float height, float scale, System.Action<Transform> build)
    {
        var root = new GameObject(kind.ToString());
        var e = root.AddComponent<Enemy>(); e.kind = kind;
        if (height > 0f)
        {
            var cc = root.AddComponent<CharacterController>();
            cc.radius = radius; cc.height = height; cc.center = new Vector3(0, height / 2f, 0); cc.stepOffset = 0.6f; cc.slopeLimit = 50f;
        }
        build(root.transform);
        root.transform.localScale = Vector3.one * scale;
        SavePrefab(root, "Assets/Resources/Enemies/" + kind + ".prefab");
    }

    // Unggoy: short, with a methane tank and glowing eyes
    static void BuildGrunt(Transform t)
    {
        G(t, PrimitiveType.Capsule, "Body", new Vector3(0, 0.9f, 0), Vector3.one * 0.9f, new Color(0.45f, 0.3f, 0.7f));
        G(t, PrimitiveType.Sphere, "Head", new Vector3(0, 1.9f, 0.1f), Vector3.one * 0.55f, new Color(0.9f, 0.5f, 0.2f));
        G(t, PrimitiveType.Cylinder, "Tank", new Vector3(0, 1.3f, -0.45f), new Vector3(0.28f, 0.4f, 0.28f), new Color(0.2f, 0.5f, 0.5f));
        G(t, PrimitiveType.Sphere, "EyeL", new Vector3(-0.13f, 1.95f, 0.33f), Vector3.one * 0.1f, new Color(1f, 0.8f, 0.2f), 3f);
        G(t, PrimitiveType.Sphere, "EyeR", new Vector3(0.13f, 1.95f, 0.33f), Vector3.one * 0.1f, new Color(1f, 0.8f, 0.2f), 3f);
        G(t, PrimitiveType.Cube, "Weapon", new Vector3(0.4f, 1.0f, 0.4f), new Vector3(0.1f, 0.12f, 0.45f), dark);
        G(t, PrimitiveType.Sphere, "WeaponGlow", new Vector3(0.4f, 1.0f, 0.65f), Vector3.one * 0.12f, new Color(0.3f, 1f, 0.4f), 3f);
    }

    // Sangheili: tall, shielded warriors; extras identify the variant
    static void BuildElite(Transform t, Color body, bool crest, bool jetpack, bool sword)
    {
        G(t, PrimitiveType.Capsule, "Body", new Vector3(0, 1.15f, 0), new Vector3(0.95f, 1.15f, 0.95f), body);
        G(t, PrimitiveType.Sphere, "Head", new Vector3(0, 2.45f, 0.1f), new Vector3(0.5f, 0.55f, 0.7f), dark);
        G(t, PrimitiveType.Sphere, "Visor", new Vector3(0, 2.4f, 0.4f), new Vector3(0.3f, 0.1f, 0.1f), plasmaBlue, 3f);
        G(t, PrimitiveType.Cube, "ShoulderL", new Vector3(-0.55f, 1.95f, 0f), new Vector3(0.35f, 0.25f, 0.4f), body * 0.8f);
        G(t, PrimitiveType.Cube, "ShoulderR", new Vector3(0.55f, 1.95f, 0f), new Vector3(0.35f, 0.25f, 0.4f), body * 0.8f);
        if (!sword)
        {
            G(t, PrimitiveType.Cube, "Rifle", new Vector3(0.45f, 1.3f, 0.4f), new Vector3(0.1f, 0.12f, 0.6f), dark);
            G(t, PrimitiveType.Sphere, "RifleGlow", new Vector3(0.45f, 1.3f, 0.75f), Vector3.one * 0.1f, plasmaBlue, 3f);
        }
        else G(t, PrimitiveType.Cube, "EnergySword", new Vector3(0.55f, 1.4f, 0.95f), new Vector3(0.1f, 0.12f, 1.4f), plasmaBlue, 3.5f);
        if (crest) G(t, PrimitiveType.Cube, "Crest", new Vector3(0, 2.85f, 0f), new Vector3(0.12f, 0.3f, 0.55f), gold, 1.5f);
        if (jetpack)
        {
            G(t, PrimitiveType.Cube, "Jetpack", new Vector3(0, 1.7f, -0.55f), new Vector3(0.55f, 0.7f, 0.3f), dark);
            G(t, PrimitiveType.Sphere, "Thruster", new Vector3(0, 1.3f, -0.6f), Vector3.one * 0.25f, new Color(1f, 0.6f, 0.2f), 3f);
        }
    }

    static void BuildWraith(Transform t)
    {
        var hull = new Color(0.3f, 0.25f, 0.4f);
        G(t, PrimitiveType.Sphere, "Body", new Vector3(0, 1.4f, 0), new Vector3(4.5f, 1.8f, 6.5f), hull);
        G(t, PrimitiveType.Sphere, "Turret", new Vector3(0, 2.7f, -0.5f), new Vector3(1.8f, 1.2f, 1.8f), hull * 1.1f);
        G(t, PrimitiveType.Cube, "Cannon", new Vector3(0, 3.1f, 1.2f), new Vector3(0.4f, 0.4f, 2.4f), dark, 0f, new Vector3(-8f, 0, 0));
        G(t, PrimitiveType.Sphere, "Underglow", new Vector3(0, 0.5f, 0), new Vector3(3.5f, 0.5f, 5f), new Color(0.6f, 0.4f, 1f), 3f);
    }

    static void BuildBanshee(Transform t)
    {
        var hull = new Color(0.35f, 0.3f, 0.5f);
        G(t, PrimitiveType.Capsule, "Body", Vector3.zero, new Vector3(0.7f, 1.6f, 0.7f), hull, 0f, new Vector3(90f, 0, 0), true);
        foreach (float sx in new[] { -1f, 1f })
        {
            G(t, PrimitiveType.Cube, "Wing", new Vector3(sx * 1.6f, 0f, -0.4f), new Vector3(2.8f, 0.1f, 1.2f), hull * 0.9f, 0f, new Vector3(0, sx * -15f, sx * 10f), true);
            G(t, PrimitiveType.Sphere, "Engine", new Vector3(sx * 0.5f, 0f, -1.5f), Vector3.one * 0.35f, new Color(0.8f, 0.4f, 1f), 3f);
        }
    }

    // ---------- Player ----------

    static GameObject BuildPlayerPrefab()
    {
        var root = new GameObject("Player");
        var pl = root.AddComponent<Player>();                       // adds the CharacterController via [RequireComponent]
        var cc = root.GetComponent<CharacterController>();
        cc.height = 1.9f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.95f, 0); cc.stepOffset = 0.6f;
        var cam = new GameObject("Camera"); cam.tag = "MainCamera";
        cam.transform.SetParent(root.transform, false); cam.transform.localPosition = new Vector3(0, 1.7f, 0);
        var c = cam.AddComponent<Camera>(); c.fieldOfView = 80f; c.nearClipPlane = 0.05f; c.farClipPlane = 400f;
        cam.AddComponent<AudioListener>();
        var gun = new GameObject("Gun");                           // weapon models are parented here at runtime
        gun.transform.SetParent(cam.transform, false); gun.transform.localPosition = new Vector3(0.3f, -0.28f, 0.55f);
        return SavePrefab(root, "Assets/Prefabs/Player.prefab");
    }

    // ---------- The scene ----------

    static void BuildScene(GameObject playerPrefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var r in scene.GetRootGameObjects()) Object.DestroyImmediate(r);

        BuildLighting();
        var level = new GameObject("Level").transform;
        BuildLevel(level);

        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = new Vector3(0, 1.2f, -16f);
        new GameObject("Game").AddComponent<GameBootstrap>();

        BuildPillar();

        Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    // 20:00 hours on Reach: low sun, smoky dusk sky, fires on the horizon
    static void BuildLighting()
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.9f; sun.color = new Color(1f, 0.6f, 0.35f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(14f, -40f, 0f);
        RenderSettings.sun = sun;

        string skyPath = Gen + "/Materials/DuskSky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (!sky)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(0.5f, 0.35f, 0.4f)); sky.SetColor("_GroundColor", new Color(0.2f, 0.15f, 0.17f));
            sky.SetFloat("_AtmosphereThickness", 1.8f); sky.SetFloat("_Exposure", 0.8f); sky.SetFloat("_SunSize", 0.05f);
            AssetDatabase.CreateAsset(sky, skyPath);
        }
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.38f, 0.3f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.24f, 0.3f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.12f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f; RenderSettings.fogColor = new Color(0.42f, 0.3f, 0.34f);
    }

    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); return g;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default(Vector3))
    {
        bool floor = name == "Floor";
        var tile = floor ? new Vector2(scale.x / 4f, scale.z / 4f) : new Vector2(Mathf.Max(1f, Mathf.Max(scale.x, scale.z) / 4f), Mathf.Max(1f, scale.y / 4f));
        var go = Prim(PrimitiveType.Cube, parent, name, pos, scale, Mat(color, 0f, floor ? floorTex : panelTex, tile, 0.25f), true, euler);
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void Strip(Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        Prim(PrimitiveType.Cube, parent, "Strip", pos, scale, Mat(color, 2.5f), false);
    }

    static readonly Color[] containerColors =
    {
        new Color(0.55f, 0.2f, 0.15f), new Color(0.2f, 0.4f, 0.45f), new Color(0.65f, 0.5f, 0.2f),
        new Color(0.25f, 0.3f, 0.5f), new Color(0.4f, 0.4f, 0.42f), new Color(0.3f, 0.45f, 0.3f)
    };

    // The Asźod ship-breaking yards: a big concrete yard ringed by shipping containers, with a central warehouse
    // (crate room, rooftop sniper nest), wrecks, a crane and burning fires.
    static void BuildLevel(Transform level)
    {
        var rng = new System.Random(11);
        var concrete = new Color(0.5f, 0.5f, 0.48f);
        var wall = new Color(0.6f, 0.58f, 0.55f);

        Box(level, "Floor", new Vector3(0, -0.5f, 0), new Vector3(135, 1, 135), concrete);

        // Perimeter: a triple-stacked wall of shipping containers (the yard has no way out)
        var perimeter = Group(level, "Perimeter");
        for (int side = 0; side < 4; side++)
            for (float o = -62f; o <= 62f; o += 12.4f)
            {
                Vector3 pos = side == 0 ? new Vector3(o, 0, 64f) : side == 1 ? new Vector3(o, 0, -64f) : side == 2 ? new Vector3(64f, 0, o) : new Vector3(-64f, 0, o);
                var size = side < 2 ? new Vector3(12.4f, 3f, 2.6f) : new Vector3(2.6f, 3f, 12.4f);
                for (int levelIdx = 0; levelIdx < 3; levelIdx++)
                    Box(perimeter, "WallContainer", pos + Vector3.up * (1.5f + levelIdx * 3f), size, containerColors[rng.Next(containerColors.Length)]);
            }

        // Main warehouse: south door, north alternate exit, flat roof reached by a stairway on the east side
        var wh = Group(level, "Warehouse");
        float h = 6f;
        Box(wh, "WallS_West", new Vector3(-9.5f, h / 2, 0.5f), new Vector3(11, h, 1), wall);
        Box(wh, "WallS_East", new Vector3(9.5f, h / 2, 0.5f), new Vector3(11, h, 1), wall);
        Box(wh, "WallN_West", new Vector3(-8.5f, h / 2, 19.5f), new Vector3(13, h, 1), wall);
        Box(wh, "WallN_East", new Vector3(8.5f, h / 2, 19.5f), new Vector3(13, h, 1), wall);
        Box(wh, "WallWest", new Vector3(-14.5f, h / 2, 10f), new Vector3(1, h, 20), wall);
        Box(wh, "WallEast", new Vector3(14.5f, h / 2, 10f), new Vector3(1, h, 20), wall);
        Box(wh, "Roof", new Vector3(0, 6.5f, 10f), new Vector3(30, 1, 20), concrete * 0.9f);
        Box(wh, "ParapetSouth", new Vector3(0, 7.4f, 0.4f), new Vector3(30, 0.8f, 0.4f), wall);
        Box(wh, "ParapetNorth", new Vector3(0, 7.4f, 19.6f), new Vector3(30, 0.8f, 0.4f), wall);
        var stairs = Group(wh, "RoofStairs");
        for (int i = 0; i < 14; i++)
            Box(stairs, "Stair" + i, new Vector3(25.6f - i * 0.8f, (i + 1) * 0.25f, 10f), new Vector3(0.8f, (i + 1) * 0.5f, 4f), concrete);

        // Crate room in the north-west corner of the warehouse: the three crates leave a hiding pocket behind them
        var room = Group(level, "CrateRoom");
        Box(room, "RoomWallSouth", new Vector3(-10.5f, h / 2, 12.5f), new Vector3(7, h, 0.6f), wall);
        Box(room, "RoomWallEast1", new Vector3(-7f, h / 2, 13.75f), new Vector3(0.6f, h, 2.5f), wall);
        Box(room, "RoomWallEast2", new Vector3(-7f, h / 2, 18f), new Vector3(0.6f, h, 2f), wall);
        var crate = new Color(0.55f, 0.4f, 0.2f);
        Box(room, "Crate1", new Vector3(-12.3f, 0.75f, 17.4f), Vector3.one * 1.5f, crate);
        Box(room, "Crate2", new Vector3(-10.8f, 0.75f, 17.4f), Vector3.one * 1.5f, crate);
        Box(room, "Crate3", new Vector3(-12.3f, 0.75f, 15.9f), Vector3.one * 1.5f, crate);

        var lights = Group(level, "Lights");
        AddLight(lights, new Vector3(-10.5f, 4.5f, 16f), new Color(1f, 0.85f, 0.6f), 12f, 1.2f).name = "RoomLight";
        AddLight(lights, new Vector3(0, 5f, 8f), new Color(1f, 0.8f, 0.6f), 22f, 1.1f).name = "HallLight";

        // Cover scattered across the yard
        var yard = Group(level, "YardCover");
        for (int i = 0; i < 26; i++)
        {
            float x = (float)(rng.NextDouble() * 96 - 48), z = (float)(rng.NextDouble() * 96 - 48);
            if (x > -20f && x < 34f && z > -6f && z < 26f) continue;          // warehouse + stairs
            if (new Vector2(x, z + 16f).magnitude < 9f) continue;             // player start
            bool rot = rng.NextDouble() < 0.5;
            var size = rot ? new Vector3(2.6f, 3f, 12.4f) : new Vector3(12.4f, 3f, 2.6f);
            Box(yard, "Container" + i, new Vector3(x, 1.5f, z), size, containerColors[rng.Next(containerColors.Length)]);
            if (rng.NextDouble() < 0.3) Box(yard, "ContainerTop" + i, new Vector3(x, 4.5f, z), size, containerColors[rng.Next(containerColors.Length)]);
        }
        for (int i = 0; i < 10; i++)   // low concrete barriers
        {
            float x = (float)(rng.NextDouble() * 80 - 40), z = (float)(rng.NextDouble() * 80 - 40);
            if (x > -20f && x < 34f && z > -6f && z < 26f) continue;
            Box(yard, "Barrier" + i, new Vector3(x, 0.6f, z), new Vector3(5f, 1.2f, 1f), concrete * 0.9f, new Vector3(0, rng.Next(0, 180), 0));
        }

        // Wrecked ship hulls and the crane
        var props = Group(level, "Wrecks");
        Box(props, "Wreck1", new Vector3(38f, 3f, -38f), new Vector3(30, 8, 10), new Color(0.25f, 0.25f, 0.28f), new Vector3(0, 25, 10));
        Box(props, "Wreck2", new Vector3(-42f, 2.5f, -34f), new Vector3(26, 6, 9), new Color(0.3f, 0.25f, 0.22f), new Vector3(0, -35, -8));
        var crane = Group(level, "Crane");
        var yellow = new Color(0.65f, 0.5f, 0.15f);
        Box(crane, "CraneLegA", new Vector3(-44f, 11f, 34f), new Vector3(1.5f, 22f, 1.5f), yellow);
        Box(crane, "CraneLegB", new Vector3(-30f, 11f, 34f), new Vector3(1.5f, 22f, 1.5f), yellow);
        Box(crane, "CraneArm", new Vector3(-37f, 22.5f, 34f), new Vector3(20f, 1.2f, 1.2f), yellow);
        Strip(crane, new Vector3(-37f, 23.4f, 34f), new Vector3(0.5f, 0.5f, 0.5f), new Color(1f, 0.1f, 0.1f));

        // Fires burning around the yard
        var fires = Group(level, "Fires");
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 2f / 9f + 0.3f; float r = 28f + (i % 3) * 9f;
            var fire = new GameObject("Fire" + i); fire.transform.SetParent(fires, false);
            fire.transform.position = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Prim(PrimitiveType.Cube, fire.transform, "Flame", Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f), Mat(new Color(1f, 0.5f, 0.1f), 2.5f), false);
            AddLight(fire.transform, Vector3.up * 2f, new Color(1f, 0.5f, 0.15f), 16f, 1.6f);
            fire.AddComponent<Flicker>();
        }

        // Supplies: the yard has a few weapons lying about, more up on the roof
        var pickups = Group(level, "Pickups");
        Place(pickups, "ar", new Vector3(-8f, 0.9f, 4f));
        Place(pickups, "shotgun", new Vector3(8f, 0.9f, 4f));
        Place(pickups, "sword", new Vector3(-9.5f, 0.9f, 15f));
        Place(pickups, "sniper", new Vector3(0f, 7.9f, 10f));
        Place(pickups, "sniper", new Vector3(-12f, 7.9f, 3f));
        Place(pickups, "turret", new Vector3(-3f, 0.9f, 17f));
        Place(pickups, "turret", new Vector3(7f, 7.9f, 4f));
        Place(pickups, "concussion", new Vector3(12f, 0.9f, 7f));
        Place(pickups, "Health", new Vector3(9f, 0.8f, 17f));
        Place(pickups, "DropShield", new Vector3(10.5f, 0.8f, 17f));

        // Enemy spawn ring just inside the container wall; skip points buried in wrecks or cover
        var spawns = new GameObject("SpawnPoints").transform;
        Physics.SyncTransforms();
        int count = 0;
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            var pt = new Vector3(Mathf.Cos(a) * 54f, 0.5f, Mathf.Sin(a) * 54f);
            if (Physics.CheckSphere(pt + Vector3.up * 3f, 1.8f)) continue;   // clear of the floor (top at y=0)
            new GameObject("SpawnPoint" + (++count)).transform.SetParent(spawns, false);
            spawns.GetChild(spawns.childCount - 1).position = pt;
        }
        if (count < 4)
            foreach (var p in new[] { new Vector3(0, 0.5f, -50f), new Vector3(0, 0.5f, 50f), new Vector3(50f, 0.5f, 0), new Vector3(-50f, 0.5f, 0) })
            { var g = new GameObject("SpawnFallback"); g.transform.SetParent(spawns, false); g.transform.position = p; }
    }

    // Weapon / equipment pickups placed in the scene are prefab instances: move them, change ammo, or delete them freely
    static void Place(Transform parent, string prefabName, Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Pickups/" + prefabName + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
        var pk = go.GetComponent<Pickup>();
        pk.permanent = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(pk);
    }

    // The UNSC Pillar of Autumn slips away over the yard: Noble Six is alone
    static void BuildPillar()
    {
        var go = new GameObject("PillarOfAutumn");
        go.transform.position = new Vector3(-70f, 55f, 110f);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, 0.5f, 1f));
        go.AddComponent<Departure>();
        Material Unlit(Color c) { return Mat(c, 0f, null, default(Vector2), 0f, "Sprites/Default"); }   // unlit so fog doesn't swallow the silhouette
        Prim(PrimitiveType.Cube, go.transform, "Hull", Vector3.zero, new Vector3(10f, 7f, 70f), Unlit(new Color(0.12f, 0.12f, 0.15f)));
        Prim(PrimitiveType.Cube, go.transform, "Fin", new Vector3(0, 0, 25f), new Vector3(22f, 1.5f, 14f), Unlit(new Color(0.1f, 0.1f, 0.13f)));
        Prim(PrimitiveType.Sphere, go.transform, "Engine", new Vector3(0, 0, -36f), new Vector3(8f, 6f, 8f), Unlit(new Color(1f, 0.6f, 0.2f)));
    }
}
