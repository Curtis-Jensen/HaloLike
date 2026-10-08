using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Writes the hand-drawn pixel-art sprites (see LastStandArt) as PNGs into Resources/Sprites:
//  - every enemy variant: an 8-angle atlas (frame 0 faces the camera). Front/Side/Back drawings, mirrored and reused for the in-between angles.
//  - every first-person weapon: arms + gun, one full-screen 256x144 image
// Sizes and muzzle positions go to Resources/Sprites/dims.txt. Replace any PNG with real art and the game uses it as-is.
public static class LastStandSprites
{
    static readonly StringBuilder dims = new StringBuilder();
    static readonly List<Color[]> previewEnemies = new List<Color[]>(), previewWeapons = new List<Color[]>();
    static readonly List<int> previewW = new List<int>(), previewH = new List<int>();

    // frame k -> (view, mirrored). Side is drawn facing right; k=2 faces screen-left, k=6 faces screen-right
    static readonly View[] ViewFor = { View.Front, View.Front, View.Side, View.Back, View.Back, View.Back, View.Side, View.Front };
    static readonly bool[] MirrorFor = { false, false, true, false, false, false, false, false };

    public static void BakeAll()
    {
        dims.Length = 0; previewEnemies.Clear(); previewWeapons.Clear(); previewW.Clear(); previewH.Clear();
        foreach (var s in LastStandBuilder.EnemySpecs())
        {
            if (s.ranked) foreach (Enemy.Rank r in new[] { Enemy.Rank.Minor, Enemy.Rank.Major, Enemy.Rank.Ultra }) BakeEnemy(s, r, true);
            else BakeEnemy(s, Enemy.Rank.Major, false);
        }
        foreach (var w in Weapons.All) BakeWeapon(w);
        File.WriteAllText("Assets/Resources/Sprites/dims.txt", dims.ToString());
        AssetDatabase.Refresh();
        WritePreviews();
    }

    static void BakeEnemy(LastStandBuilder.EnemySpec s, Enemy.Rank rank, bool ranked)
    {
        string name = ranked ? s.kind + "_" + rank : s.kind.ToString();
        var views = new Dictionary<View, Canvas>();
        foreach (View v in new[] { View.Front, View.Side, View.Back }) views[v] = LastStandArt.Enemy(s.kind, rank, v);
        int fw = views[View.Front].W, fh = views[View.Front].H;
        var atlas = new Texture2D(fw * 8, fh, TextureFormat.RGBA32, false);
        for (int k = 0; k < 8; k++)
        {
            var cv = views[ViewFor[k]]; if (MirrorFor[k]) cv = cv.Mirrored();
            atlas.SetPixels(k * fw, 0, fw, fh, cv.ToUnityPixels());
        }
        previewEnemies.Add(atlas.GetPixels()); previewW.Add(fw); previewH.Add(fh);
        SavePng(atlas, "Assets/Resources/Sprites/" + name + ".png");
        dims.AppendLine(name + "," + (fw / (float)s.ppm).ToString(CultureInfo.InvariantCulture) + "," + (fh / (float)s.ppm).ToString(CultureInfo.InvariantCulture) + "," + (s.pivotFeet ? 1 : 0));
    }

    static void BakeWeapon(WeaponDef def)
    {
        Vector2 uv;
        var cv = LastStandArt.Weapon(def.id, out uv);
        var tex = new Texture2D(cv.W, cv.H, TextureFormat.RGBA32, false);
        tex.SetPixels(cv.ToUnityPixels());
        previewWeapons.Add(tex.GetPixels());
        SavePng(tex, "Assets/Resources/Sprites/w_" + def.id + ".png");
        dims.AppendLine("w_" + def.id + "," + uv.x.ToString(CultureInfo.InvariantCulture) + "," + uv.y.ToString(CultureInfo.InvariantCulture));
    }

    static void SavePng(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true; ti.npotScale = TextureImporterNPOTScale.None; ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
    }

    // ---------- review previews (Screenshots/sprites_*.png) ----------

    static void WritePreviews()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
        Directory.CreateDirectory(dir);
        var bg = new Color(0.55f, 0.4f, 0.32f, 1f);

        // enemies: front, side and back frames of every variant, at 1:1
        var sheet = NewSheet(1920, 800, bg);
        int x = 4, y = 796, rowH = 0;
        for (int i = 0; i < previewEnemies.Count; i++)
        {
            int fw = previewW[i], fh = previewH[i];
            foreach (int f in new[] { 0, 2, 4 })
            {
                if (x + fw > 1916) { x = 4; y -= rowH + 4; rowH = 0; }
                Blit(sheet, SubRect(previewEnemies[i], fw * 8, f * fw, fw, fh), fw, fh, x, y - fh);
                x += fw + 4; rowH = Mathf.Max(rowH, fh);
            }
        }
        Save(sheet, Path.Combine(dir, "sprites_enemies.png"));

        // weapons: 3x3 grid at 2.5x over a dusk background
        var ws = NewSheet(1920, 1080, bg);
        const int W = LastStandArt.WeaponW, H = LastStandArt.WeaponH;
        for (int i = 0; i < previewWeapons.Count && i < 9; i++)
        {
            var px = previewWeapons[i];
            for (int yy = 0; yy < H; yy++)
                for (int xx = 0; xx < W; xx++)
                {
                    var c = px[yy * W + xx]; if (c.a < 0.5f) continue;
                    int bx = (i % 3) * 640 + (int)(xx * 2.5f), by = (2 - i / 3) * 360 + (int)(yy * 2.5f);
                    for (int dy = 0; dy < 3; dy++) for (int dx = 0; dx < 3; dx++) if (bx + dx < 1920 && by + dy < 1080) ws.SetPixel(bx + dx, by + dy, c);
                }
        }
        Save(ws, Path.Combine(dir, "sprites_weapons.png"));
    }

    static Color[] SubRect(Color[] atlas, int atlasW, int x0, int w, int h)
    {
        var o = new Color[w * h];
        for (int y = 0; y < h; y++) System.Array.Copy(atlas, y * atlasW + x0, o, y * w, w);
        return o;
    }

    static Texture2D NewSheet(int w, int h, Color bg)
    {
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        var fill = new Color[w * h]; for (int i = 0; i < fill.Length; i++) fill[i] = bg;
        t.SetPixels(fill); return t;
    }

    static void Blit(Texture2D dst, Color[] src, int sw, int sh, int dx, int dy)
    {
        for (int yy = 0; yy < sh; yy++)
            for (int xx = 0; xx < sw; xx++)
            {
                var c = src[yy * sw + xx]; if (c.a < 0.5f) continue;
                int tx = dx + xx, ty = dy + yy;
                if (tx >= 0 && ty >= 0 && tx < dst.width && ty < dst.height) dst.SetPixel(tx, ty, c);
            }
    }

    static void Save(Texture2D t, string path) { File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t); }
}
