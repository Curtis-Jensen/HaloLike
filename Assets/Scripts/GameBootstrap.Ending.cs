using UnityEngine;

// Noble Six's death, as in Lone Wolf: a Zealot ignites an energy dagger and stabs down at the fallen Spartan, a Field Marshal's
// boot steps into frame, another Sangheili ignites a sword, cut to black. Then the shattered helmet in the dust of the yards,
// and the epilogue: a peaceful grassy plain, frigate wreckage in the distance, 7 July 2589, Halsey's narration.
public partial class GameBootstrap
{
    // Timeline (seconds since death)
    const float TStab = 3.0f, TBoot = 3.8f, TSword = 4.5f, TBlack = 5.5f, TDustIn = 6.5f, TDustOut = 10.5f, TEpilogue = 11.5f;

    Transform deathCam;
    Vector3 camStartPos, povPos, dustCamPos, dustLook, plainCamPos;
    Quaternion camStartRot, povRot;
    GameObject dagger, boot, blocker, flash;
    Transform zealotActor;
    Vector3 stabFrom, stabTo, bootFrom, bootTo;
    Enemy[] actors;
    int endPhase;
    static readonly Vector3 DustOrigin = new Vector3(1000f, 0f, 0f), PlainOrigin = new Vector3(-1000f, 0f, 0f);

