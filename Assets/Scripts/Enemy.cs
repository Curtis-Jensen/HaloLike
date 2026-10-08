using System.Collections.Generic;
using UnityEngine;

// Covenant forces. One behavior script, different kinds: Unggoy (Grunt), Sangheili (Elite / Ranger / General / Zealot),
// Wraith (mortar tank) and Banshee (flier). Models and stats are built by Spawner.
public class Enemy : MonoBehaviour
{
    public enum Kind { Grunt, Elite, Ranger, General, Zealot, Wraith, Banshee }
    public enum Rank { Minor, Major, Ultra }

    public static readonly List<Enemy> All = new List<Enemy>();

    public Kind kind;
    public Rank rank;
    public float health = 40f;
    public float shield = 0f;          // Sangheili energy shield; absorbs damage first
    public float speed = 3.5f;
    public float preferredRange = 14f;
    public float fireInterval = 1.4f;
    public float boltDamage = 9f;
    public float boltSpeed = 22f;
    public Color boltColor = new Color(0.3f, 1f, 0.4f);
    public float dropChance = 0.25f;

    CharacterController cc;           // ground units move through the world with real collision (walls, containers, stairs)
    float yVel, nextStuckCheck, detourUntil, detourDir = 1f;
    Vector3 prevPos;
    float nextFire, strafeDir = 1f, nextStrafeFlip, orbitAngle, flashUntil, nextSense, lastSeen;
    Vector3 lastKnown;
    // The warehouse stairway: ground troops climb it to reach a player on the roof
    static readonly Vector3 stairFoot = new Vector3(28f, 0f, 10f);
    int burst;
    Renderer[] renderers;
    Color[] baseColors;
    bool dead;

