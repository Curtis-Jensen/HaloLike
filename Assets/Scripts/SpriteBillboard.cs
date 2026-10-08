using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Baked sprite data (written by Tools > Last Stand > Build Scene): atlas textures and sizes in Resources/Sprites.
public static class SpriteData
{
    static Dictionary<string, float[]> dims;

    static void Load()
    {
        dims = new Dictionary<string, float[]>();
        var ta = Resources.Load<TextAsset>("Sprites/dims");
        if (!ta) return;
        foreach (var line in ta.text.Split('\n'))
        {
            var p = line.Trim().Split(',');
            if (p.Length < 2) continue;
            var v = new float[p.Length - 1];
            for (int i = 1; i < p.Length; i++) float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i - 1]);
            dims[p[0]] = v;
        }
    }

    public static float[] Get(string name) { if (dims == null) Load(); float[] v; return dims.TryGetValue(name, out v) ? v : null; }
    public static Texture2D Tex(string name) { return Resources.Load<Texture2D>("Sprites/" + name); }

    public static string EnemyName(Enemy.Kind kind, Enemy.Rank rank)
    {
        return kind == Enemy.Kind.Grunt || kind == Enemy.Kind.Elite ? kind + "_" + rank : kind.ToString();
    }
}

// Doom-style enemy: a camera-facing quad that shows the baked frame matching the enemy's facing relative to the camera.
public class SpriteBillboard : MonoBehaviour
{
    public const int Frames = 8;
    Material mat;
    Transform root;

    public void Setup(Enemy.Kind kind, Enemy.Rank rank)
    {
        string name = SpriteData.EnemyName(kind, rank);
        var tex = SpriteData.Tex(name); var d = SpriteData.Get(name);
        if (!tex || d == null) { Debug.LogError("Missing baked sprite '" + name + "' - run Tools > Last Stand > Build Scene"); return; }
        var rend = GetComponent<Renderer>();
        mat = rend.material;
        mat.mainTexture = tex; mat.SetFloat("_Frames", Frames);
        transform.localScale = new Vector3(d[0], d[1], 1f);
        transform.localPosition = new Vector3(0f, d[2] > 0.5f ? d[1] * 0.5f : 0f, 0f);   // pivot at the feet, or centered for fliers
    }

    void Awake() { root = transform.parent; }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam || mat == null || !root) return;
        Vector3 toCam = cam.transform.position - transform.position; toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.LookRotation(-toCam);
        Vector3 fwd = root.forward; fwd.y = 0f;
        float a = Vector3.SignedAngle(toCam, fwd, Vector3.up);     // 0 = the enemy faces the camera
        int k = Mathf.RoundToInt(a / 45f); k = ((k % Frames) + Frames) % Frames;
        mat.SetFloat("_Frame", k);
    }
}
