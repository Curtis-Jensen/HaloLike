using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Enemy models (used only as sources for sprite baking) and the runtime sprite-billboard enemy prefabs.
public static partial class LastStandBuilder
{
    public class EnemySpec
    {
        public Enemy.Kind kind; public float radius, height, scale; public System.Action<Transform> build;
        public float frameW, frameH, ppm; public bool pivotFeet = true, ranked;
    }

    public static List<EnemySpec> EnemySpecs()
    {
        return new List<EnemySpec>
        {
            new EnemySpec { kind = Enemy.Kind.Grunt, radius = 0.42f, height = 1.85f, scale = 1f, build = BuildGrunt, frameW = 2.0f, frameH = 2.4f, ppm = 40, ranked = true },
            new EnemySpec { kind = Enemy.Kind.Elite, radius = 0.5f, height = 2.9f, scale = 1f, build = t => BuildElite(t, new Color(0.15f, 0.38f, 0.95f), false, false, false), frameW = 2.8f, frameH = 3.5f, ppm = 40, ranked = true },
            new EnemySpec { kind = Enemy.Kind.Ranger, radius = 0.5f, height = 2.9f, scale = 1f, build = t => BuildElite(t, new Color(0.18f, 0.6f, 0.28f), false, true, false), frameW = 2.8f, frameH = 3.5f, ppm = 40 },
            new EnemySpec { kind = Enemy.Kind.General, radius = 0.5f, height = 2.9f, scale = 1.1f, build = t => BuildElite(t, new Color(0.9f, 0.68f, 0.12f), true, false, false), frameW = 3.1f, frameH = 3.75f, ppm = 40 },
            new EnemySpec { kind = Enemy.Kind.Zealot, radius = 0.5f, height = 2.9f, scale = 1.1f, build = t => BuildElite(t, new Color(0.95f, 0.78f, 0.25f), true, false, true), frameW = 3.4f, frameH = 3.75f, ppm = 40 },
            new EnemySpec { kind = Enemy.Kind.Wraith, radius = 1.8f, height = 3.6f, scale = 1f, build = BuildWraith, frameW = 9.1f, frameH = 4.5f, ppm = 22 },
            new EnemySpec { kind = Enemy.Kind.Banshee, radius = 0f, height = 0f, scale = 1f, build = BuildBanshee, frameW = 6.25f, frameH = 2.9f, ppm = 24, pivotFeet = false },
        };
    }

    // 3D model prefab: geometry only, not in Resources (the game uses the baked sprites instead)
    static void BuildEnemyModels()
    {
        foreach (var s in EnemySpecs())
        {
            var root = new GameObject(s.kind.ToString());
            s.build(root.transform);
            root.transform.localScale = Vector3.one * s.scale;
            SavePrefab(root, "Assets/Prefabs/Models/" + s.kind + ".prefab");
        }
    }

    // Runtime enemy: Enemy + CharacterController (collision) + a camera-facing sprite quad
    static void BuildEnemySpritePrefabs()
    {
        var shader = Shader.Find("LastStand/Sprite");
        foreach (var s in EnemySpecs())
        {
            var root = new GameObject(s.kind.ToString());
            var e = root.AddComponent<Enemy>(); e.kind = s.kind;
            if (s.height > 0f)
            {
                var cc = root.AddComponent<CharacterController>();
                cc.radius = s.radius; cc.height = s.height; cc.center = new Vector3(0, s.height / 2f, 0); cc.stepOffset = 0.6f; cc.slopeLimit = 50f;
            }
            else
            {
                var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(3.2f, 1.4f, 3.4f);   // fliers need a hit volume
            }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Sprite";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            quad.transform.localScale = new Vector3(s.frameW, s.frameH, 1f);
            quad.transform.localPosition = new Vector3(0, s.pivotFeet ? s.frameH * 0.5f : 0f, 0);
            string matPath = Gen + "/Materials/Sprite_" + s.kind + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetFloat("_Frames", 8f);
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.GetComponent<Renderer>().receiveShadows = false;
            quad.AddComponent<SpriteBillboard>();
            SavePrefab(root, "Assets/Resources/Enemies/" + s.kind + ".prefab");
        }
    }

    // First-person arms (sleeves, plates, gloves) around a Gun holder; baked together with the weapon model into the HUD sprite
    public static void BuildViewArms(Transform gun)
    {
        // First-person forearms: armored sleeves and gloves holding the weapon
        var sleeve = Mat(new Color(0.2f, 0.26f, 0.18f), 0f, null, default(Vector2), 0.5f, "Standard", 0.4f);
        var glove = Mat(new Color(0.07f, 0.07f, 0.08f), 0f, null, default(Vector2), 0.3f, "Standard", 0.1f);
        var plate = Mat(new Color(0.3f, 0.36f, 0.24f), 0f, null, default(Vector2), 0.5f, "Standard", 0.5f);
        var inner = Mat(new Color(0.1f, 0.1f, 0.11f), 0f, null, default(Vector2), 0.35f, "Standard", 0.3f);
        var sleeveMat = Mat(new Color(0.22f, 0.27f, 0.17f), 0f, null, default(Vector2), 0.4f, "Standard", 0.3f);
        // right hand flanks the grip, left hand flanks the foregrip; forearms enter from the bottom corners
        Limb(gun, PrimitiveType.Capsule, "ForearmR", new Vector3(0.2f, -0.42f, -0.45f), new Vector3(0.075f, -0.075f, 0.02f), 0.08f, sleeveMat);
        Prim(PrimitiveType.Cube, gun, "VambraceR", new Vector3(0.15f, -0.2f, -0.22f), new Vector3(0.1f, 0.06f, 0.2f), plate, false, new Vector3(-18f, 20f, 8f));
        Glove(gun, new Vector3(0.07f, -0.06f, 0.03f), inner);
        Limb(gun, PrimitiveType.Capsule, "ForearmL", new Vector3(-0.3f, -0.44f, -0.3f), new Vector3(-0.085f, -0.07f, 0.34f), 0.075f, sleeveMat);
        Prim(PrimitiveType.Cube, gun, "VambraceL", new Vector3(-0.2f, -0.24f, 0.02f), new Vector3(0.09f, 0.06f, 0.22f), plate, false, new Vector3(-12f, -22f, -8f));
        Glove(gun, new Vector3(-0.085f, -0.055f, 0.36f), inner);
    }
}
