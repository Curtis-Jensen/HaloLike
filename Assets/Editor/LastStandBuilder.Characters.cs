using UnityEngine;

// Covenant character models built from primitives: proper limbs, armor plates, mandibles, held weapons.
public static partial class LastStandBuilder
{
    static Material Armor(Color c, float glow = 0f) { return Mat(c, glow, null, default(Vector2), 0.5f, "Standard", 0.2f); }
    static Material Suit(Color c) { return Mat(c, 0f, null, default(Vector2), 0.15f, "Standard", 0f); }

    // Unggoy: squat, hunched, methane tank, breather mask, plasma pistol
    static void BuildGrunt(Transform t)
    {
        var skin = Suit(new Color(0.26f, 0.2f, 0.32f)); var body = Armor(new Color(0.9f, 0.45f, 0.1f));
        var dark = Suit(new Color(0.08f, 0.08f, 0.1f));
        foreach (float s in new[] { -1f, 1f })
        {
            Limb(t, PrimitiveType.Capsule, "Leg", new Vector3(s * 0.17f, 0.7f, 0f), new Vector3(s * 0.2f, 0.12f, 0.08f), 0.2f, skin);
            Prim(PrimitiveType.Cube, t, "Foot", new Vector3(s * 0.2f, 0.07f, 0.15f), new Vector3(0.22f, 0.13f, 0.38f), dark);
            Limb(t, PrimitiveType.Capsule, "Arm", new Vector3(s * 0.34f, 1.25f, 0.0f), new Vector3(s * 0.2f, 0.98f, 0.42f), 0.14f, skin);
        }
        var bodyGo = Prim(PrimitiveType.Capsule, t, "Body", new Vector3(0, 1.0f, 0), new Vector3(0.62f, 0.46f, 0.55f), body);
        Prim(PrimitiveType.Cube, t, "ChestPlate", new Vector3(0, 1.1f, 0.2f), new Vector3(0.45f, 0.32f, 0.12f), Armor(new Color(0.55f, 0.25f, 0.06f)));
        Prim(PrimitiveType.Sphere, t, "Head", new Vector3(0, 1.6f, 0.1f), new Vector3(0.42f, 0.4f, 0.44f), Suit(new Color(0.55f, 0.4f, 0.28f)));
        Prim(PrimitiveType.Cube, t, "Mask", new Vector3(0, 1.55f, 0.3f), new Vector3(0.3f, 0.22f, 0.1f), dark);
        Prim(PrimitiveType.Cylinder, t, "Hose", new Vector3(0, 1.43f, 0.34f), new Vector3(0.05f, 0.1f, 0.05f), dark, false, new Vector3(90f, 0, 0));
        foreach (float s in new[] { -1f, 1f }) Prim(PrimitiveType.Sphere, t, "Eye", new Vector3(s * 0.09f, 1.62f, 0.32f), Vector3.one * 0.07f, Mat(new Color(1f, 0.8f, 0.2f), 3f));
        // methane tanks on the back
        Prim(PrimitiveType.Cylinder, t, "TankL", new Vector3(-0.14f, 1.25f, -0.42f), new Vector3(0.2f, 0.34f, 0.2f), Armor(new Color(0.2f, 0.5f, 0.5f)));
        Prim(PrimitiveType.Cylinder, t, "TankR", new Vector3(0.14f, 1.25f, -0.42f), new Vector3(0.2f, 0.34f, 0.2f), Armor(new Color(0.2f, 0.5f, 0.5f)));
        Prim(PrimitiveType.Cylinder, t, "Strap", new Vector3(0, 1.15f, -0.1f), new Vector3(0.64f, 0.03f, 0.58f), dark);
        // plasma pistol held forward
        Prim(PrimitiveType.Cube, t, "Pistol", new Vector3(0.2f, 0.98f, 0.55f), new Vector3(0.1f, 0.14f, 0.34f), Armor(new Color(0.2f, 0.45f, 0.25f)));
        Prim(PrimitiveType.Sphere, t, "PistolGlow", new Vector3(0.2f, 0.99f, 0.75f), Vector3.one * 0.1f, Mat(new Color(0.3f, 1f, 0.4f), 3f));
        TagArmor(t, new[] { "Body" }, new[] { "ChestPlate" });
        LeanUpperBody(t, new Vector3(0f, 0.75f, 0f), 16f, new[] { "Leg", "Foot" });
    }

    static Material ArmorT(Color col) { return Mat(col, 0f, null, default(Vector2), 0.55f, "Standard", 0.25f); }   // glossy painted armor; Spawner tints parts named *_A / *_AD per rank

