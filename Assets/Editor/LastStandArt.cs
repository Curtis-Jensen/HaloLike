using UnityEngine;

// A tiny pixel-art painter and the hand-authored 2D drawings of the Covenant and the first-person weapons.
// Shapes are filled with a bevel (light rim top-left, shadow rim bottom-right, a soft top-to-bottom gradient), then outlined and posterized,
// so everything reads as painted pixel sprites rather than shaded geometry. Coordinates are image pixels, y DOWN.
public class Canvas
{
    public readonly int W, H;
    public readonly Color[] Px;
    static readonly Color Ink = new Color(0.05f, 0.035f, 0.035f, 1f);

    public Canvas(int w, int h) { W = w; H = h; Px = new Color[w * h]; }

    // ---- masks ----
    public bool[] PolyMask(Vector2[] p)
    {
        var m = new bool[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f; bool inside = false;
                for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                    if ((p[i].y > fy) != (p[j].y > fy) && fx < (p[j].x - p[i].x) * (fy - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
                m[y * W + x] = inside;
            }
        return m;
    }

    public bool[] EllipseMask(float cx, float cy, float rx, float ry, float rotDeg = 0f)
    {
        var m = new bool[W * H]; float c = Mathf.Cos(rotDeg * Mathf.Deg2Rad), s = Mathf.Sin(rotDeg * Mathf.Deg2Rad);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy, u = dx * c + dy * s, v = -dx * s + dy * c;
                m[y * W + x] = (u * u) / (rx * rx) + (v * v) / (ry * ry) <= 1f;
            }
        return m;
    }

    bool G(bool[] m, int x, int y) { return x >= 0 && y >= 0 && x < W && y < H && m[y * W + x]; }

    // ---- painting ----
    // Bevel-shaded fill: highlight where the up-left neighbour is outside, shadow where the down-right neighbour is outside
    public void Paint(bool[] m, Color c)
    {
        int minY = H, maxY = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (m[y * W + x]) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
        if (maxY < minY) return;
        for (int y = minY; y <= maxY; y++)
            for (int x = 0; x < W; x++)
            {
                if (!m[y * W + x]) continue;
                float t = (y - minY) / (float)(maxY - minY + 1);
                Color o = c * (1.1f - 0.3f * t); o.a = 1f;
                bool hi = !G(m, x - 1, y - 1) || !G(m, x - 2, y - 2) && !G(m, x - 1, y);
                bool sh = !G(m, x + 1, y + 1) || !G(m, x + 2, y + 2);
                if (hi) o = Color.Lerp(c, Color.white, 0.32f);
                else if (sh) o = c * 0.55f;
                o.a = 1f; Px[y * W + x] = o;
            }
    }

    public void Poly(Vector2[] p, Color c) { Paint(PolyMask(p), c); }
    public void Ell(float cx, float cy, float rx, float ry, Color c, float rot = 0f) { Paint(EllipseMask(cx, cy, rx, ry, rot), c); }

    // Tapered capsule between two points (limbs, barrels)
    public void Limb(Vector2 a, Vector2 b, float w0, float w1, Color c)
    {
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
        Poly(new[] { a + n * w0 * 0.5f, b + n * w1 * 0.5f, b - n * w1 * 0.5f, a - n * w0 * 0.5f }, c);
    }

    // Flat unshaded disc: eyes, lights, energy
    public void Glow(float cx, float cy, float r, Color c)
    {
        var m = EllipseMask(cx, cy, r, r);
        for (int i = 0; i < m.Length; i++) if (m[i]) Px[i] = c;
        var core = EllipseMask(cx, cy, r * 0.5f, r * 0.5f);
        for (int i = 0; i < core.Length; i++) if (core[i]) Px[i] = Color.Lerp(c, Color.white, 0.7f);
    }

    public void Flat(Vector2[] p, Color c) { var m = PolyMask(p); for (int i = 0; i < m.Length; i++) if (m[i]) Px[i] = new Color(c.r, c.g, c.b, 1f); }

    // Dark outline around the whole silhouette + posterize
    public void Finish(int levels = 8)
    {
        for (int i = 0; i < Px.Length; i++)
            if (Px[i].a > 0.5f) Px[i] = new Color(Q(Px[i].r, levels), Q(Px[i].g, levels), Q(Px[i].b, levels), 1f);
        var src = (Color[])Px.Clone();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (src[y * W + x].a > 0.5f) continue;
                bool e = (x > 0 && src[y * W + x - 1].a > 0.5f) || (x < W - 1 && src[y * W + x + 1].a > 0.5f)
                      || (y > 0 && src[(y - 1) * W + x].a > 0.5f) || (y < H - 1 && src[(y + 1) * W + x].a > 0.5f);
                if (e) Px[y * W + x] = Ink;
            }
    }

