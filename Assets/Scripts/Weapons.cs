using UnityEngine;

// Data for every weapon Noble Six can carry. Two slots, scavenged from the yard and from fallen Covenant.
public class WeaponDef
{
    public string id, name, sfx;                 // id also names the model prefab in Resources/Weapons
    public int mag, reserve;                 // reserve = rounds carried beyond the loaded magazine
    public float interval, damage, spread, reload, zoom = 1f, kick = 0.06f, range = 220f;
    public int pellets = 1;
    public bool auto, melee;                 // melee weapons (energy sword) have no ammo
    public float meleeRange = 4.5f;
    public bool projectile; public float projectileSpeed = 45f;   // fires a physical shell instead of a hitscan ray
    public float moveMul = 1f;                                      // heavy weapons slow you down
    public Color tracer = new Color(1f, 0.9f, 0.4f);
    public Color tint = new Color(0.4f, 0.45f, 0.4f);   // color of the pickup model
}

public static class Weapons
{
    public static readonly WeaponDef Dmr = new WeaponDef
    {
        id = "dmr", name = "M392 DMR", sfx = "dmr", mag = 15, reserve = 60, interval = 0.2f, damage = 28f, spread = 0.002f,
        reload = 1.9f, zoom = 2.2f, kick = 0.09f, tint = new Color(0.35f, 0.4f, 0.3f)
    };
    public static readonly WeaponDef Pistol = new WeaponDef
    {
        id = "pistol", name = "M6G MAGNUM", sfx = "pistol", mag = 8, reserve = 32, interval = 0.28f, damage = 32f, spread = 0.003f,
        reload = 1.5f, zoom = 1.6f, kick = 0.1f, tint = new Color(0.5f, 0.5f, 0.55f)
    };
    public static readonly WeaponDef AssaultRifle = new WeaponDef
    {
        id = "ar", name = "MA37 ASSAULT RIFLE", sfx = "rifle", mag = 32, reserve = 96, interval = 1f / 11f, damage = 8f, spread = 0.014f,
        reload = 1.8f, auto = true, tint = new Color(0.3f, 0.35f, 0.25f)
    };
    public static readonly WeaponDef Shotgun = new WeaponDef
    {
        id = "shotgun", name = "M45 SHOTGUN", sfx = "shotgun", mag = 6, reserve = 18, interval = 0.85f, damage = 11f, pellets = 9, spread = 0.06f,
        reload = 2.4f, kick = 0.22f, range = 45f, tracer = new Color(1f, 0.7f, 0.3f), tint = new Color(0.45f, 0.3f, 0.2f)
    };
    public static readonly WeaponDef Sniper = new WeaponDef
    {
        id = "sniper", name = "S99 SNIPER RIFLE", sfx = "sniper", mag = 4, reserve = 12, interval = 1.2f, damage = 140f, spread = 0f,
        reload = 2.8f, zoom = 5f, kick = 0.2f, tint = new Color(0.25f, 0.3f, 0.35f)
    };
    public static readonly WeaponDef Sword = new WeaponDef
    {
        id = "sword", name = "TYPE-1 ENERGY SWORD", sfx = "sword", melee = true, interval = 0.7f, damage = 220f, meleeRange = 4.5f,
        tint = new Color(0.2f, 0.8f, 1f)
    };

    public static readonly WeaponDef Concussion = new WeaponDef
    {
        id = "concussion", name = "T50 CONCUSSION RIFLE", sfx = "concussion", mag = 6, reserve = 18, interval = 0.8f, damage = 70f, reload = 2.2f,
        projectile = true, projectileSpeed = 48f, kick = 0.15f, tint = new Color(0.4f, 0.3f, 0.8f)
    };
    // Covenant plasma weapons: scavenged from fallen Sangheili
    public static readonly WeaponDef PlasmaRifle = new WeaponDef
    {
        id = "plasma", name = "TYPE-25 PLASMA RIFLE", sfx = "bolt", mag = 100, reserve = 100, interval = 1f / 9f, damage = 11f, spread = 0.015f,
        reload = 2.2f, auto = true, tracer = new Color(0.3f, 0.8f, 1f), tint = new Color(0.3f, 0.5f, 0.9f)
    };
    public static readonly WeaponDef PlasmaRepeater = new WeaponDef
    {
        id = "repeater", name = "TYPE-25 PLASMA REPEATER", sfx = "bolt", mag = 120, reserve = 120, interval = 1f / 15f, damage = 6.5f, spread = 0.03f,
        reload = 2.4f, auto = true, tracer = new Color(0.4f, 1f, 0.5f), tint = new Color(0.3f, 0.8f, 0.4f)
    };
    // The yard's detachable machine-gun turrets: carried, heavy, brutal
    public static readonly WeaponDef Turret = new WeaponDef
    {
        id = "turret", name = "M247 TURRET", sfx = "rifle", mag = 100, reserve = 200, interval = 0.06f, damage = 9f, spread = 0.03f, reload = 4.5f,
        auto = true, moveMul = 0.65f, kick = 0.03f, tint = new Color(0.35f, 0.35f, 0.38f)
    };

    public static readonly WeaponDef[] All = { Dmr, Pistol, AssaultRifle, Shotgun, Sniper, Sword, Concussion, Turret, PlasmaRifle, PlasmaRepeater };

    public static WeaponDef ById(string id)
    {
        foreach (var d in All) if (d.id == id) return d;
        return null;
    }

    // What fallen Covenant can leave behind (they carry human weapons they pick up... and the occasional sword)
    public static readonly WeaponDef[] Scavenge = { AssaultRifle, AssaultRifle, Shotgun, Pistol, Dmr, Sniper, Concussion };
}