    public bool IsFlying { get { return kind == Kind.Banshee; } }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].material.color;
        nextFire = Time.time + Random.Range(0.5f, fireInterval);
        orbitAngle = Random.value * 6.28f;
        cc = GetComponent<CharacterController>();
        prevPos = transform.position;
        lastSeen = Time.time + 25f;   // fresh arrivals know where you are for a while, then must actually see you
        if (Player.Instance) lastKnown = Player.Instance.transform.position;
    }

    void Update()
    {
        var p = Player.Instance;
        if (!p) return;
        if (flashUntil > 0f && Time.time > flashUntil) { RestoreColors(); flashUntil = 0f; }
        if (p.IsDead) { Converge(p); return; }
        if (kind == Kind.Banshee) { FlyUpdate(p); return; }

        // Perception: Sangheili only chase what they can see. Hide where they can't and they lose track of you
        // (during the final stand nothing escapes their notice).
        bool grace = Time.time < lastSeen;
        if (Time.time >= nextSense)
        {
            nextSense = Time.time + 0.25f;
            if (HasLineOfSight(p)) { lastSeen = Mathf.Max(lastSeen, Time.time); lastKnown = p.transform.position; }
        }
        if (grace) lastKnown = p.transform.position;
        bool aware = GameBootstrap.FinalStand || Time.time - lastSeen < 3f || grace;
        Vector3 goal = aware ? p.transform.position : lastKnown;

        // Player on the roof: ground troops path to the stairs first
        Vector3 pathGoal = goal;
        if (aware && goal.y > 5f && transform.position.y < 4f && kind != Kind.Wraith)
        {
            bool onStairs = transform.position.x > 15f && Mathf.Abs(transform.position.z - 10f) < 3f;
            pathGoal = onStairs ? new Vector3(10f, 7f, 10f) : stairFoot;
        }

        Vector3 to = pathGoal - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
        Vector3 toPlayer = p.transform.position - transform.position;
        float playerDist = toPlayer.magnitude;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aware ? dir : (dist > 0.5f ? dir : transform.forward)), Time.deltaTime * 6f);

        if (Time.time > nextStrafeFlip) { strafeDir = Random.value < 0.5f ? -1f : 1f; nextStrafeFlip = Time.time + Random.Range(1f, 2.5f); }

        Vector3 move = Vector3.zero;
        if (!aware)
        {
            if (dist > 2.5f) move = dir * 0.8f;                    // searching the last place they saw you
        }
        else if (kind == Kind.Zealot)
        {
            // Zealots rush you with an energy sword
            move = dir;
            if (playerDist < 2.6f && Mathf.Abs(toPlayer.y) < 2.5f && Time.time >= nextFire && HasLineOfSight(p))
            {
                nextFire = Time.time + 0.9f;
                Sfx.Play3D("sword", transform.position, 0.8f);
                p.TakeDamage(boltDamage);
            }
        }
        else if (pathGoal != goal) move = dir;
        else
        {
            if (dist > preferredRange) move += dir;
            else if (dist < preferredRange * 0.5f) move -= dir;
            move += Vector3.Cross(Vector3.up, dir) * strafeDir * (kind == Kind.Wraith ? 0.2f : 0.6f);
        }

        // Collision stops us at walls; if we're making no headway, sidestep for a moment
        bool wantMove = move.sqrMagnitude > 0.001f;
        if (Time.time >= nextStuckCheck)
        {
            nextStuckCheck = Time.time + 0.4f;
            float moved = (transform.position - prevPos).magnitude; prevPos = transform.position;
            if (wantMove && moved < speed * 0.1f) { detourUntil = Time.time + 1.2f; detourDir = Random.value < 0.5f ? -1f : 1f; }
        }
        if (wantMove && Time.time < detourUntil) move = Vector3.Cross(Vector3.up, dir) * detourDir + dir * 0.25f;
        Step(wantMove ? move.normalized * speed : Vector3.zero);

        if (aware && kind != Kind.Zealot && Time.time >= nextFire && playerDist < (kind == Kind.Wraith ? 75f : 45f))
        {
            if (kind == Kind.Wraith || HasLineOfSight(p)) Shoot(p, playerDist);
        }
    }

    // After Noble Six falls the Sangheili close in around the body
    void Converge(Player p)
    {
        if (kind == Kind.Banshee || kind == Kind.Wraith) return;
        Vector3 to = p.transform.position - transform.position; to.y = 0f;
        if (to.magnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 4f);
        Step(to.magnitude > 2.8f ? to.normalized * speed * 0.7f : Vector3.zero);
    }

    void Shoot(Player p, float dist)
    {
        nextFire = Time.time + fireInterval * Random.Range(0.8f, 1.2f);
        Vector3 origin = transform.position + Vector3.up * (kind == Kind.Wraith ? 2.5f : 1.2f);
        Vector3 target = p.transform.position + Vector3.up * 0.9f;
        switch (kind)
        {
            case Kind.General:
                Sfx.Play3D("concussion", origin, 0.9f);
                GameBootstrap.SpawnConcussion(origin, (target - origin).normalized * 30f, boltDamage);
                break;
            case Kind.Wraith:
                Sfx.Play3D("mortar", origin, 1f);
                GameBootstrap.SpawnMortar(origin, MortarVelocity(origin, p.transform.position + p.transform.forward * 0.5f, 1.9f), boltDamage);
                break;
            default:
                Sfx.Play3D("bolt", origin, 0.7f);
                GameBootstrap.SpawnBolt(origin, (target - origin).normalized * boltSpeed, boltDamage, boltColor);
                break;
        }
    }

    // Launch velocity that lands a ballistic shell on `target` after `flight` seconds (matches Projectile's gravity)
    static Vector3 MortarVelocity(Vector3 from, Vector3 target, float flight)
    {
        Vector3 d = target - from;
        return new Vector3(d.x / flight, d.y / flight - 0.5f * Projectile.Gravity * flight, d.z / flight);
    }

    // Banshee: circles the player at altitude and makes strafing runs in short bursts
    void FlyUpdate(Player p)
    {
        orbitAngle += Time.deltaTime * 0.35f;
        float radius = 24f + Mathf.Sin(Time.time * 0.4f) * 6f;
        Vector3 goal = p.transform.position + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * radius;
        goal.y = 11f + Mathf.Sin(Time.time * 0.7f) * 3f;
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
        Vector3 vel = transform.position - before;
        Vector3 look = p.transform.position - transform.position;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 3f);

        if (Time.time >= nextFire && look.magnitude < 60f)
        {
            burst++;
            nextFire = Time.time + (burst % 6 == 0 ? 2.5f : 0.3f);
            Vector3 origin = transform.position + transform.forward * 1.5f;
            Sfx.Play3D("bolt", origin, 0.6f);
            GameBootstrap.SpawnBolt(origin, (p.transform.position + Vector3.up - origin).normalized * boltSpeed, boltDamage, boltColor);
        }
    }

    // Move with gravity and collision (CharacterController also climbs stairs via its step offset)
    void Step(Vector3 horizontal)
    {
        if (cc && cc.enabled)
        {
            if (cc.isGrounded && yVel < 0f) yVel = -2f;
            yVel -= 22f * Time.deltaTime;
            cc.Move((horizontal + Vector3.up * yVel) * Time.deltaTime);
        }
        else transform.position += horizontal * Time.deltaTime;
    }

    bool HasLineOfSight(Player p)
    {
        Vector3 o = transform.position + Vector3.up * (kind == Kind.Wraith ? 2.5f : 1.6f);
        Vector3 t = p.EyePos;
        var hits = Physics.RaycastAll(o, (t - o).normalized, 70f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; Collider first = null;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<Enemy>()) continue;
            if (h.distance < best) { best = h.distance; first = h.collider; }
        }
        return first && first.GetComponentInParent<Player>() != null;
    }

    void SetColor(Color c) { foreach (var r in renderers) r.material.color = c; }
    void RestoreColors() { for (int i = 0; i < renderers.Length; i++) renderers[i].material.color = baseColors[i]; }

    public void TakeDamage(float amount)
    {
        if (dead) return;
        float absorbed = Mathf.Min(shield, amount);
        shield -= absorbed;
        health -= amount - absorbed;
        // Shield hits flash cyan, health hits flash white
        SetColor(absorbed > 0f ? new Color(0.6f, 1.5f, 2f) : new Color(2f, 2f, 2f)); flashUntil = Time.time + 0.06f;
        if (health <= 0f)
        {
            dead = true;
            GameBootstrap.OnEnemyKilled(this);
            Destroy(gameObject);
        }
    }
}
