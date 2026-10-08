using UnityEngine;

// Static spawn helpers for projectiles, pickups and visual effects (used by Player / Enemy / Projectile).
public partial class GameBootstrap
{
    public static void SpawnTracer(Vector3 from, Vector3 to, Color color)
    {
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to);
        lr.startWidth = lr.endWidth = 0.04f;
        lr.material = new Material(Shader.Find("Sprites/Default")); lr.startColor = lr.endColor = color;
        Destroy(go, 0.05f);
    }

    public static void SpawnSpark(Vector3 pos, Vector3 normal)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.transform.position = pos + normal * 0.02f; s.transform.localScale = Vector3.one * 0.12f;
        Destroy(s.GetComponent<Collider>());
        s.GetComponent<Renderer>().material.color = new Color(1f, 0.8f, 0.3f);
        Destroy(s, 0.08f);
    }

    static Projectile MakeProjectile(Projectile.Kind kind, Vector3 pos, float size, Color color, float glow)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = pos; go.transform.localScale = Vector3.one * size;
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>(); r.material.color = color;
        if (glow > 0f) { r.material.EnableKeyword("_EMISSION"); r.material.SetColor("_EmissionColor", color * glow); }
        var p = go.AddComponent<Projectile>(); p.kind = kind;
        return p;
    }

    public static void SpawnBolt(Vector3 pos, Vector3 velocity, float damage, Color color)
    {
        var p = MakeProjectile(Projectile.Kind.Bolt, pos, 0.35f, color, 2f);
        p.velocity = velocity; p.damage = damage;
    }

    public static void SpawnConcussion(Vector3 pos, Vector3 velocity, float damage)
    {
        var p = MakeProjectile(Projectile.Kind.Concussion, pos, 0.55f, new Color(0.5f, 0.8f, 1f), 3f);
        p.velocity = velocity; p.damage = damage;
    }

    public static void SpawnPlayerConcussion(Vector3 pos, Vector3 velocity, float damage)
    {
        var p = MakeProjectile(Projectile.Kind.PlayerConcussion, pos, 0.4f, new Color(0.6f, 0.5f, 1f), 3f);
        p.velocity = velocity; p.damage = damage;
        Destroy(p.gameObject, 4f);
    }

    public static void SpawnMortar(Vector3 pos, Vector3 velocity, float damage)
    {
        var p = MakeProjectile(Projectile.Kind.Mortar, pos, 0.9f, new Color(0.7f, 0.3f, 1f), 3f);
        p.velocity = velocity; p.damage = damage;
    }

    public static void SpawnGrenade(Vector3 pos, Vector3 velocity)
    {
        var p = MakeProjectile(Projectile.Kind.Grenade, pos, 0.3f, new Color(0.2f, 0.4f, 1f), 0f);
        p.velocity = velocity;
    }

    public static void SpawnExplosion(Vector3 pos, float scale = 1f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = pos; Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>(); r.material.color = new Color(1f, 0.6f, 0.15f);
        r.material.EnableKeyword("_EMISSION"); r.material.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 3f);
        var light = go.AddComponent<Light>(); light.color = new Color(1f, 0.6f, 0.2f); light.range = 14f * scale; light.intensity = 4f;
        go.AddComponent<Explosion>().maxScale = 9f * scale;
        if (scale >= 0.8f) Sfx.Play3D("explosion", pos, Mathf.Min(scale, 1f));
    }

    public static void SpawnDebris(Vector3 pos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos; go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.2f);
            Destroy(go.GetComponent<Collider>());
            var c = Random.value < 0.5f ? new Color(1f, 0.6f, 0.2f) : new Color(0.45f, 0.3f, 0.7f);
            var r = go.GetComponent<Renderer>(); r.material.color = c;
            r.material.EnableKeyword("_EMISSION"); r.material.SetColor("_EmissionColor", c * 1.5f);
            go.AddComponent<Debris>().velocity = Random.onUnitSphere * Random.Range(3f, 9f) + Vector3.up * 3f;
        }
    }

    // Pickups are prefabs (Resources/Pickups) so they look right and can be tweaked in the editor
    public static void SpawnPickup(Vector3 pos, Pickup.Kind kind, bool permanent = false)
    {
        var prefab = Resources.Load<GameObject>("Pickups/" + (kind == Pickup.Kind.DropShield ? "DropShield" : "Health"));
        if (!prefab) return;
        var go = Instantiate(prefab, new Vector3(pos.x, Mathf.Max(pos.y, 0.8f), pos.z), Quaternion.identity);
        var pk = go.GetComponent<Pickup>(); pk.kind = kind; pk.permanent = permanent;
    }

    public static void SpawnWeaponPickup(WeaponDef def, int ammo, int reserve, Vector3 pos, bool permanent = false)
    {
        var prefab = Resources.Load<GameObject>("Pickups/" + def.id);
        if (!prefab) return;
        var go = Instantiate(prefab, new Vector3(pos.x, Mathf.Max(pos.y, 0.9f), pos.z), Quaternion.identity);
        var pk = go.GetComponent<Pickup>(); pk.kind = Pickup.Kind.Weapon; pk.weapon = def; pk.weaponId = def.id; pk.ammo = ammo; pk.reserve = reserve; pk.permanent = permanent;
    }

    class Explosion : MonoBehaviour
    {
        public float maxScale; float t; Light glow; float glowStart;
        void Update()
        {
            if (!glow) { glow = GetComponent<Light>(); if (glow) glowStart = glow.intensity; }
            t += Time.deltaTime / 0.35f;
            if (glow) glow.intensity = glowStart * Mathf.Clamp01(1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, maxScale, t);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    class Debris : MonoBehaviour
    {
        public Vector3 velocity; float life, spin;
        void Start() { spin = Random.Range(200f, 600f); }
        void Update()
        {
            life += Time.deltaTime;
            if (transform.position.y > 0.1f) { velocity += Vector3.down * 20f * Time.deltaTime; transform.position += velocity * Time.deltaTime; transform.Rotate(Vector3.one * spin * Time.deltaTime); }
            transform.localScale *= 1f - Time.deltaTime * 1.2f;
            if (life > 1.2f) Destroy(gameObject);
        }
    }
}
