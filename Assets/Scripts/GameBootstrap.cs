using System.Collections.Generic;
using UnityEngine;

// Halo: Reach "Lone Wolf" — Noble Six's last stand at the Asźod ship-breaking yards.
// Lives on the "Game" object in the scene. The yard, Player and prefabs are authored in the scene (Tools > Last Stand > Build Scene);
// this component runs the endless Covenant director, the HUD and the unwinnable ending.
public partial class GameBootstrap : MonoBehaviour
{
    // Lone Wolf cannot be beaten: at this point the final stand begins and Noble Six is going to fall.
    public const float FinalStandTime = 360f;

    public static bool FinalStand { get; private set; }
    public static float Elapsed { get { return inst ? Mathf.Max(0f, Time.time - inst.startTime) : 0f; } }
    // Damage ramps up during the final stand so death is certain, however well you play
    public static float DamageMultiplier { get { return FinalStand && inst ? 1f + (Time.time - inst.finalStart) / 15f : 1f; } }

    static GameBootstrap inst;

    int kills;
    float helmetOff = -1f;
    float startTime, nextSpawn, nextPhantom, finalStart, endStart = -1f;
    string banner = ""; float bannerUntil;
    readonly List<Vector3> spawnPoints = new List<Vector3>();
    Texture2D white, circleTex;
    Transform helmet, deathCam, marshal;
    Material floorMat;
    Vector3 camStartPos; Quaternion camStartRot;