    static Material Lit(Color c, float glow = 0f)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        if (glow > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * glow); }
        return m;
    }

    static GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default(Vector3), Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(t); Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false); go.transform.position = pos; go.transform.localScale = scale; go.transform.eulerAngles = euler;
        go.GetComponent<Renderer>().material = m;
        return go;
    }

    static GameObject GlowCube(Vector3 scale, Color c, float lightIntensity)
    {
        var go = Prim(PrimitiveType.Cube, Vector3.zero, scale, Lit(c, 4f));
        var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(go.transform, false);
        l.color = c; l.range = 8f; l.intensity = lightIntensity;
        return go;
    }

    void StartEnding(Player p, Camera cam)
    {
        endStart = Time.time;
        Cursor.visible = false;
        Sfx.Play2D("death", 0.7f);

        // Everything else in the yard goes quiet; only the staged Sangheili remain
        foreach (var e in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (e) Destroy(e.gameObject);
        foreach (var ph in FindObjectsOfType<Phantom>()) Destroy(ph.gameObject);

        Vector3 floorPos = p.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        // Camera: Six's view from the ground, looking up at the Zealot
        deathCam = cam.transform; deathCam.SetParent(null);
        camStartPos = deathCam.position; camStartRot = deathCam.rotation;
        povPos = floorPos + Vector3.up * 0.3f;
        povRot = Quaternion.LookRotation(fwd * 2.2f + Vector3.up * 1.1f);

        var zeal = Actor(Enemy.Kind.Zealot, floorPos + fwd * 2.4f, povPos);
        var elite2 = Actor(Enemy.Kind.Elite, floorPos + fwd * 2.8f + right * 2.2f, povPos);
        var elite3 = Actor(Enemy.Kind.Elite, floorPos + fwd * 2.6f - right * 2.3f, povPos);
        var elite4 = Actor(Enemy.Kind.General, floorPos + fwd * 4.8f + right * 0.8f, povPos);
        // Seven Sangheili in all stand over the fallen Spartan
        var ring = new Enemy[3];
        for (int i = 0; i < 3; i++)
        {
            float a = Mathf.PI * (0.35f + 0.65f * i / 2f);
            ring[i] = Actor(i == 1 ? Enemy.Kind.Zealot : Enemy.Kind.Elite, floorPos + fwd * (5.5f * Mathf.Sin(a)) + right * (5.5f * Mathf.Cos(a)), povPos);
        }
        actors = new[] { zeal, elite2, elite3, elite4, ring[0], ring[1], ring[2] };
        zealotActor = zeal ? zeal.transform : null;

        // The energy dagger: ignites above the fallen Spartan, then stabs down
        stabFrom = floorPos + fwd * 1.2f + Vector3.up * 1.9f;
        stabTo = floorPos + fwd * 0.6f + Vector3.up * 0.4f;
        dagger = GlowCube(new Vector3(0.06f, 0.06f, 0.5f), new Color(0.4f, 0.9f, 1f), 3f);
        dagger.SetActive(false);
        flash = new GameObject("StabFlash"); var fl = flash.AddComponent<Light>(); fl.type = LightType.Point; fl.color = new Color(0.6f, 0.9f, 1f); fl.range = 12f; fl.intensity = 0f;
        flash.transform.position = stabTo + Vector3.up * 0.5f;

        // A Field Marshal's boot steps into frame
        bootFrom = floorPos + fwd * 1.6f + right * 3.2f; bootTo = floorPos + fwd * 1.5f + right * 0.9f;
        boot = new GameObject("FieldMarshalBoot");
        var armor = Lit(new Color(0.07f, 0.07f, 0.09f)); var trim = Lit(new Color(0.9f, 0.7f, 0.2f), 1.2f);
        Prim(PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.4f, 1.3f), armor, Vector3.zero, boot.transform);
        Prim(PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 3f, 0.55f), armor, Vector3.zero, boot.transform).transform.localPosition = new Vector3(0, 1.8f, -0.25f);
        Prim(PrimitiveType.Cube, Vector3.zero, new Vector3(0.62f, 0.06f, 0.5f), trim, Vector3.zero, boot.transform).transform.localPosition = new Vector3(0, 0.22f, 0.3f);
        boot.transform.position = bootFrom; boot.transform.rotation = Quaternion.LookRotation(-right);
        boot.SetActive(false);

        // A second sword ignites right in front of the lens
        blocker = GlowCube(new Vector3(0.14f, 2.6f, 0.14f), new Color(0.4f, 0.9f, 1f), 5f);
        blocker.SetActive(false);

        BuildDustSet();
        BuildPlainSet();
    }

    // A staged Sangheili (no AI, no collision): used for the cinematic
    Enemy Actor(Enemy.Kind kind, Vector3 pos, Vector3 lookAt)
    {
        var e = Spawner.Spawn(kind, Enemy.Rank.Ultra, pos, 1f);
        if (!e) return null;
        var cc = e.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
        e.enabled = false;
        e.transform.position = new Vector3(pos.x, pos.y, pos.z);
        Vector3 d = lookAt - pos; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) e.transform.rotation = Quaternion.LookRotation(d);
        return e;
    }

    // Shattered helmet lying in the dust of the Asźod yards, as in the opening
    void BuildDustSet()
    {
        var o = DustOrigin;
        Prim(PrimitiveType.Cube, o + Vector3.down * 0.5f, new Vector3(80f, 1f, 80f), Lit(new Color(0.5f, 0.4f, 0.3f)));
        var shell = Lit(new Color(0.45f, 0.5f, 0.4f)); var gold = Lit(new Color(1f, 0.7f, 0.1f), 1.2f);
        Prim(PrimitiveType.Sphere, o + new Vector3(0f, 0.12f, 0f), new Vector3(0.4f, 0.22f, 0.42f), shell, new Vector3(10f, 30f, 35f));
        Prim(PrimitiveType.Sphere, o + new Vector3(0.35f, 0.07f, 0.2f), new Vector3(0.22f, 0.12f, 0.2f), shell, new Vector3(0f, 80f, 20f));
        Prim(PrimitiveType.Cube, o + new Vector3(-0.3f, 0.04f, 0.15f), new Vector3(0.18f, 0.03f, 0.12f), gold, new Vector3(0f, 20f, 8f));
        Prim(PrimitiveType.Cube, o + new Vector3(0.15f, 0.03f, -0.3f), new Vector3(0.14f, 0.03f, 0.1f), gold, new Vector3(0f, -40f, 5f));
        for (int i = 0; i < 14; i++)   // small rubble
            Prim(PrimitiveType.Cube, o + new Vector3(Random.Range(-4f, 4f), 0.05f, Random.Range(-4f, 4f)), Vector3.one * Random.Range(0.06f, 0.25f), Lit(new Color(0.35f, 0.3f, 0.27f)), new Vector3(0, Random.value * 360f, 0));
        var light = new GameObject("DustLight").AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, 0.6f, 0.3f); light.range = 10f; light.intensity = 1.6f;
        light.transform.position = o + new Vector3(-1.5f, 1.2f, -1.5f);
        dustCamPos = o + new Vector3(-0.9f, 0.4f, -1.1f); dustLook = o + new Vector3(0f, 0.12f, 0f);
    }

    // Reach, long after: a grassy plain under blue sky, a frigate's wreckage on the horizon
    void BuildPlainSet()
    {
        var o = PlainOrigin;
        Prim(PrimitiveType.Cube, o + Vector3.down * 0.5f, new Vector3(600f, 1f, 600f), Lit(new Color(0.3f, 0.55f, 0.22f)));
        for (int i = 0; i < 9; i++)   // gentle hills
            Prim(PrimitiveType.Sphere, o + new Vector3(Random.Range(-250f, 250f), -4f, Random.Range(60f, 260f)), new Vector3(Random.Range(60f, 120f), Random.Range(10f, 22f), Random.Range(60f, 120f)), Lit(new Color(0.26f, 0.5f, 0.2f)));
        var hull = Lit(new Color(0.22f, 0.22f, 0.26f));
        Prim(PrimitiveType.Cube, o + new Vector3(30f, 9f, 130f), new Vector3(60f, 16f, 20f), hull, new Vector3(0f, 18f, 8f));
        Prim(PrimitiveType.Cube, o + new Vector3(70f, 14f, 150f), new Vector3(30f, 30f, 12f), hull, new Vector3(0f, 35f, -14f));
        Prim(PrimitiveType.Cube, o + new Vector3(-10f, 5f, 120f), new Vector3(24f, 8f, 10f), hull, new Vector3(0f, -25f, 20f));
        plainCamPos = o + new Vector3(0f, 1.7f, 0f);
    }

    void UpdateEnding()
    {
        if (endStart < 0f) return;
        float t = Time.time - endStart;
        if (!deathCam) return;

        if (t < TBlack)
        {
            // Phase A: Six's last moments
            deathCam.position = Vector3.Lerp(camStartPos, povPos, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.5f)));
            deathCam.rotation = Quaternion.Slerp(camStartRot, povRot, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.5f)));
            Sfx.MusicVolume = Mathf.Lerp(0.45f, 0.05f, t / 3f);

            if (t > 1.8f && t < TStab + 0.3f && dagger)
            {
                if (!dagger.activeSelf) { dagger.SetActive(true); Sfx.Play2D("sword", 0.8f); }
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TStab - 0.25f, TStab, t));     // raised, then a fast downward stab
                dagger.transform.position = Vector3.Lerp(stabFrom, stabTo, k);
                dagger.transform.rotation = Quaternion.LookRotation((stabTo - stabFrom).normalized);
            }
            if (t >= TStab && endPhase == 0) { endPhase = 1; Sfx.Play2D("hit", 1f); if (flash) flash.GetComponent<Light>().intensity = 10f; }
            if (flash && endPhase >= 1) flash.GetComponent<Light>().intensity = Mathf.MoveTowards(flash.GetComponent<Light>().intensity, 0f, Time.deltaTime * 14f);

            if (t > TBoot && boot)
            {
                if (!boot.activeSelf) { boot.SetActive(true); Sfx.Play2D("hit", 0.9f); }
                boot.transform.position = Vector3.Lerp(bootFrom, bootTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - TBoot) / 0.9f)));
            }
            if (t > TSword && blocker)
            {
                if (!blocker.activeSelf) { blocker.SetActive(true); Sfx.Play2D("sword", 1f); }
                blocker.transform.position = deathCam.position + deathCam.forward * 1.1f + deathCam.right * Mathf.Lerp(0.8f, -0.1f, (t - TSword) / 0.8f);
                blocker.transform.rotation = deathCam.rotation * Quaternion.Euler(0f, 0f, 12f);
            }
        }
        else if (t < TEpilogue)
        {
            // Phase B: the helmet, in the dust
            if (endPhase < 2) { endPhase = 2; HideStaging(); }
            float k = Mathf.Clamp01((t - TDustIn) / 4f);
            deathCam.position = Vector3.Lerp(dustCamPos, dustCamPos + new Vector3(0.25f, 0.05f, 0.2f), k);
            deathCam.rotation = Quaternion.LookRotation(dustLook - deathCam.position);
            Sfx.MusicVolume = 0f;
        }
        else
        {
            // Phase C: epilogue on a peaceful plain
            if (endPhase < 3) { endPhase = 3; EpilogueLighting(); Sfx.MusicVolume = 0.3f; }
            float k = Mathf.Clamp01((t - TEpilogue) / 20f);
            deathCam.position = plainCamPos + Vector3.forward * k * 3f;
            deathCam.rotation = Quaternion.Euler(Mathf.Lerp(6f, -2f, k), Mathf.Lerp(-12f, 10f, k), 0f);
        }

        if (t > TEpilogue + 23f && Input.GetKeyDown(KeyCode.Return))
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    void HideStaging()
    {
        foreach (var a in actors) if (a) a.gameObject.SetActive(false);
        if (dagger) dagger.SetActive(false);
        if (boot) boot.SetActive(false);
        if (blocker) blocker.SetActive(false);
    }

    void EpilogueLighting()
    {
        var sun = RenderSettings.sun;
        if (sun) { sun.color = new Color(1f, 0.96f, 0.88f); sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(48f, -30f, 0f); }
        if (RenderSettings.skybox)
        {
            var sky = new Material(RenderSettings.skybox);      // copy so the project's sky asset isn't modified
            sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 1f)); sky.SetColor("_GroundColor", new Color(0.4f, 0.5f, 0.4f));
            sky.SetFloat("_AtmosphereThickness", 1f); sky.SetFloat("_Exposure", 1.25f); sky.SetFloat("_SunSize", 0.05f);
            RenderSettings.skybox = sky;
        }
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.75f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.65f, 0.7f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.35f, 0.25f);
        RenderSettings.fogDensity = 0.002f; RenderSettings.fogColor = new Color(0.75f, 0.85f, 1f);
    }
}
