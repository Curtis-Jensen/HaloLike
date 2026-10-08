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
    float helmetOff = -1f, nextBob;
    bool bobAlive; Enemy bobRef;
    float startTime, nextSpawn, nextPhantom, finalStart, endStart = -1f;
    string banner = ""; float bannerUntil;
    readonly List<Vector3> spawnPoints = new List<Vector3>();
    Texture2D white, circleTex;

    void Awake()
    {
        inst = this;
        FinalStand = false;
        // Lots of small colored lights and soft shadows: make sure the quality tier doesn't throw them away
        QualitySettings.pixelLightCount = 8; QualitySettings.shadowDistance = 80f; QualitySettings.shadowCascades = 2;
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

        // "BOB": an Elite with an energy sword and far more aggressive AI, one at a time from 2 minutes on
        if (!FinalStand && t > 120f && Time.time >= nextBob && !bobAlive)
        {
            nextBob = Time.time + 60f;
            var bob = Spawner.Spawn(Enemy.Kind.Zealot, Enemy.Rank.Ultra, PickSpawnPoint(player.transform.position), 1f + Mathf.Min(t / 240f, 2f));
            if (bob)
            {
                bob.name = "BOB"; bob.speed *= 1.15f; bob.shield *= 1.4f; bob.dropChance = 1f;
                foreach (var r in bob.GetComponentsInChildren<Renderer>()) if (r.name == "Body") r.material.color = new Color(0.12f, 0.12f, 0.14f);
                bobAlive = true; bobRef = bob;
            }
        }
        if (bobAlive && !bobRef) bobAlive = false;

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
        if (!inst || (Player.Instance && Player.Instance.IsDead)) return;
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
        else if ((e.kind == Enemy.Kind.Elite || e.kind == Enemy.Kind.Ranger) && Random.value < 0.5f)
        {
            var pw = Random.value < 0.5f ? Weapons.PlasmaRifle : Weapons.PlasmaRepeater;   // Sangheili carry plasma weapons
            SpawnWeaponPickup(pw, pw.mag / 2, pw.reserve / 3, pos);
        }
        else if (Random.value < 0.35f) SpawnPickup(pos, Pickup.Kind.Health);
        else { var d = Weapons.Scavenge[Random.Range(0, Weapons.Scavenge.Length)]; SpawnWeaponPickup(d, d.mag / 2, d.reserve / 3, pos); }
    }

    // ---------- Death: see GameBootstrap.Ending.cs ----------

    public static void OnPlayerDeath(Player p, Camera cam) { if (inst) inst.StartEnding(p, cam); }

    static string FormatTime(float s) { int m = (int)(s / 60f); return m.ToString("00") + ":" + ((int)s % 60).ToString("00"); }
}
