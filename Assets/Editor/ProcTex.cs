using System.IO;
using UnityEditor;
using UnityEngine;

// Procedural textures for the yard (concrete, rusty corrugated metal, steel plate, dirt, smoke-choked sky panorama).
// All noise is periodic so the textures tile without seams. Output is written as PNG assets by LastStandBuilder.
public static class ProcTex
{
    // ---- periodic value noise ----
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263 + seed * 1442695041;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    static float Smooth(float t) { return t * t * (3f - 2f * t); }

    // x,y in [0,1) tile space; period = lattice cells across the tile
    public static float Value(float x, float y, int px, int py, int seed)
    {
        float fx = x * px, fy = y * py;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
        int xa = ((x0 % px) + px) % px, xb = (xa + 1) % px, ya = ((y0 % py) + py) % py, yb = (ya + 1) % py;
        float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), tx);
        float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), tx);
        return Mathf.Lerp(a, b, ty);
    }

    public static float Fbm(float x, float y, int px, int py, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Value(x, y, px << i, py << i, seed + i * 17);
            norm += amp; amp *= 0.5f;
        }
        return sum / norm;
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static Texture2D Make(int w, int h, System.Func<float, float, Color> f)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
        tex.SetPixels(px);
        return tex;
    }

    // Dirty weathered concrete: blotchy base, grain, dark stains and hairline cracks
    public static Texture2D Concrete(int size = 256)
    {
        return Make(size, size, (u, v) =>
        {
            float blotch = Fbm(u, v, 3, 3, 4, 1);
            float grain = Value(u, v, 128, 128, 2);
            float stain = Mathf.SmoothStep(0.55f, 0.8f, Fbm(u, v, 4, 4, 3, 9));
            float crack = Mathf.Abs(Fbm(u, v, 6, 6, 3, 21) - 0.5f);
            float c = 0.68f + (blotch - 0.5f) * 0.35f + (grain - 0.5f) * 0.1f - stain * 0.22f;
            if (crack < 0.004f) c -= 0.12f;
            // long dark oil / tire streaks and rust-red patches
            float streakN = Fbm(u, v, 2, 14, 3, 201);
            c -= Mathf.SmoothStep(0.62f, 0.8f, streakN) * 0.25f;
            float rustP = Mathf.SmoothStep(0.7f, 0.85f, Fbm(u, v, 5, 5, 3, 211));
            if (rustP > 0f) return Color.Lerp(C(c * 1.0f, c * 0.97f, c * 0.92f), C(0.5f, 0.3f, 0.2f) * (c + 0.25f), rustP * 0.4f);
            // warm dust tint
            return C(c * 1.04f, c * 0.98f, c * 0.9f);
        });
    }

    // Corrugated container metal: vertical ribs, rust blooms, streaks, top/bottom frame
    public static Texture2D Corrugated(int size = 256)
    {
        return Make(size, size, (u, v) =>
        {
            float rib = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 12f);
            float shade = 0.62f + 0.3f * rib;
            float rust = Mathf.SmoothStep(0.55f, 0.78f, Fbm(u, v, 4, 4, 4, 33));
            float streak = Mathf.SmoothStep(0.5f, 0.8f, Fbm(u, v, 24, 3, 2, 41));
            float grime = Fbm(u, v, 3, 5, 3, 51);
            float bottomDirt = Mathf.SmoothStep(0.35f, 0f, v) * 0.5f;
            Color col = C(shade, shade, shade);
            col = Color.Lerp(col, C(0.62f, 0.34f, 0.18f) * (0.8f + 0.3f * rib), rust * 0.85f);
            col *= 1f - streak * 0.25f - (1f - grime) * 0.2f - bottomDirt;
            if (v < 0.035f || v > 0.965f) col *= 0.55f;           // frame rails
            col.a = 1f; return col;
        });
    }

    // Riveted steel plate: 2x2 panels with seams, rivets, stains
    public static Texture2D Plate(int size = 256)
    {
        return Make(size, size, (u, v) =>
        {
            float pu = (u * 2f) % 1f, pv = (v * 2f) % 1f;
            float seam = (pu < 0.02f || pu > 0.98f || pv < 0.02f || pv > 0.98f) ? 0.55f : 1f;
            float rivet = 1f;
            float ru = Mathf.Abs(((pu * 8f) % 1f) - 0.5f), rv = Mathf.Abs(((pv * 8f) % 1f) - 0.5f);
            if ((pv < 0.07f || pv > 0.93f) && ru < 0.2f && rv < 0.2f) rivet = 1.35f;
            float blotch = Fbm(u, v, 3, 3, 4, 61);
            float streak = Mathf.SmoothStep(0.5f, 0.85f, Fbm(u, v, 20, 2, 2, 71));
            float c = (0.52f + (blotch - 0.5f) * 0.3f) * seam * rivet - streak * 0.18f;
            return C(c * 1.02f, c * 0.98f, c * 0.94f);
        });
    }

    // Perforated steel grating: bright bars with dark holes
    public static Texture2D Grate(int size = 256)
    {
        return Make(size, size, (u, v) =>
        {
            float cu = (u * 16f) % 1f, cv = (v * 16f) % 1f;
            bool hole = cu > 0.22f && cu < 0.78f && cv > 0.22f && cv < 0.78f;
            float wear = Fbm(u, v, 4, 4, 3, 221);
            float c = hole ? 0.04f : 0.5f + (wear - 0.5f) * 0.3f;
            if (!hole && (cu < 0.1f || cv < 0.1f)) c *= 1.15f;
            return C(c, c * 0.98f, c * 0.95f);
        });
    }

    // Packed dirt and gravel for the surrounding terrain
    public static Texture2D Dirt(int size = 256)
    {
        return Make(size, size, (u, v) =>
        {
            float n = Fbm(u, v, 4, 4, 5, 81);
            float pebble = Mathf.SmoothStep(0.72f, 0.85f, Value(u, v, 48, 48, 91));
            float c = 0.58f + (n - 0.5f) * 0.4f + pebble * 0.12f;
            return C(c * 1.1f, c * 0.84f, c * 0.62f);
        });
    }

    // Equirectangular panorama: a sky choked with smoke and ash, orange glow on the horizon, a hazy sun
    public static Texture2D SmokeSky(int w = 2048, int h = 1024, float sunU = 0.5f, float sunElevationDeg = 7f)
    {
        float sunLon = (sunU - 0.5f) * Mathf.PI * 2f, sunLat = sunElevationDeg * Mathf.Deg2Rad;
        var sunDir = new Vector3(Mathf.Cos(sunLat) * Mathf.Sin(sunLon), Mathf.Sin(sunLat), Mathf.Cos(sunLat) * Mathf.Cos(sunLon));
        return Make(w, h, (u, v) =>
        {
            float lon = (u - 0.5f) * Mathf.PI * 2f, lat = (v - 0.5f) * Mathf.PI;
            var dir = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
            float e = Mathf.Max(0f, Mathf.Sin(lat));                    // 0 at horizon, 1 at zenith
            Color horizon = C(0.80f, 0.55f, 0.42f), mid = C(0.52f, 0.38f, 0.32f), top = C(0.15f, 0.12f, 0.13f);
            Color col = Color.Lerp(horizon, mid, Mathf.Clamp01(e * 3.2f));
            col = Color.Lerp(col, top, Mathf.Clamp01((e - 0.15f) * 1.3f));
            // billowing smoke layers
            float c1 = Fbm(u, v * 3f, 12, 6, 5, 101);
            float c2 = Fbm(u, v * 3f, 24, 12, 4, 131);
            float cn = 0.5f + (c1 * 0.7f + c2 * 0.3f - 0.5f) * 2.6f;     // fbm hugs 0.5, so stretch contrast to get real cloud masses
            float smoke = Mathf.SmoothStep(0.3f, 0.72f, cn);
            Color smokeCol = Color.Lerp(C(0.30f, 0.22f, 0.20f), C(0.62f, 0.40f, 0.28f), Mathf.Clamp01(1f - e * 2f));
            col = Color.Lerp(col, smokeCol, smoke * Mathf.Clamp01(0.35f + e) * 0.9f);
            // a teal-grey break in the smoke
            float teal = Mathf.SmoothStep(0.55f, 0.7f, 0.5f + (Fbm(u, v * 2f, 5, 3, 3, 171) - 0.5f) * 2.4f) * Mathf.SmoothStep(0.08f, 0.35f, e);
            col = Color.Lerp(col, C(0.40f, 0.48f, 0.48f), teal * 0.55f);
            // horizon smoke columns from the glassing
            float column = Mathf.SmoothStep(0.55f, 0.8f, Fbm(u, v, 40, 2, 3, 151)) * Mathf.Clamp01(1f - e * 5f);
            col = Color.Lerp(col, C(0.20f, 0.14f, 0.12f), column * 0.0f);
            // hazy sun and its bloom
            float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, sunDir), -1f, 1f));
            float glow = Mathf.Exp(-ang * ang * 9f) * 0.55f + Mathf.Exp(-ang * ang * 120f) * 1.2f;
            col += C(1f, 0.66f, 0.46f) * glow * (1f - smoke * 0.35f);
            // below the horizon: dusty ground tone (hidden by terrain anyway)
            if (lat < 0f) col = Color.Lerp(col, C(0.38f, 0.26f, 0.19f), Mathf.Clamp01(-lat * 6f));
            col.r = Mathf.Min(col.r, 1f); col.g = Mathf.Min(col.g, 1f); col.b = Mathf.Min(col.b, 1f); col.a = 1f;
            return col;
        });
    }

    public static string Save(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return path;
    }
}