    static float Q(float v, int l) { return Mathf.Round(Mathf.Clamp01(v) * (l - 1)) / (l - 1); }

    // Pixels in unity order (bottom row first)
    public Color[] ToUnityPixels()
    {
        var o = new Color[W * H];
        for (int y = 0; y < H; y++) System.Array.Copy(Px, y * W, o, (H - 1 - y) * W, W);
        return o;
    }

    public Canvas Mirrored()
    {
        var c = new Canvas(W, H);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) c.Px[y * W + x] = Px[y * W + (W - 1 - x)];
        return c;
    }
}

public enum View { Front, Side, Back }

public static class LastStandArt
{
    static Vector2 P(float x, float y) { return new Vector2(x, y); }
    static Color Dk(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, 1f); }
    static readonly Color Suit = new Color(0.16f, 0.13f, 0.18f), Skin = new Color(0.36f, 0.27f, 0.27f), Gun = new Color(0.25f, 0.26f, 0.32f),
                          Cyan = new Color(0.4f, 0.85f, 1f), Gold = new Color(0.95f, 0.75f, 0.2f), Orange = new Color(1f, 0.55f, 0.15f);

    // ---------------- enemies ----------------

    public static Canvas Enemy(global::Enemy.Kind kind, global::Enemy.Rank rank, View v)
    {
        Canvas c;
        switch (kind)
        {
            case global::Enemy.Kind.Grunt: c = new Canvas(80, 96); Grunt(c, v, Spawner.RankColor(kind, rank)); break;
            case global::Enemy.Kind.Wraith: c = new Canvas(200, 100); Wraith(c, v); break;
            case global::Enemy.Kind.Banshee: c = new Canvas(150, 70); Banshee(c, v); break;
            default:
            {
                bool general = kind == global::Enemy.Kind.General, zealot = kind == global::Enemy.Kind.Zealot, ranger = kind == global::Enemy.Kind.Ranger;
                Color armor = kind == global::Enemy.Kind.Elite ? Spawner.RankColor(kind, rank)
                            : ranger ? new Color(0.2f, 0.55f, 0.28f) : general ? new Color(0.85f, 0.65f, 0.15f) : new Color(0.92f, 0.78f, 0.3f);
                c = new Canvas(general ? 120 : zealot ? 124 : 112, general || zealot ? 150 : 140);
                Elite(c, v, armor, general || zealot, ranger, zealot);
                break;
            }
        }
        c.Finish();
        return c;
    }

    static void Elite(Canvas c, View v, Color A, bool crest, bool jet, bool sword)
    {
        Color AD = Dk(A, 0.62f);
        float ox = (c.W - 100) * 0.5f, oy = c.H - 140;    // 100x140 design space, centered and bottom-aligned
        System.Func<float, float, Vector2> p = (x, y) => new Vector2(x + ox, y + oy);
        System.Func<float, float, float, float, Vector2[]> quad = (x0, y0, x1, y1) => new[] { p(x0, y0), p(x1, y0), p(x1, y1), p(x0, y1) };

        if (v == View.Side)
        {
            if (jet) { c.Poly(new[] { p(26, 38), p(36, 36), p(36, 66), p(27, 68) }, Gun); c.Glow(p(30, 70).x, p(30, 70).y, 3.5f, Orange); }
            // far leg (darker), digitigrade: thigh forward, shin back
            c.Limb(p(46, 82), p(54, 100), 11, 9, Dk(Suit, 0.8f)); c.Limb(p(54, 100), p(40, 124), 9, 7, Dk(AD, 0.8f));
            c.Poly(new[] { p(28, 128), p(50, 127), p(60, 134), p(28, 137) }, Dk(Suit, 0.8f));
            // near leg
            c.Limb(p(50, 82), p(63, 101), 12, 10, Suit); c.Limb(p(63, 101), p(47, 124), 10, 7, AD);
            c.Ell(p(63, 101).x, p(63, 101).y, 6, 6, A);
            c.Poly(new[] { p(36, 126), p(56, 126), p(68, 134), p(34, 137) }, Suit);
            c.Ell(p(50, 80).x, p(50, 80).y, 13, 9, AD);
            // torso leaning forward
            c.Poly(new[] { p(38, 38), p(68, 40), p(70, 66), p(58, 82), p(40, 78) }, A);
            c.Poly(new[] { p(30, 40), p(40, 38), p(40, 74), p(33, 72) }, AD);
            c.Poly(new[] { p(50, 48), p(66, 48), p(66, 64), p(52, 66) }, AD);
            c.Ell(p(52, 40).x, p(52, 40).y, 12, 9, A, -10); c.Ell(p(52, 36).x, p(52, 36).y, 8, 3.5f, AD, -10);
            // arm holding the weapon forward
            c.Limb(p(52, 46), p(66, 60), 9, 8, Suit); c.Limb(p(66, 60), p(82, 61), 7, 6, Suit);
            if (!sword)
            {
                c.Poly(new[] { p(62, 59), p(97, 56), p(98, 63), p(62, 69) }, Gun);
                c.Poly(new[] { p(94, 55), p(100, 53), p(100, 58), p(95, 60) }, Dk(Gun, 1.3f)); c.Glow(p(98, 58).x, p(98, 58).y, 2.6f, Cyan);
            }
            else c.Poly(new[] { p(82, 58), p(100, 20), p(102, 24), p(86, 64) }, Cyan);
            // head, neck, mandibles
            c.Limb(p(58, 38), p(64, 28), 9, 8, Skin);
            c.Ell(p(68, 22).x, p(68, 22).y, 14, 9, AD, -12);
            c.Poly(new[] { p(71, 17), p(79, 19), p(78, 23), p(70, 22) }, Cyan);
            c.Limb(p(76, 26), p(88, 31), 3.5f, 2, Skin); c.Limb(p(73, 29), p(83, 38), 3.5f, 2, Skin);
            if (crest) c.Poly(new[] { p(60, 15), p(55, 3), p(68, 12) }, Gold);
        }
        else
        {
            bool front = v == View.Front;
            if (jet) { c.Poly(quad(31, 22, 38, 44), Gun); c.Poly(quad(62, 22, 69, 44), Gun); if (front) { c.Glow(p(34, 22).x, p(34, 22).y, 3, Orange); c.Glow(p(66, 22).x, p(66, 22).y, 3, Orange); } }
            foreach (float s in new[] { -1f, 1f })
            {
                float L(float x) { return s < 0 ? x : 100 - x; }
                c.Poly(new[] { p(L(30), 80), p(L(47), 80), p(L(46), 104), p(L(33), 104) }, Suit);
                c.Poly(new[] { p(L(33), 104), p(L(46), 104), p(L(45), 127), p(L(36), 127) }, AD);
                c.Poly(new[] { p(L(26), 127), p(L(46), 127), p(L(46), 137), p(L(22), 137) }, Suit);
                if (front) c.Ell(p(L(40), 104).x, p(L(40), 104).y, 7, 5, A);
            }
            c.Poly(quad(27, 72, 73, 86), AD);
            c.Poly(new[] { p(25, 36), p(75, 36), p(70, 74), p(30, 74) }, A);
            if (front)
            {
                c.Poly(quad(43, 74, 57, 90), A);
                c.Poly(new[] { p(33, 40), p(67, 40), p(62, 62), p(38, 62) }, AD);
                c.Glow(p(50, 50).x, p(50, 50).y, 2.6f, Cyan);
            }
            else { c.Poly(new[] { p(34, 40), p(66, 40), p(62, 70), p(38, 70) }, AD); c.Poly(quad(46, 38, 54, 72), A); }
            foreach (float s in new[] { -1f, 1f })
            {
                float L(float x) { return s < 0 ? x : 100 - x; }
                c.Ell(p(L(21), 38).x, p(L(21), 38).y, 13, 9, A, s * 14f); c.Ell(p(L(21), 34).x, p(L(21), 34).y, 9, 4, AD, s * 14f);
                c.Limb(p(L(17), 44), p(L(14), 66), 9, 8, Suit);
            }
            // arms: left hangs, right holds the weapon at the hip
            c.Limb(p(14, 66), p(22, 82), 7, 6, Suit); c.Ell(p(23, 84).x, p(23, 84).y, 4.5f, 4.5f, Gun);
            c.Limb(p(86, 66), p(74, 76), 7, 6, Suit);
            if (front && !sword) { c.Poly(new[] { p(66, 64), p(79, 62), p(77, 92), p(68, 95) }, Gun); c.Glow(p(72, 97).x, p(72, 97).y, 3f, Cyan); }
            else if (!front && !sword) c.Poly(new[] { p(80, 60), p(90, 58), p(92, 90), p(82, 92) }, Gun);
            if (sword) c.Poly(new[] { p(70, 52), p(75, 52), p(75, 102), p(72.5f, 107), p(70, 102) }, Cyan);
            c.Poly(new[] { p(44, 30), p(56, 30), p(55, 38), p(45, 38) }, Skin);
            c.Ell(p(50, 20).x, p(50, 20).y, 11, 14, AD);
            if (front)
            {
                c.Poly(new[] { p(43, 17), p(57, 17), p(55, 22.5f), p(45, 22.5f) }, Cyan);
                c.Poly(new[] { p(40, 12), p(60, 12), p(58, 17), p(42, 17) }, A);
                c.Limb(p(43, 29), p(38, 43), 3.5f, 2, Skin); c.Limb(p(47, 31), p(44, 45), 3.5f, 2, Skin);
                c.Limb(p(57, 29), p(62, 43), 3.5f, 2, Skin); c.Limb(p(53, 31), p(56, 45), 3.5f, 2, Skin);
            }
            if (crest) c.Poly(new[] { p(45, 10), p(50, -2), p(55, 10) }, Gold);
        }
    }

    static void Grunt(Canvas c, View v, Color A)
    {
        Color AD = Dk(A, 0.6f), Teal = new Color(0.2f, 0.55f, 0.55f), Head = new Color(0.55f, 0.4f, 0.3f);
        System.Func<float, float, Vector2> p = (x, y) => new Vector2(x, y);
        if (v == View.Side)
        {
            c.Poly(new[] { p(14, 30), p(30, 26), p(34, 66), p(16, 68) }, Teal);                    // methane tank on the back
            c.Poly(new[] { p(18, 24), p(26, 22), p(26, 30), p(18, 30) }, Dk(Teal, 0.7f));
            c.Limb(p(40, 66), p(36, 86), 9, 8, Suit); c.Poly(new[] { p(30, 86), p(46, 86), p(52, 94), p(28, 94) }, Dk(Suit, 0.6f));
            c.Ell(p(40, 52).x, p(40, 52).y, 17, 17, A); c.Ell(p(44, 56).x, p(44, 56).y, 10, 10, AD);
            c.Limb(p(46, 44), p(60, 56), 7, 6, Suit); c.Poly(new[] { p(56, 52), p(72, 52), p(72, 60), p(56, 62) }, Gun); c.Glow(p(72, 56).x, p(72, 56).y, 3, new Color(0.3f, 1f, 0.4f));
            c.Ell(p(46, 30).x, p(46, 30).y, 14, 13, Head); c.Poly(new[] { p(52, 30), p(64, 32), p(62, 42), p(50, 40) }, Dk(Gun, 0.6f)); c.Glow(p(54, 24).x, p(54, 24).y, 2.6f, new Color(1f, 0.85f, 0.2f));
        }
        else
        {
            bool f = v == View.Front;
            foreach (float s in new[] { -1f, 1f })
            {
                float L(float x) { return s < 0 ? x : 80 - x; }
                c.Limb(p(L(32), 66), p(L(30), 86), 9, 8, Suit); c.Poly(new[] { p(L(24), 86), p(L(37), 86), p(L(39), 94), p(L(22), 94) }, Dk(Suit, 0.6f));
                c.Poly(new[] { p(L(22), 26), p(L(30), 26), p(L(30), 46), p(L(22), 46) }, Teal);
            }
            c.Ell(40, 54, 19, 16, A);
            if (f)
            {
                c.Ell(40, 59, 11, 9, AD);
                c.Limb(p(23, 46), p(18, 64), 7, 6, Suit); c.Limb(p(57, 46), p(60, 60), 7, 6, Suit);
                c.Poly(new[] { p(57, 56), p(66, 56), p(66, 74), p(58, 74) }, Gun); c.Glow(62, 76, 2.5f, new Color(0.3f, 1f, 0.4f));
            }
            else { c.Poly(new[] { p(28, 36), p(52, 36), p(54, 66), p(26, 66) }, Teal); c.Poly(new[] { p(26, 46), p(54, 46), p(54, 50), p(26, 50) }, Dk(Gun, 0.7f)); }
            c.Ell(40, 30, 15, 14, Head);
            if (f)
            {
                c.Poly(new[] { p(30, 32), p(50, 32), p(48, 43), p(32, 43) }, Dk(Gun, 0.6f)); c.Glow(34, 26, 2.6f, new Color(1f, 0.85f, 0.2f)); c.Glow(46, 26, 2.6f, new Color(1f, 0.85f, 0.2f));
            }
        }
    }

    static void Wraith(Canvas c, View v)
    {
        Color hull = new Color(0.38f, 0.28f, 0.52f), dark = Dk(hull, 0.6f);
        if (v == View.Side)
        {
            c.Ell(100, 66, 88, 22, hull); c.Poly(new[] { P(170, 60), P(198, 68), P(176, 80), P(150, 78) }, dark);
            c.Ell(96, 70, 70, 6, new Color(0.7f, 0.5f, 1f));                                       // underglow
            c.Ell(88, 44, 28, 16, hull); c.Limb(P(104, 40), P(176, 28), 11, 7, dark); c.Ell(176, 28, 6, 6, Gun);
            c.Poly(new[] { P(20, 62), P(6, 70), P(24, 80) }, dark);
        }
        else
        {
            c.Ell(100, 66, 52, 26, hull); c.Ell(100, 40, 24, 16, hull); c.Ell(100, 40, 9, 9, dark); c.Ell(100, 74, 40, 6, new Color(0.7f, 0.5f, 1f));
            c.Ell(60, 80, 12, 9, dark); c.Ell(140, 80, 12, 9, dark);
        }
    }

    static void Banshee(Canvas c, View v)
    {
        Color hull = new Color(0.38f, 0.3f, 0.55f), dark = Dk(hull, 0.6f);
        if (v == View.Side)
        {
            c.Poly(new[] { P(58, 34), P(28, 62), P(50, 64), P(88, 40) }, dark);                    // swept wing
            c.Ell(76, 36, 56, 11, hull); c.Poly(new[] { P(22, 34), P(6, 18), P(34, 30) }, dark);
            c.Ell(104, 32, 14, 7, new Color(0.4f, 0.8f, 1f)); c.Glow(24, 38, 4, new Color(0.8f, 0.4f, 1f));
        }
        else
        {
            c.Poly(new[] { P(10, 40), P(66, 28), P(66, 40), P(14, 50) }, dark); c.Poly(new[] { P(140, 40), P(84, 28), P(84, 40), P(136, 50) }, dark);
            c.Ell(75, 36, 14, 14, hull); c.Ell(75, 30, 8, 6, new Color(0.4f, 0.8f, 1f));
            c.Glow(16, 44, 3.5f, new Color(0.8f, 0.4f, 1f)); c.Glow(134, 44, 3.5f, new Color(0.8f, 0.4f, 1f));
        }
    }

    // ---------------- first-person weapons (256x144: gun rises from the bottom-right toward the center) ----------------

    public const int WeaponW = 256, WeaponH = 144;
    static readonly Vector2 Rear = new Vector2(222, 158), Front = new Vector2(104, 44);

    // Point along the gun: t = 0 rear .. 1 front, s = pixels perpendicular (+ = top), tapering toward the front for perspective
    static Vector2 GP(float t, float s)
    {
        Vector2 v = Front - Rear, u = v.normalized, n = new Vector2(-u.y, u.x);
        if (n.y > 0) n = -n;
        return Rear + v * t + n * s * 1.75f * (1f - 0.4f * t);   // 1.75 = chunky, readable at retro resolution
    }
    static Vector2[] GQ(float t0, float s0, float t1, float s1) { return new[] { GP(t0, s0), GP(t1, s0), GP(t1, s1), GP(t0, s1) }; }

    public static Canvas Weapon(string id, out Vector2 muzzleUv)
    {
        var c = new Canvas(WeaponW, WeaponH);
        Color olive = new Color(0.33f, 0.4f, 0.24f), gm = new Color(0.22f, 0.23f, 0.27f), tan = new Color(0.62f, 0.5f, 0.32f), black = new Color(0.1f, 0.1f, 0.12f),
              sleeve = new Color(0.27f, 0.33f, 0.2f), glove = new Color(0.14f, 0.14f, 0.17f);
        float muzzleT = 1.04f;
        // forearms enter from the bottom corners
        c.Limb(P(246, 156), P(212, 112), 44, 30, sleeve); c.Limb(P(232, 150), P(212, 114), 12, 10, new Color(0.45f, 0.52f, 0.3f));
        c.Limb(P(104, 156), P(142, 94), 38, 26, sleeve); c.Limb(P(112, 150), P(140, 98), 10, 8, new Color(0.45f, 0.52f, 0.3f));
        switch (id)
        {
            case "dmr":
                c.Poly(GQ(0.0f, -13, 0.22f, 9), tan); c.Poly(GQ(0.18f, -11, 0.58f, 15), olive); c.Poly(GQ(0.55f, -8, 0.84f, 10), gm); c.Poly(GQ(0.82f, -2, 1.04f, 4), black);
                c.Poly(GQ(0.3f, 15, 0.66f, 26), black); c.Glow(GP(0.67f, 21).x, GP(0.67f, 21).y, 4f, Cyan);
                c.Poly(GQ(0.34f, -11, 0.44f, -28), gm); c.Poly(GQ(0.2f, -11, 0.27f, -26), black); break;
            case "ar":
                c.Poly(GQ(0.0f, -12, 0.2f, 9), olive); c.Poly(GQ(0.16f, -12, 0.55f, 16), gm); c.Poly(GQ(0.5f, -9, 0.8f, 11), black); c.Poly(GQ(0.78f, -2, 1.02f, 5), black);
                c.Poly(GQ(0.2f, 16, 0.5f, 24), gm); c.Poly(GQ(0.24f, 2, 0.5f, 7), Orange);
                c.Poly(new[] { GP(0.36f, -12), GP(0.46f, -12), GP(0.5f, -34), GP(0.4f, -36) }, gm); c.Poly(GQ(0.18f, -11, 0.25f, -26), black); break;
            case "pistol":
                muzzleT = 0.62f; c.Poly(GQ(0.05f, -10, 0.5f, 12), gm); c.Poly(GQ(0.1f, 12, 0.5f, 18), Dk(gm, 1.4f)); c.Poly(GQ(0.45f, -2, 0.62f, 6), black); c.Poly(GQ(0.1f, -10, 0.2f, -34), black); break;
            case "shotgun":
                c.Poly(GQ(0.0f, -13, 0.25f, 9), tan); c.Poly(GQ(0.2f, -11, 0.5f, 14), gm); c.Poly(GQ(0.45f, 3, 1.04f, 11), black); c.Poly(GQ(0.45f, -9, 0.9f, 0), Dk(gm, 1.3f));
                c.Poly(GQ(0.55f, -14, 0.75f, -2), tan); break;
            case "sniper":
                muzzleT = 1.08f; c.Poly(GQ(0.0f, -13, 0.22f, 10), new Color(0.3f, 0.36f, 0.45f)); c.Poly(GQ(0.2f, -11, 0.62f, 14), new Color(0.3f, 0.36f, 0.45f)); c.Poly(GQ(0.6f, -3, 1.08f, 5), black);
                c.Poly(GQ(0.22f, 14, 0.72f, 30), black); c.Glow(GP(0.73f, 22).x, GP(0.73f, 22).y, 5f, Cyan); c.Poly(GQ(0.3f, 0, 0.5f, 9), Color.white); break;
            case "sword":
                muzzleT = 1.12f; c.Poly(GQ(0.0f, -4, 0.18f, 4), black); c.Poly(GQ(0.16f, -14, 0.2f, 14), gm);
                c.Poly(GQ(0.2f, -5, 1.12f, 5), new Color(0.3f, 0.7f, 1f)); c.Poly(GQ(0.24f, -2, 1.05f, 2), new Color(0.8f, 0.95f, 1f)); break;
            case "concussion":
                muzzleT = 0.96f; c.Poly(GQ(0.0f, -16, 0.4f, 16), new Color(0.38f, 0.28f, 0.6f)); c.Poly(GQ(0.35f, -13, 0.95f, 13), new Color(0.3f, 0.22f, 0.5f));
                c.Poly(GQ(0.2f, 14, 0.45f, 22), black); c.Poly(GQ(0.45f, -6, 0.5f, 6), Cyan); c.Poly(GQ(0.65f, -6, 0.7f, 6), Cyan); c.Poly(GQ(0.9f, -8, 0.96f, 8), black); break;
            case "turret":
                muzzleT = 1.06f; c.Poly(GQ(0.0f, -20, 0.5f, 18), gm); c.Poly(GQ(0.45f, -9, 0.78f, 11), Dk(gm, 1.4f)); c.Poly(GQ(0.75f, -3, 1.06f, 5), black);
                c.Poly(GQ(0.1f, 18, 0.5f, 26), black); c.Poly(GQ(0.3f, -20, 0.5f, -36), olive); break;
            case "plasma":
                muzzleT = 1.0f; c.Poly(GQ(0.0f, -12, 0.45f, 14), new Color(0.3f, 0.4f, 0.75f)); c.Poly(GQ(0.42f, -8, 0.8f, 10), new Color(0.22f, 0.3f, 0.6f));
                c.Poly(GQ(0.78f, 3, 1.0f, 9), new Color(0.35f, 0.5f, 0.9f)); c.Poly(GQ(0.78f, -9, 1.0f, -3), new Color(0.35f, 0.5f, 0.9f)); c.Poly(GQ(0.5f, -2, 0.98f, 2), Cyan);
                c.Poly(GQ(0.15f, 14, 0.4f, 18), Cyan); break;
            default: // repeater
                muzzleT = 1.0f; c.Poly(GQ(0.0f, -14, 0.5f, 16), new Color(0.22f, 0.5f, 0.3f)); c.Poly(GQ(0.48f, -9, 0.95f, 10), new Color(0.15f, 0.35f, 0.22f)); c.Poly(GQ(0.9f, -3, 1.0f, 4), black);
                c.Poly(GQ(0.1f, 16, 0.2f, 24), new Color(0.4f, 1f, 0.5f)); c.Poly(GQ(0.3f, 16, 0.4f, 24), new Color(0.4f, 1f, 0.5f)); break;
        }
        // gloved hands: right around the grip, left supporting the fore-end
        Hand(c, GP(0.2f, -6), 17, glove);
        Hand(c, id == "pistol" ? GP(0.3f, -8) : GP(0.6f, -7), 15, glove);
        c.Finish();
        Vector2 m = GP(muzzleT, 0f);
        muzzleUv = new Vector2(m.x / WeaponW, 1f - m.y / WeaponH);
        return c;
    }

    static void Hand(Canvas c, Vector2 at, float r, Color glove)
    {
        c.Ell(at.x, at.y, r * 1.1f, r * 0.85f, glove, -20);
        for (int i = 0; i < 4; i++) c.Limb(at + new Vector2(-r * 0.7f + i * r * 0.45f, -r * 0.5f), at + new Vector2(-r * 0.9f + i * r * 0.5f, -r * 1.05f), 3.4f, 3f, Dk(glove, 1.35f));
        c.Limb(at + new Vector2(r * 0.8f, -r * 0.1f), at + new Vector2(r * 0.3f, -r * 0.9f), 4f, 3.4f, Dk(glove, 1.3f));
    }
}
