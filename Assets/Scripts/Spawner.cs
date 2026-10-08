using UnityEngine;

// Spawns Covenant from prefabs (Assets/Resources/Enemies/<Kind>.prefab, built by Tools > Last Stand > Build Scene)
// and applies per-rank stats and colors. Edit the prefabs to change how an enemy looks.
public static class Spawner
{
    static readonly Color gruntMinor = new Color(0.78f, 0.4f, 0.14f), gruntMajor = new Color(0.68f, 0.2f, 0.14f), gruntUltra = new Color(0.8f, 0.68f, 0.3f);
    static readonly Color eliteMinor = new Color(0.14f, 0.3f, 0.7f), eliteMajor = new Color(0.68f, 0.2f, 0.12f), eliteUltra = new Color(0.72f, 0.74f, 0.78f);
    static readonly Color plasmaBlue = new Color(0.3f, 0.8f, 1f);

    // Don't spawn embedded in a wall, crate or container: search outward for open ground
    public static Vector3 FreeSpot(Vector3 pos, float radius)
    {
        for (int ring = 0; ring < 5; ring++)
            for (int i = 0; i < (ring == 0 ? 1 : 8); i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 c = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring * 2.5f;
                if (!Physics.CheckCapsule(c + Vector3.up * (radius + 0.2f), c + Vector3.up * (radius + 1.4f), radius, ~0, QueryTriggerInteraction.Ignore)) return c;
            }
        return pos;
    }

    // `hpScale` ramps up with survival time: "enemies become significantly tougher the longer the mission runs"
    public static Enemy Spawn(Enemy.Kind kind, Enemy.Rank rank, Vector3 pos, float hpScale)
    {
        var prefab = Resources.Load<GameObject>("Enemies/" + kind);
        if (!prefab) { Debug.LogError("Missing enemy prefab Resources/Enemies/" + kind + " - run Tools > Last Stand > Build Scene"); return null; }
        if (kind != Enemy.Kind.Banshee) pos = FreeSpot(pos, kind == Enemy.Kind.Wraith ? 2.2f : 0.5f);
        var root = Object.Instantiate(prefab, pos + Vector3.up * 0.3f, Quaternion.identity);
        root.name = kind + "_" + rank;
        var e = root.GetComponent<Enemy>();
        e.kind = kind; e.rank = rank;
        int r = (int)rank;

        // Rank color on all tagged armor parts (named *_A, darker variant *_AD)
        if (kind == Enemy.Kind.Grunt || kind == Enemy.Kind.Elite)
        {
            var c = kind == Enemy.Kind.Grunt ? (rank == Enemy.Rank.Minor ? gruntMinor : rank == Enemy.Rank.Major ? gruntMajor : gruntUltra)
                                             : (rank == Enemy.Rank.Minor ? eliteMinor : rank == Enemy.Rank.Major ? eliteMajor : eliteUltra);
            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                if (rend.name.EndsWith("_AD")) rend.material.color = c * 0.65f;
                else if (rend.name.EndsWith("_A")) rend.material.color = c;
            }
        }

        switch (kind)
        {
            case Enemy.Kind.Grunt:
                e.health = new[] { 30f, 45f, 70f }[r]; e.speed = 3.5f; e.preferredRange = 14f; e.fireInterval = 1.5f;
                e.boltDamage = new[] { 6f, 8f, 10f }[r]; e.boltSpeed = 22f; e.boltColor = new Color(0.3f, 1f, 0.4f); e.dropChance = 0.2f;
                break;
            case Enemy.Kind.Elite:
                e.health = 50f; e.shield = new[] { 40f, 70f, 100f }[r]; e.speed = new[] { 4.2f, 4.6f, 5f }[r]; e.preferredRange = 16f;
                e.fireInterval = new[] { 1.3f, 1.1f, 1f }[r]; e.boltDamage = new[] { 9f, 11f, 14f }[r]; e.boltSpeed = 26f; e.boltColor = plasmaBlue; e.dropChance = 0.35f;
                break;
            case Enemy.Kind.Ranger:
                e.health = 50f; e.shield = 70f; e.speed = 6f; e.preferredRange = 22f; e.fireInterval = 1f; e.boltDamage = 10f; e.boltSpeed = 28f; e.boltColor = plasmaBlue; e.dropChance = 0.35f;
                break;
            case Enemy.Kind.General:
                e.health = 70f; e.shield = 140f; e.speed = 4.6f; e.preferredRange = 22f; e.fireInterval = 2.2f; e.boltDamage = 28f; e.dropChance = 0.5f;
                break;
            case Enemy.Kind.Zealot:
                e.health = 80f; e.shield = 200f; e.speed = 7.2f; e.boltDamage = 38f; e.dropChance = 0.6f;
                break;
            case Enemy.Kind.Wraith:
                e.health = 650f; e.speed = 2.2f; e.preferredRange = 32f; e.fireInterval = 3.2f; e.boltDamage = 65f; e.dropChance = 0f;
                break;
            case Enemy.Kind.Banshee:
                e.health = 160f; e.speed = 14f; e.fireInterval = 0.3f; e.boltDamage = 6f; e.boltSpeed = 30f; e.boltColor = new Color(0.8f, 0.4f, 1f); e.dropChance = 0f;
                break;
        }
        e.health *= hpScale; e.shield *= hpScale;
        return e;
    }
}