    void Awake()
    {
        inst = this;
        FinalStand = false;
        white = Texture2D.whiteTexture;
        circleTex = MakeCircle(128);

        // Spawn points, floor and everything else are scene objects you can move around in the editor
        var sp = GameObject.Find("SpawnPoints");
        if (sp) foreach (Transform t in sp.transform) spawnPoints.Add(t.position);
        if (spawnPoints.Count == 0)
        {
            Debug.LogError("No SpawnPoints in the scene - run Tools > Last Stand > Build Scene");
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; spawnPoints.Add(new Vector3(Mathf.Cos(a) * 50f, 0.5f, Mathf.Sin(a) * 50f)); }
        }
        var floor = GameObject.Find("Floor");
        if (floor) floorMat = floor.GetComponent<Renderer>().material;
    }

    void Start()
    {
        Sfx.StartMusic();
        Sfx.MusicVolume = 0.45f;
        startTime = Time.time + 6f;          // the Pillar of Autumn pulls away; then the first wave
        nextSpawn = startTime;
        nextPhantom = 120f;
        Banner("SURVIVE AS LONG AS POSSIBLE", 5f);
    }

    // ---------- Director: endless, escalating Covenant assault with no breaks between waves ----------

    void Update()
    {
        var player = Player.Instance;
        if (!player) return;

        if (player.IsDead) { UpdateEnding(); return; }

        float t = Time.time - startTime;
        if (t < 0f) return;
        if (!FinalStand && t >= FinalStandTime) BeginFinalStand();

        float prog = Mathf.Clamp01(t / FinalStandTime);
        int maxAlive = FinalStand ? 28 : Mathf.RoundToInt(Mathf.Lerp(5f, 22f, prog));
        float interval = FinalStand ? 0.8f : Mathf.Lerp(2.6f, 1f, prog);
        if (Time.time >= nextSpawn && Enemy.All.Count < maxAlive) { nextSpawn = Time.time + interval; SpawnNext(t); }

        if (!FinalStand && t >= nextPhantom)
        {
            nextPhantom = t + Mathf.Lerp(80f, 55f, prog);
            Phantom.Spawn(player.transform.position + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f)));
            Banner("PHANTOM DROPSHIP INBOUND", 3f);
        }
    }

    int Count(Enemy.Kind k) { int n = 0; foreach (var e in Enemy.All) if (e.kind == k) n++; return n; }

    void SpawnNext(float t)
    {
        float hp = 1f + Mathf.Min(t / 240f, 2f);
        var p = Player.Instance.transform.position;

        if (FinalStand)
        {
            // Sangheili swarm: Zealots and Generals only
            Spawner.Spawn(Random.value < 0.7f ? Enemy.Kind.Zealot : Enemy.Kind.General, Enemy.Rank.Ultra, PickSpawnPoint(p), hp);
            return;
        }
        if (t > 150f && Count(Enemy.Kind.Banshee) < (t > 240f ? 2 : 1) && Random.value < 0.1f)
        {
            var a = Random.value * Mathf.PI * 2f;
            Spawner.Spawn(Enemy.Kind.Banshee, Enemy.Rank.Major, p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 60f + Vector3.up * 14f, hp);
            Banner("BANSHEES INBOUND", 2.5f); return;
        }
        if (t > 180f && Count(Enemy.Kind.Wraith) < (t > 280f ? 2 : 1) && Random.value < 0.08f)
        {
            Spawner.Spawn(Enemy.Kind.Wraith, Enemy.Rank.Major, PickSpawnPoint(p), hp);
            Banner("WRAITH INBOUND", 2.5f); return;
        }

        var picks = new List<(Enemy.Kind k, Enemy.Rank r, float w)>();
        picks.Add((Enemy.Kind.Grunt, Enemy.Rank.Minor, Mathf.Max(1f, 6f - t / 30f)));
        picks.Add((Enemy.Kind.Elite, Enemy.Rank.Minor, 3f));
        if (t > 40f) { picks.Add((Enemy.Kind.Grunt, Enemy.Rank.Major, 3f)); picks.Add((Enemy.Kind.Elite, Enemy.Rank.Major, 3f)); }
        if (t > 90f) { picks.Add((Enemy.Kind.Grunt, Enemy.Rank.Ultra, 2f)); picks.Add((Enemy.Kind.Elite, Enemy.Rank.Ultra, 2f)); picks.Add((Enemy.Kind.Ranger, Enemy.Rank.Major, 2f)); }
        // After ~3 minutes Elite Generals with concussion rifles become much more common
        if (t > 180f) picks.Add((Enemy.Kind.General, Enemy.Rank.Ultra, Mathf.Lerp(2f, 9f, (t - 180f) / 180f)));
        if (t > 240f) picks.Add((Enemy.Kind.Zealot, Enemy.Rank.Ultra, 2f + (t - 240f) / 30f));

        float total = 0f; foreach (var pk in picks) total += pk.w;
        float roll = Random.value * total;
        foreach (var pk in picks)
        {
            roll -= pk.w;
            if (roll <= 0f) { Spawner.Spawn(pk.k, pk.r, PickSpawnPoint(p), hp); return; }
        }
    }

    // A Phantom unloads Sangheili right on top of you
    public static void SpawnDropTrooper(Vector3 pos)
    {
        if (!inst) return;
        float t = Time.time - inst.startTime;
        var kind = t > 240f && Random.value < 0.4f ? Enemy.Kind.Zealot : Random.value < 0.25f ? Enemy.Kind.General : Enemy.Kind.Elite;
        Spawner.Spawn(kind, t > 90f ? Enemy.Rank.Ultra : Enemy.Rank.Major, pos, 1f + Mathf.Min(t / 240f, 2f));
        Sfx.Play3D("bounce", pos, 0.8f);
    }

    // Spawn far from the player, so reinforcements have to come to you
    Vector3 PickSpawnPoint(Vector3 playerPos)
    {
        Vector3 best = spawnPoints[0]; float bestD = -1f;
        for (int i = 0; i < 3; i++)
        {
            var c = spawnPoints[Random.Range(0, spawnPoints.Count)];
            float d = (c - playerPos).sqrMagnitude;
            if (d > bestD) { bestD = d; best = c; }
        }
        return best + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
    }

    void BeginFinalStand()
    {
        FinalStand = true; finalStart = Time.time;
        Banner("THE LAST STAND", 6f);
        Player.Instance.StripLoadout();
        helmetOff = Time.time;      // Noble Six pulls off the helmet: visor and motion tracker go dark
        Sfx.Play2D("wave");
        var p = Player.Instance.transform.position;
        Phantom.Spawn(p + new Vector3(8f, 0f, 8f)); Phantom.Spawn(p + new Vector3(-8f, 0f, -8f));
    }

    void Banner(string text, float seconds) { banner = text; bannerUntil = Time.time + seconds; }

    // ---------- Kills and drops ----------

    public static void OnEnemyKilled(Enemy e)
    {
        var pos = e.transform.position;
        if (inst) inst.kills++;
        SpawnExplosion(pos + Vector3.up * (e.IsFlying ? 0f : 1f), e.kind == Enemy.Kind.Wraith ? 1.2f : 0.6f);
        SpawnDebris(pos + Vector3.up, e.kind == Enemy.Kind.Wraith ? 30 : 12);
        Sfx.Play3D("edie", pos);
        if (Random.value > e.dropChance) return;

        // Scavenge: fallen Covenant leave behind weapons and health
        if (e.kind == Enemy.Kind.Zealot && Random.value < 0.5f) SpawnWeaponPickup(Weapons.Sword, 0, 0, pos);
        else if (e.kind == Enemy.Kind.General && Random.value < 0.5f) SpawnWeaponPickup(Weapons.Concussion, 4, 8, pos);
        else if (Random.value < 0.35f) SpawnPickup(pos, Pickup.Kind.Health);
        else { var d = Weapons.Scavenge[Random.Range(0, Weapons.Scavenge.Length)]; SpawnWeaponPickup(d, d.mag / 2, d.reserve / 3, pos); }
    }

    // ---------- Death: the helmet, the fade, the epilogue ----------

    public static void OnPlayerDeath(Player p, Camera cam) { if (inst) inst.StartEnding(p, cam); }

    void StartEnding(Player p, Camera cam)
    {
        endStart = Time.time;
        Cursor.visible = false;
        Sfx.Play2D("death");
        Vector3 floorPos = p.transform.position;

        // Noble Six's helmet, lying where they fell
        var h = new GameObject("Helmet").transform;
        h.position = floorPos + p.transform.forward * 0.4f + Vector3.up * 0.2f;
        h.rotation = Quaternion.Euler(0f, p.transform.eulerAngles.y + 160f, 20f);
        var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(shell.GetComponent<Collider>());
        shell.transform.SetParent(h, false); shell.transform.localScale = new Vector3(0.38f, 0.42f, 0.42f);
        shell.GetComponent<Renderer>().material.color = new Color(0.45f, 0.5f, 0.4f);
        var visor = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(visor.GetComponent<Collider>());
        visor.transform.SetParent(h, false); visor.transform.localPosition = new Vector3(0, 0.03f, 0.13f); visor.transform.localScale = new Vector3(0.3f, 0.2f, 0.24f);
        var vr = visor.GetComponent<Renderer>(); vr.material.color = new Color(1f, 0.7f, 0.1f);
        vr.material.EnableKeyword("_EMISSION"); vr.material.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.1f) * 1.2f);
        helmet = h;

        // Seven Sangheili close in on the fallen Spartan; two are Zealots
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f + 0.4f;
            Vector3 pos = floorPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 11f;
            var e = Spawner.Spawn(i < 2 ? Enemy.Kind.Zealot : Enemy.Kind.Elite, Enemy.Rank.Ultra, pos, 1f);
            if (e) e.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a)));
        }

        // A towering silhouette waits at the edge of the circle (hidden until the fade begins)
        var m = Spawner.Spawn(Enemy.Kind.General, Enemy.Rank.Ultra, floorPos + p.transform.forward * -14f, 1f);
        if (m)
        {
            m.transform.localScale = Vector3.one * 1.45f; m.enabled = false;
            foreach (var r in m.GetComponentsInChildren<Renderer>()) r.material.color = Color.black;
            m.transform.rotation = Quaternion.LookRotation(floorPos - m.transform.position);
            m.gameObject.SetActive(false);
            marshal = m.transform;
        }

        deathCam = cam.transform;
        deathCam.SetParent(null);
        camStartPos = deathCam.position; camStartRot = deathCam.rotation;
    }

    void UpdateEnding()
    {
        if (endStart < 0f) return;
        float t = Time.time - endStart;
        Sfx.MusicVolume = Mathf.Lerp(0.4f, 0f, t / 4f);

        if (deathCam && helmet)
        {
            // The camera sinks to the ground and settles on the helmet
            Vector3 back = -Vector3.ProjectOnPlane(helmet.forward, Vector3.up).normalized;
            Vector3 target = helmet.position + back * 1.3f + Vector3.up * 0.45f + Vector3.Cross(Vector3.up, back) * 0.5f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 3.5f));
            deathCam.position = Vector3.Lerp(camStartPos, target, k);
            deathCam.rotation = Quaternion.Slerp(camStartRot, Quaternion.LookRotation(helmet.position - target), k);
        }
        if (marshal && !marshal.gameObject.activeSelf && t > 4.5f) marshal.gameObject.SetActive(true);

        // Reach is glassed: the world burns down to a molten orange glow
        float glow = Mathf.Clamp01((t - 7f) / 5f);
        RenderSettings.fogColor = Color.Lerp(new Color(0.42f, 0.3f, 0.34f), new Color(0.75f, 0.2f, 0.05f), glow);
        if (floorMat) { floorMat.EnableKeyword("_EMISSION"); floorMat.SetColor("_EmissionColor", new Color(0.8f, 0.2f, 0.03f) * glow * 0.6f); }

        if (t > 15f && Input.GetKeyDown(KeyCode.Return))
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    static string FormatTime(float s) { int m = (int)(s / 60f); return m.ToString("00") + ":" + ((int)s % 60).ToString("00"); }
}
