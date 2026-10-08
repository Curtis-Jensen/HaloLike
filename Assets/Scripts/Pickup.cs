using System.Collections.Generic;
using UnityEngine;

// Loot on the ground: health packs (walk over them), weapons and equipment (press E). Dropped loot expires; placed loot doesn't.
// Weapon pickups are prefabs (Resources/Pickups) so they can be placed and tweaked in the scene.
public class Pickup : MonoBehaviour
{
    public enum Kind { Health, Weapon, DropShield }
    public static readonly List<Pickup> All = new List<Pickup>();

    public Kind kind;
    public string weaponId;                 // for Kind.Weapon: "dmr", "ar", "shotgun", ...
    public int ammo = -1, reserve = -1;     // -1 = the weapon's default
    public bool permanent;
    [HideInInspector] public WeaponDef weapon;
    const float Lifetime = 40f;
    float spawnTime, baseY;

    void Awake()
    {
        if (weapon == null && !string.IsNullOrEmpty(weaponId)) weapon = Weapons.ById(weaponId);
        if (weapon != null)
        {
            if (ammo < 0) ammo = weapon.mag;
            if (reserve < 0) reserve = weapon.reserve;
        }
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    void Start() { spawnTime = Time.time; baseY = transform.position.y; }

    void Update()
    {
        transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        var pos = transform.position; pos.y = baseY + Mathf.Sin(Time.time * 3f) * 0.08f; transform.position = pos;
        if (!permanent && Time.time - spawnTime > Lifetime) { Destroy(gameObject); return; }

        if (kind != Kind.Health) return;
        // Distance check instead of trigger events: the player is a CharacterController and the pickup has no Rigidbody
        var p = Player.Instance;
        if (!p || p.IsDead) return;
        if ((p.transform.position + Vector3.up - transform.position).sqrMagnitude > 1.6f * 1.6f) return;
        if (p.Health >= p.maxHealth) return;
        p.Heal(30f);
        Sfx.Play2D("pickup");
        Destroy(gameObject);
    }
}