    // Sangheili: tall digitigrade warriors in segmented armor, two-handed plasma rifle (or energy sword)
    static void BuildElite(Transform t, Color bodyColor, bool crest, bool jetpack, bool sword)
    {
        var armor = ArmorT(bodyColor); var armorDark = ArmorT(bodyColor * 0.65f); var dark = Suit(new Color(0.11f, 0.10f, 0.13f)); var skin = Suit(new Color(0.22f, 0.17f, 0.18f));
        // legs: thigh forward, shin back (digitigrade), clawed foot
        foreach (float s in new[] { -1f, 1f })
        {
            var knee = new Vector3(s * 0.23f, 0.84f, 0.27f);
            Limb(t, PrimitiveType.Capsule, "Thigh", new Vector3(s * 0.2f, 1.32f, 0f), knee, 0.27f, armorDark);
            Limb(t, PrimitiveType.Capsule, "Shin", knee, new Vector3(s * 0.24f, 0.2f, -0.15f), 0.21f, dark);
            Prim(PrimitiveType.Cube, t, "Greave", new Vector3(s * 0.24f, 0.56f, 0.06f), new Vector3(0.28f, 0.42f, 0.3f), armorDark, false, new Vector3(-14f, 0, 0));
            Prim(PrimitiveType.Cube, t, "Foot", new Vector3(s * 0.24f, 0.08f, 0.02f), new Vector3(0.26f, 0.14f, 0.56f), dark);
            Prim(PrimitiveType.Cube, t, "Toe", new Vector3(s * 0.24f, 0.05f, 0.34f), new Vector3(0.2f, 0.08f, 0.16f), skin);
            Prim(PrimitiveType.Sphere, t, "KneePlate", new Vector3(s * 0.23f, 0.86f, 0.4f), new Vector3(0.24f, 0.24f, 0.18f), armor);
            Prim(PrimitiveType.Cube, t, "HipPlate", new Vector3(s * 0.36f, 1.28f, 0f), new Vector3(0.12f, 0.36f, 0.38f), armor);
        }
        Prim(PrimitiveType.Cube, t, "Pelvis", new Vector3(0, 1.34f, 0), new Vector3(0.66f, 0.3f, 0.46f), armorDark);
        Prim(PrimitiveType.Cube, t, "Belt", new Vector3(0, 1.28f, 0.25f), new Vector3(0.3f, 0.34f, 0.08f), armor);
        Prim(PrimitiveType.Capsule, t, "Body", new Vector3(0, 1.8f, 0), new Vector3(0.8f, 0.54f, 0.6f), armor);               // rank-tinted chest
        Prim(PrimitiveType.Cube, t, "ChestPlate", new Vector3(0, 1.88f, 0.25f), new Vector3(0.62f, 0.5f, 0.16f), armorDark, false, new Vector3(-6f, 0, 0));
        Prim(PrimitiveType.Cube, t, "Spine", new Vector3(0, 1.86f, -0.27f), new Vector3(0.5f, 0.56f, 0.16f), armorDark);
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Sphere, t, "Pauldron", new Vector3(s * 0.58f, 2.17f, 0f), new Vector3(0.36f, 0.2f, 0.42f), armor, false, new Vector3(0, 0, s * 14f));
            Prim(PrimitiveType.Cube, t, "PauldronCap", new Vector3(s * 0.58f, 2.24f, 0f), new Vector3(0.28f, 0.05f, 0.34f), armorDark, false, new Vector3(0, 0, s * 14f));
            Limb(t, PrimitiveType.Capsule, "UpperArm", new Vector3(s * 0.54f, 2.04f, 0f), new Vector3(s * 0.6f, 1.78f, 0.27f), 0.21f, armorDark);
        }
        // forearms reach forward to hold the weapon
        Limb(t, PrimitiveType.Capsule, "ForearmR", new Vector3(0.6f, 1.78f, 0.27f), new Vector3(0.32f, 1.64f, 0.72f), 0.18f, dark);
        Limb(t, PrimitiveType.Capsule, "ForearmL", new Vector3(-0.6f, 1.78f, 0.27f), new Vector3(0.04f, 1.62f, 0.98f), 0.18f, dark);
        Prim(PrimitiveType.Cube, t, "BracerR", new Vector3(0.46f, 1.7f, 0.5f), new Vector3(0.2f, 0.2f, 0.3f), armor, false, new Vector3(0, -25f, 0));
        Prim(PrimitiveType.Cube, t, "BracerL", new Vector3(-0.3f, 1.68f, 0.64f), new Vector3(0.2f, 0.2f, 0.3f), armor, false, new Vector3(0, 40f, 0));
        // head: long skull, visor, four-part mandibles
        Limb(t, PrimitiveType.Cylinder, "Neck", new Vector3(0, 2.2f, 0.02f), new Vector3(0, 2.44f, 0.1f), 0.28f, skin);
        Prim(PrimitiveType.Sphere, t, "Head", new Vector3(0, 2.6f, 0.16f), new Vector3(0.36f, 0.4f, 0.68f), armorDark);
        Prim(PrimitiveType.Sphere, t, "Visor", new Vector3(0, 2.65f, 0.46f), new Vector3(0.3f, 0.1f, 0.16f), Mat(new Color(0.15f, 0.55f, 1f), 1.1f));
        Prim(PrimitiveType.Cube, t, "BrowRidge", new Vector3(0, 2.72f, 0.3f), new Vector3(0.3f, 0.06f, 0.3f), armor, false, new Vector3(-10f, 0, 0));
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cube, t, "MandibleUpper", new Vector3(s * 0.13f, 2.5f, 0.52f), new Vector3(0.08f, 0.07f, 0.36f), skin, false, new Vector3(0, s * 16f, 0));
            Prim(PrimitiveType.Cube, t, "MandibleLower", new Vector3(s * 0.2f, 2.4f, 0.46f), new Vector3(0.07f, 0.07f, 0.36f), skin, false, new Vector3(10f, s * 28f, 0));
        }
        if (!sword)
        {
            // plasma rifle across the body
            Prim(PrimitiveType.Cube, t, "Rifle", new Vector3(0.18f, 1.62f, 0.82f), new Vector3(0.14f, 0.18f, 0.86f), Armor(new Color(0.2f, 0.22f, 0.3f)));
            Prim(PrimitiveType.Cube, t, "RifleProngL", new Vector3(0.1f, 1.67f, 1.28f), new Vector3(0.03f, 0.07f, 0.34f), Armor(new Color(0.3f, 0.34f, 0.46f)), false, new Vector3(0, 5f, 0));
            Prim(PrimitiveType.Cube, t, "RifleProngR", new Vector3(0.26f, 1.67f, 1.28f), new Vector3(0.03f, 0.07f, 0.34f), Armor(new Color(0.3f, 0.34f, 0.46f)), false, new Vector3(0, -5f, 0));
            Prim(PrimitiveType.Cube, t, "RifleCore", new Vector3(0.18f, 1.67f, 1.22f), new Vector3(0.05f, 0.05f, 0.4f), Mat(new Color(0.3f, 0.8f, 1f), 3f));
        }
        else
        {
            Prim(PrimitiveType.Cube, t, "SwordHilt", new Vector3(0.32f, 1.64f, 0.72f), new Vector3(0.09f, 0.09f, 0.28f), dark);
            Prim(PrimitiveType.Cube, t, "EnergySword", new Vector3(0.32f, 1.64f, 1.46f), new Vector3(0.05f, 0.2f, 1.25f), Mat(new Color(0.4f, 0.85f, 1f), 3.5f));
        }
        if (crest) Prim(PrimitiveType.Cube, t, "Crest", new Vector3(0, 2.8f, -0.05f), new Vector3(0.08f, 0.12f, 0.5f), ArmorT(bodyColor * 0.5f), false, new Vector3(-8f, 0, 0));   // low ridge, not a plume
        if (jetpack)
        {
            Prim(PrimitiveType.Cube, t, "Jetpack", new Vector3(0, 1.9f, -0.42f), new Vector3(0.56f, 0.74f, 0.3f), Armor(new Color(0.15f, 0.15f, 0.18f)));
            foreach (float s in new[] { -1f, 1f }) Prim(PrimitiveType.Sphere, t, "Thruster", new Vector3(s * 0.17f, 1.5f, -0.48f), Vector3.one * 0.2f, Mat(new Color(1f, 0.6f, 0.2f), 3f));
        }
        TagArmor(t, new[] { "Body", "KneePlate", "HipPlate", "Belt", "Pauldron", "BracerR", "BracerL" },
                    new[] { "Thigh", "Greave", "Pelvis", "ChestPlate", "Spine", "PauldronCap", "UpperArm", "Head", "BrowRidge", "Crest" });
        LeanUpperBody(t, new Vector3(0f, 1.3f, 0f), 18f, new[] { "Thigh", "Shin", "Greave", "Foot", "Toe", "KneePlate", "HipPlate", "Pelvis", "Belt" });
    }

    // Names armor renderers "_A" (rank color) / "_AD" (darker rank color) so Spawner can tint a whole suit per rank
    static void TagArmor(Transform t, string[] light, string[] dark)
    {
        foreach (Transform ch in t)
        {
            if (System.Array.IndexOf(light, ch.name) >= 0) ch.name += "_A";
            else if (System.Array.IndexOf(dark, ch.name) >= 0) ch.name += "_AD";
        }
    }

    // Hunched Sangheili posture: everything above the pelvis pivots forward about the hips
    static void LeanUpperBody(Transform t, Vector3 pivotPos, float degrees, string[] lowerNames)
    {
        var pivot = new GameObject("Upper").transform;
        pivot.SetParent(t, false); pivot.localPosition = pivotPos;
        var move = new System.Collections.Generic.List<Transform>();
        foreach (Transform ch in t)
        {
            if (ch == pivot) continue;
            string n = ch.name; int us = n.LastIndexOf('_'); string baseName = us > 0 && (n.EndsWith("_A") || n.EndsWith("_AD")) ? n.Substring(0, us) : n;
            if (System.Array.IndexOf(lowerNames, baseName) < 0) move.Add(ch);
        }
        foreach (var ch in move) ch.SetParent(pivot, true);
        pivot.localRotation = Quaternion.Euler(degrees, 0f, 0f);
    }
}
