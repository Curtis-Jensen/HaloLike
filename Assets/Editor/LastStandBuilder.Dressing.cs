using UnityEditor;
using UnityEngine;

// Environment dressing for the yard: start platform, hangars, lamp posts, barrels, fallen troopers, smoke / ember / ash particles,
// and the distant glassing beams and Covenant ships that make the sky feel like Reach burning.
public static partial class LastStandBuilder
{
    static readonly Color hazardYellow = new Color(0.78f, 0.62f, 0.08f);
    static readonly Color rustA = new Color(0.45f, 0.22f, 0.12f), rustB = new Color(0.25f, 0.3f, 0.34f), rustC = new Color(0.3f, 0.36f, 0.22f);

    // Capsule/cylinder stretched between two local points (used for limbs, poles, pipes)
    static GameObject Limb(Transform parent, PrimitiveType t, string name, Vector3 from, Vector3 to, float thickness, Material mat, bool collider = false)
    {
        var d = to - from;
        var go = Prim(t, parent, name, (from + to) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), mat, collider);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return go;
    }

    static void BuildDressing(Transform level, System.Random rng)
    {
        var dress = Group(level, "Dressing");
        StartPlatform(dress);

        // Lamp posts: warm-white floods around the warehouse, platform and yard
        var lamps = Group(dress, "LampPosts");
        foreach (var p in new[] { new Vector3(-17f, 0, -2f), new Vector3(17f, 0, -2f), new Vector3(-17f, 0, 22f), new Vector3(17f, 0, 22f),
                                  new Vector3(-9f, 0, -12f), new Vector3(9f, 0, -12f), new Vector3(-30f, 0, 8f), new Vector3(30f, 0, -14f),
                                  new Vector3(0f, 0, 34f), new Vector3(-28f, 0, -26f) })
            LampPost(lamps, p);

        // Barrel-vault hangars: the iconic arched sheds of the yard
        var hangars = Group(dress, "Hangars");
        Hangar(hangars, new Vector3(-47f, 0, 2f), 0f, 26f, 9f);
        Hangar(hangars, new Vector3(47f, 0, 33f), 90f, 24f, 8f);

        // Fuel drums and crates in clusters
        var junk = Group(dress, "Barrels");
        foreach (var c in new[] { new Vector3(-20f, 0, -8f), new Vector3(21f, 0, -9f), new Vector3(-24f, 0, 17f), new Vector3(24f, 0, 27f),
                                  new Vector3(4f, 0, 14f), new Vector3(-6f, 0, 28f), new Vector3(33f, 0, 8f), new Vector3(-34f, 0, -14f) })
            BarrelCluster(junk, c, rng);

        // Fallen UNSC troopers (the level is strewn with them)
        var dead = Group(dress, "DeadTroopers");
        DeadTrooper(dead, new Vector3(-4f, 0.02f, -7.5f), 20f, false);
        DeadTrooper(dead, new Vector3(6f, 0.02f, -5.5f), -70f, true);
        DeadTrooper(dead, new Vector3(5f, 0.02f, 6f), 110f, false);
        DeadTrooper(dead, new Vector3(-6f, 0.02f, 11f), -20f, true);
        DeadTrooper(dead, new Vector3(-28f, 0.02f, 4f), 60f, false);
        DeadTrooper(dead, new Vector3(22f, 0.02f, 16f), 160f, true);
        DeadTrooper(dead, new Vector3(-12f, 0.02f, -26f), -110f, false);

        // Smoke columns and embers over the fires; huge dark plumes on the horizon
        var fireGroup = level.Find("Fires");
        if (fireGroup)
            foreach (Transform fire in fireGroup)
            {
                Smoke(fire, new Vector3(0, 0.8f, 0), 1.0f, 9f);
                Embers(fire, new Vector3(0, 1f, 0));
                Flames(fire, new Vector3(0, 0.3f, 0));
            }
        var plumes = Group(dress, "HorizonSmoke");
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f + 0.2f, dist = 190f + (i % 3) * 40f;
            Smoke(plumes, new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist), 28f, 60f);
        }

        // Covenant glassing: purple beams on the horizon and ships holding station in the smoke
        var sky = Group(dress, "Sky");
        var beamMat = Mat(new Color(0.7f, 0.4f, 1f, 0.22f), 0f, null, default(Vector2), 0f, "Sprites/Default");
        foreach (var bp in new[] { new Vector2(260f, 120f), new Vector2(-240f, 200f), new Vector2(40f, 290f), new Vector2(-120f, -280f) })
            Prim(PrimitiveType.Cylinder, sky, "GlassingBeam", new Vector3(bp.x, 220f, bp.y), new Vector3(7f, 220f, 7f), beamMat, false);
        var shipMat = Mat(new Color(0.22f, 0.18f, 0.3f), 0f, null, default(Vector2), 0f, "Sprites/Default");
        var lineMat = Mat(new Color(0.75f, 0.6f, 1f, 0.6f), 0f, null, default(Vector2), 0f, "Sprites/Default");
        foreach (var sp in new[] { new Vector3(120f, 75f, 150f), new Vector3(-150f, 90f, 90f), new Vector3(30f, 65f, 210f) })
        {
            var ship = Group(sky, "CovenantShip"); ship.position = sp; ship.rotation = Quaternion.Euler(0, Mathf.Atan2(-sp.x, -sp.z) * Mathf.Rad2Deg + 90f, 0);
            Prim(PrimitiveType.Sphere, ship, "Hull", Vector3.zero, new Vector3(34f, 9f, 16f), shipMat, false);
            Prim(PrimitiveType.Cube, ship, "WingL", new Vector3(-9f, 1f, -5f), new Vector3(14f, 1.2f, 9f), shipMat, false, new Vector3(0, 18f, 8f));
            Prim(PrimitiveType.Cube, ship, "WingR", new Vector3(9f, 1f, -5f), new Vector3(14f, 1.2f, 9f), shipMat, false, new Vector3(0, -18f, -8f));
            // plasma lines raking the ground
            var line = Prim(PrimitiveType.Cube, ship, "PlasmaLine", new Vector3(0, -30f, 0), new Vector3(0.35f, 70f, 0.35f), lineMat, false, new Vector3(0, 0, Random.Range(-14f, 14f)));
        }
    }

    // Raised steel platform where Noble Six begins (the wiki's opening shot): grating, yellow rails, steps toward the warehouse
    static void StartPlatform(Transform dress)
    {
        var g = Group(dress, "StartPlatform");
        var steel = new Color(0.42f, 0.42f, 0.45f);
        Box(g, "PlatformDeck", new Vector3(0, 0.5f, -16f), new Vector3(16f, 1f, 12f), steel);
        var yellow = Mat(hazardYellow, 0f, null, default(Vector2), 0.3f, "Standard", 0.3f);
        // railings on the west, east and south edges (open to the north where the steps are)
        for (float x = -7.5f; x <= 7.5f; x += 2.5f)
        {
            Prim(PrimitiveType.Cube, g, "PostS", new Vector3(x, 1.55f, -21.9f), new Vector3(0.1f, 1.1f, 0.1f), yellow, false);
        }
        Prim(PrimitiveType.Cube, g, "RailS", new Vector3(0, 2.05f, -21.9f), new Vector3(15.8f, 0.08f, 0.08f), yellow, true);
        Prim(PrimitiveType.Cube, g, "RailS2", new Vector3(0, 1.6f, -21.9f), new Vector3(15.8f, 0.06f, 0.06f), yellow, false);
        foreach (float sx in new[] { -7.9f, 7.9f })
        {
            for (float z = -21.5f; z <= -11f; z += 2.5f) Prim(PrimitiveType.Cube, g, "PostSide", new Vector3(sx, 1.55f, z), new Vector3(0.1f, 1.1f, 0.1f), yellow, false);
            Prim(PrimitiveType.Cube, g, "RailSide", new Vector3(sx, 2.05f, -16.3f), new Vector3(0.08f, 0.08f, 11f), yellow, true);
            Prim(PrimitiveType.Cube, g, "RailSide2", new Vector3(sx, 1.6f, -16.3f), new Vector3(0.06f, 0.06f, 11f), yellow, false);
        }
        // steps down toward the yard
        for (int i = 0; i < 2; i++)
        {
            float top = 1f - 0.33f * (i + 1);
            Box(g, "PlatformStep" + i, new Vector3(0, top * 0.5f, -9.6f + i * 0.8f), new Vector3(7f, top, 0.8f), steel * 0.9f);
        }
        // support legs and a floodlight tower at the back corners
        foreach (float sx in new[] { -7.5f, 7.5f })
            LampPost(g, new Vector3(sx, 1f, -21f));
    }

    static void LampPost(Transform parent, Vector3 pos)
    {
        var g = Group(parent, "LampPost"); g.position = pos;
        var steel = Mat(new Color(0.25f, 0.26f, 0.28f), 0f, null, default(Vector2), 0.35f, "Standard", 0.6f);
        Prim(PrimitiveType.Cylinder, g, "Pole", new Vector3(0, 3.2f, 0), new Vector3(0.18f, 3.2f, 0.18f), steel, true);
        Prim(PrimitiveType.Cube, g, "Arm", new Vector3(0.5f, 6.35f, 0), new Vector3(1.3f, 0.1f, 0.12f), steel, false);
        Prim(PrimitiveType.Cube, g, "Head", new Vector3(1.05f, 6.25f, 0), new Vector3(0.7f, 0.12f, 0.3f), steel, false);
        Prim(PrimitiveType.Cube, g, "Lens", new Vector3(1.05f, 6.17f, 0), new Vector3(0.6f, 0.04f, 0.24f), Mat(new Color(0.85f, 0.95f, 1f), 3f), false);
        var l = AddLight(g, new Vector3(1.05f, 5.9f, 0), new Color(0.85f, 0.92f, 1f), 24f, 1.5f);
        l.name = "LampLight";
    }

    // Arched shed made of rotated slabs along a half circle, open at both ends
    static void Hangar(Transform parent, Vector3 pos, float yaw, float length, float radius)
    {
        var g = Group(parent, "HangarShed"); g.position = pos; g.rotation = Quaternion.Euler(0, yaw, 0);
        var skin = new Color(0.55f, 0.5f, 0.45f);
        const int n = 14;
        float slab = Mathf.PI * radius / n * 1.08f;
        for (int i = 0; i < n; i++)
        {
            float th = Mathf.PI * (i + 0.5f) / n;
            var p = new Vector3(Mathf.Cos(th) * radius, Mathf.Sin(th) * radius, 0f);
            var box = Box(g, "HangarSlab" + i, p, new Vector3(0.35f, slab, length), skin, Vector3.zero);
            box.transform.localRotation = Quaternion.Euler(0, 0, th * Mathf.Rad2Deg);
        }
        // end arches / ribs
        var rib = Mat(new Color(0.18f, 0.18f, 0.2f), 0f, null, default(Vector2), 0.3f, "Standard", 0.6f);
        for (float z = -length * 0.5f; z <= length * 0.5f + 0.1f; z += length / 4f)
            for (int i = 0; i < n; i++)
            {
                float th = Mathf.PI * (i + 0.5f) / n;
                var r = Prim(PrimitiveType.Cube, g, "Rib", new Vector3(Mathf.Cos(th) * (radius - 0.25f), Mathf.Sin(th) * (radius - 0.25f), z), new Vector3(0.2f, slab, 0.25f), rib, false);
                r.transform.localRotation = Quaternion.Euler(0, 0, th * Mathf.Rad2Deg);
            }
        AddLight(g, new Vector3(0, radius - 1.5f, 0), new Color(1f, 0.82f, 0.6f), 16f, 1.0f).name = "ShedLight";
    }

    static void BarrelCluster(Transform parent, Vector3 center, System.Random rng)
    {
        var g = Group(parent, "BarrelCluster"); g.position = center;
        int n = 3 + rng.Next(3);
        for (int i = 0; i < n; i++)
        {
            var c = new[] { rustA, rustB, rustC }[rng.Next(3)];
            var m = Mat(c, 0f, containerTex, new Vector2(1f, 1f), 0.3f, "Standard", 0.4f);
            float x = (float)(rng.NextDouble() * 3 - 1.5), z = (float)(rng.NextDouble() * 3 - 1.5);
            bool fallen = rng.NextDouble() < 0.25;
            var b = Prim(PrimitiveType.Cylinder, g, "Barrel", new Vector3(x, fallen ? 0.3f : 0.55f, z), new Vector3(0.6f, 0.55f, 0.6f), m, true,
                         fallen ? new Vector3(90f, (float)rng.NextDouble() * 360f, 0) : new Vector3(0, (float)rng.NextDouble() * 360f, 0));
            GameObjectUtility.SetStaticEditorFlags(b, StaticEditorFlags.BatchingStatic);
            if (!fallen) Prim(PrimitiveType.Cylinder, g, "BarrelBand", new Vector3(x, 0.8f, z), new Vector3(0.63f, 0.03f, 0.63f), Mat(c * 0.6f), false);
        }
    }

    // A lying marine / ODST with a dropped rifle
    static void DeadTrooper(Transform parent, Vector3 pos, float yaw, bool odst)
    {
        var g = Group(parent, "DeadTrooper"); g.position = pos; g.rotation = Quaternion.Euler(0, yaw, 0);
        var armor = Mat(odst ? new Color(0.17f, 0.18f, 0.22f) : new Color(0.2f, 0.28f, 0.17f), 0f, null, default(Vector2), 0.35f, "Standard", 0.3f);
        var suit = Mat(new Color(0.12f, 0.12f, 0.13f));
        var gunMat = Mat(new Color(0.07f, 0.07f, 0.08f), 0f, null, default(Vector2), 0.5f, "Standard", 0.7f);
        Prim(PrimitiveType.Capsule, g, "Torso", new Vector3(0, 0.2f, 0), new Vector3(0.42f, 0.32f, 0.3f), armor, false, new Vector3(90f, 0, 0));
        Prim(PrimitiveType.Sphere, g, "Helmet", new Vector3(0.04f, 0.2f, 0.62f), new Vector3(0.3f, 0.3f, 0.32f), armor, false);
        Prim(PrimitiveType.Sphere, g, "Visor", new Vector3(0.05f, 0.2f, 0.74f), new Vector3(0.2f, 0.12f, 0.1f), Mat(new Color(0.9f, 0.7f, 0.2f), 0.6f), false);
        Limb(g, PrimitiveType.Capsule, "LegL", new Vector3(-0.12f, 0.12f, -0.35f), new Vector3(-0.2f, 0.1f, -1.0f), 0.17f, suit);
        Limb(g, PrimitiveType.Capsule, "LegR", new Vector3(0.12f, 0.12f, -0.35f), new Vector3(0.38f, 0.1f, -0.95f), 0.17f, suit);
        Limb(g, PrimitiveType.Capsule, "ArmL", new Vector3(-0.28f, 0.15f, 0.3f), new Vector3(-0.6f, 0.1f, 0.0f), 0.13f, armor);
        Limb(g, PrimitiveType.Capsule, "ArmR", new Vector3(0.28f, 0.15f, 0.3f), new Vector3(0.55f, 0.1f, 0.65f), 0.13f, armor);
        Prim(PrimitiveType.Cube, g, "Rifle", new Vector3(0.95f, 0.05f, 0.3f), new Vector3(0.08f, 0.07f, 0.85f), gunMat, false, new Vector3(0, 25f, 0));
    }

    // ---- particles ----

    static Material ParticleMat(string shader, Color tint)
    {
        string key = Gen + "/Materials/Particle_" + shader.Replace('/', '_').Replace(' ', '_') + "_" + ColorUtility.ToHtmlStringRGBA(tint) + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(key);
        if (m) return m;
        m = new Material(Shader.Find(shader));
        m.SetColor("_TintColor", tint);
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        m.mainTexture = tex;
        AssetDatabase.CreateAsset(m, key);
        return m;
    }

    static ParticleSystem NewSystem(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        return go.AddComponent<ParticleSystem>();
    }

    // Slow dark smoke rising and spreading
    static void Smoke(Transform parent, Vector3 pos, float size, float lifetime)
    {
        var ps = NewSystem(parent, "Smoke", pos);
        var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = lifetime; main.startSpeed = 1.2f + size * 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f * size, 2.5f * size);
        main.startColor = new Color(0.16f, 0.13f, 0.12f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 60 + (int)size * 4; main.gravityModifier = -0.02f;
        var em = ps.emission; em.rateOverTime = Mathf.Max(1.5f, 9f / Mathf.Sqrt(size));
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.4f * size;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.35f), new Keyframe(1, 2.2f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.7f, 0.65f), 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Legacy Shaders/Particles/Alpha Blended", new Color(0.6f, 0.6f, 0.6f, 0.5f));
    }

    // Licking flames above the fire pit
    static void Flames(Transform parent, Vector3 pos)
    {
        var ps = NewSystem(parent, "Flames", pos);
        var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 0.9f; main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); main.startColor = new Color(1f, 0.55f, 0.15f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 50; main.gravityModifier = -0.1f;
        var em = ps.emission; em.rateOverTime = 28f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.35f;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.9f), new Keyframe(1, 0.1f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f) },
                     new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Legacy Shaders/Particles/Additive", new Color(1f, 0.6f, 0.25f, 1f));
    }

    // Glowing embers drifting up from a fire
    static void Embers(Transform parent, Vector3 pos)
    {
        var ps = NewSystem(parent, "Embers", pos);
        var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 4f; main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f); main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 60; main.gravityModifier = -0.05f;
        var em = ps.emission; em.rateOverTime = 10f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.4f;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.6f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Legacy Shaders/Particles/Additive", new Color(1f, 0.6f, 0.25f, 1f));
    }

    // Drifting ash and dust that follows the player's camera
    static void Ash(Transform camera)
    {
        var ps = NewSystem(camera, "Ash", Vector3.zero);
        var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 8f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f); main.startColor = new Color(0.85f, 0.72f, 0.6f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 600; main.gravityModifier = 0.01f;
        var em = ps.emission; em.rateOverTime = 70f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(36f, 16f, 36f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.3f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = 0.8f; vel.z = 0.3f;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Legacy Shaders/Particles/Alpha Blended", new Color(0.9f, 0.8f, 0.7f, 0.5f));
    }
}
