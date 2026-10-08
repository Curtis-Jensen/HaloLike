using System.Collections.Generic;
using UnityEngine;

// Plasma bolt, frag grenade (player), concussion blast (General) or Wraith mortar shell.
public class Projectile : MonoBehaviour
{
    public enum Kind { Bolt, Grenade, Concussion, Mortar, PlayerConcussion }
    public const float Gravity = -20f;

    public Kind kind;
    public float damage = 9f;
    public Vector3 velocity;
    float life;
    float fuse = 2.2f;

    void Update()
    {
        life += Time.deltaTime;
        if (kind == Kind.Grenade) { UpdateGrenade(); return; }

        if (kind == Kind.Mortar) velocity += Vector3.up * Gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        if (Physics.SphereCast(transform.position, 0.15f, step.normalized, out RaycastHit hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (kind == Kind.Bolt)
            {
                var p = hit.collider.GetComponentInParent<Player>();
                if (p) p.TakeDamage(damage);
                Destroy(gameObject);
            }
            else if (kind == Kind.PlayerConcussion) DetonatePlayerShell(hit.point);
            else Detonate(hit.point);
            return;
        }
        transform.position += step;
        if (life > 8f) Destroy(gameObject);
    }

    void UpdateGrenade()
    {
        velocity += Vector3.up * Gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        if (Physics.SphereCast(transform.position, 0.15f, step.normalized, out RaycastHit hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (velocity.sqrMagnitude > 9f) Sfx.Play3D("bounce", transform.position, 0.6f);
            velocity = Vector3.Reflect(velocity, hit.normal) * 0.45f;
            transform.position = hit.point + hit.normal * 0.16f;
        }
        else transform.position += step;
        if (life >= fuse) { Explode(); }
    }

    // Enemy shells: hurt (and push) only the player
    void Detonate(Vector3 point)
    {
        bool concussion = kind == Kind.Concussion;
        float radius = concussion ? 5f : 7f;
        var player = Player.Instance;
        if (player && !player.IsDead)
        {
            Vector3 away = player.transform.position + Vector3.up - point;
            float d = away.magnitude;
            if (d < radius)
            {
                float falloff = 1f - d / radius;
                player.TakeDamage(damage * Mathf.Clamp01(falloff + 0.15f));
                if (concussion) player.AddImpulse(away.normalized * 16f * falloff + Vector3.up * 5f);
            }
        }
        GameBootstrap.SpawnExplosion(point, concussion ? 0.5f : 0.8f);
        Destroy(gameObject);
    }

    // Each enemy is hurt once per blast, however many colliders it is made of
    static void DamageEnemiesInRadius(Vector3 point, float radius, float maxDamage)
    {
        var seen = new HashSet<Enemy>();
        foreach (var c in Physics.OverlapSphere(point, radius))
        {
            var e = c.GetComponentInParent<Enemy>();
            if (!e || !seen.Add(e)) continue;
            float falloff = 1f - Vector3.Distance(e.transform.position + Vector3.up, point) / radius;
            e.TakeDamage(maxDamage * Mathf.Clamp01(falloff + 0.2f));
        }
    }

    // Player concussion shell: area damage to Covenant, and the blast shoves Noble Six too
    void DetonatePlayerShell(Vector3 point)
    {
        const float radius = 5f;
        DamageEnemiesInRadius(point, radius, damage);
        var player = Player.Instance;
        if (player)
        {
            Vector3 away = player.transform.position + Vector3.up - point;
            if (away.magnitude < radius) player.AddImpulse(away.normalized * 14f * (1f - away.magnitude / radius) + Vector3.up * 4f);
        }
        GameBootstrap.SpawnExplosion(point, 0.5f);
        Destroy(gameObject);
    }

    // Player frag grenade: hurts everything nearby, including Noble Six
    void Explode()
    {
        const float radius = 7f;
        DamageEnemiesInRadius(transform.position, radius, 120f);
        var player = Player.Instance;
        if (player)
        {
            float d = Vector3.Distance(player.transform.position, transform.position);
            if (d < radius) player.TakeDamage(60f * (1f - d / radius));
        }
        GameBootstrap.SpawnExplosion(transform.position);
        Destroy(gameObject);
    }
}
