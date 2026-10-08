using UnityEngine;

// Reach-style HUD drawn with IMGUI (dependency-free): vitals, motion tracker, objective, ability, weapon, scope and the ending screen.
public partial class GameBootstrap
{
    Texture2D scopeTex, visorTex;
    GUIStyle label, big, small, shadowStyle;

    void OnGUI()
    {
        var p = Player.Instance;
        if (!p) return;
        float w = Screen.width, h = Screen.height;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            big = new GUIStyle(label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(big) { fontSize = 22 };
            shadowStyle = new GUIStyle(label);
        }

        if (endStart >= 0f) { DrawEnding(w, h); return; }

        // Helmet visor: a dark frame around the view, until Noble Six takes the helmet off for the last stand
        float visor = helmetOff < 0f ? 1f : Mathf.Clamp01(1f - (Time.time - helmetOff) / 2f);
        if (visor > 0f)
        {
            if (!visorTex) visorTex = MakeVisor(128);
            GUI.color = new Color(1f, 1f, 1f, visor); GUI.DrawTexture(new Rect(0, 0, w, h), visorTex);
        }

        if (p.HitFlash > 0f) { GUI.color = new Color(1f, 0f, 0f, p.HitFlash * 0.25f); GUI.DrawTexture(new Rect(0, 0, w, h), white); }

        bool scoped = p.IsZoomed && p.Zoom >= 4f;
        if (scoped) DrawScope(w, h);
        else
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(w / 2 - 1, h / 2 - 8, 2, 16), white);
            GUI.DrawTexture(new Rect(w / 2 - 8, h / 2 - 1, 16, 2), white);
        }
        if (p.HitMarker > 0f)
        {
            GUI.color = new Color(1f, 0.3f, 0.3f, p.HitMarker);
            float m = 14f, s = 6f;
            foreach (var d in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                GUI.DrawTexture(new Rect(w / 2 + d.x * m - s / 2, h / 2 + d.y * m - s / 2, s, s), white);
        }

        // Shield (cyan) over health (red), Halo-style bars top-center
        Bar(new Rect(w / 2 - 150, 20, 300, 14), p.Shield / p.maxShield, new Color(0.3f, 0.8f, 1f));
        Bar(new Rect(w / 2 - 150, 38, 300, 8), p.Health / p.maxHealth, new Color(1f, 0.25f, 0.25f));

        // Objective + survival clock (top-left)
        label.fontSize = 18; Shadow(new Rect(20, 16, 500, 26), "SURVIVE AS LONG AS POSSIBLE", label);
        label.fontSize = 34; Shadow(new Rect(20, 38, 300, 44), FormatTime(Elapsed), label);
        label.fontSize = 18; Shadow(new Rect(20, 82, 300, 26), "KILLS " + kills, label);

        if (helmetOff < 0f) DrawRadar(p, h);
        else
        {
            label.fontSize = 16; Shadow(new Rect(20, h - 60, 400, 24), "HELMET REMOVED - TRACKER OFFLINE", label);
        }

        // Opening card: where and when
        float intro = Time.time - (startTime - 6f);
        if (intro < 7f)
        {
            float a = Mathf.Clamp01(Mathf.Min(intro / 1f, (7f - intro) / 1.2f));
            label.alignment = TextAnchor.UpperCenter; label.fontSize = 24;
            GUI.color = new Color(1f, 1f, 1f, a);
            Shadow(new Rect(0, h * 0.62f, w, 30), "20:00 HOURS - 30 AUGUST 2552", label);
            Shadow(new Rect(0, h * 0.62f + 32, w, 30), "ASŹOD SHIP-BREAKING YARDS - REACH", label);
            label.alignment = TextAnchor.UpperLeft; GUI.color = Color.white;
        }

        // Weapon + ammo (bottom-right)
        label.fontSize = 22;
        string ammo = p.IsMeleeWeapon ? "" : p.IsReloading ? "RELOADING..." : p.Ammo + " | " + p.Reserve;
        Shadow(new Rect(w - 420, h - 84, 400, 30), p.WeaponName, label);
        label.alignment = TextAnchor.UpperRight;
        Shadow(new Rect(w - 420, h - 52, 400, 30), ammo + "   GRENADES " + p.Grenades, label);
        label.alignment = TextAnchor.UpperLeft;

        // Controls reminder for the first moments, and a sprint cue
        float cx = w / 2;
        label.alignment = TextAnchor.UpperCenter; label.fontSize = 16;
        if (Time.time - (startTime - 6f) < 20f)
            Shadow(new Rect(cx - 400, h - 40, 800, 22), "WASD move   SHIFT sprint   C crouch   F melee   G grenade   E pick up   X drop shield   TAB swap weapon   RMB zoom", label);
        else if (p.IsSprinting) Shadow(new Rect(cx - 150, h - 40, 300, 22), "SPRINTING", label);
        label.alignment = TextAnchor.UpperLeft;

        if (!string.IsNullOrEmpty(p.Prompt))
        {
            label.alignment = TextAnchor.UpperCenter; label.fontSize = 20;
            Shadow(new Rect(0, h * 0.62f, w, 30), p.Prompt, label);
            label.alignment = TextAnchor.UpperLeft;
        }

        if (Time.time < bannerUntil) Shadow(new Rect(0, h * 0.2f, w, 70), banner, big);
        GUI.color = Color.white;
    }

    // Motion tracker: enemies relative to where you're facing, 45m range
    void DrawRadar(Player p, float h)
    {
        const float size = 160f, range = 45f;
        var r = new Rect(20, h - size - 24, size, size);
        GUI.color = new Color(0.05f, 0.2f, 0.25f, 0.55f); GUI.DrawTexture(r, circleTex);
        GUI.color = new Color(0.3f, 0.9f, 1f, 0.15f);
        float c = size / 2f;
        GUI.DrawTexture(new Rect(r.x + c - 0.5f, r.y, 1, size), white); GUI.DrawTexture(new Rect(r.x, r.y + c - 0.5f, size, 1), white);
        foreach (var e in Enemy.All)
        {
            Vector3 local = p.transform.InverseTransformPoint(e.transform.position);
            Vector2 v = new Vector2(local.x, local.z) / range * (c - 5f);
            bool far = v.magnitude > c - 5f;
            if (far) v = v.normalized * (c - 5f);
            GUI.color = e.IsFlying ? new Color(1f, 0.7f, 0.2f, far ? 0.5f : 1f) : new Color(1f, 0.25f, 0.25f, far ? 0.5f : 1f);
            float dot = e.kind == Enemy.Kind.Wraith ? 9f : 5f;
            GUI.DrawTexture(new Rect(r.x + c + v.x - dot / 2, r.y + c - v.y - dot / 2, dot, dot), white);
        }
        GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x + c - 3, r.y + c - 3, 6, 6), white);
    }

    // Soft-edged white disc, used for the motion tracker
    static Texture2D MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 20f)));
            }
        tex.Apply();
        return tex;
    }

    // Soft dark frame, clear in the middle
    static Texture2D MakeVisor(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                tex.SetPixel(x, y, new Color(0.02f, 0.05f, 0.06f, Mathf.Clamp01((d - 0.75f) * 1.6f) * 0.7f));
            }
        tex.Apply();
        return tex;
    }

    void DrawScope(float w, float h)
    {
        if (!scopeTex) scopeTex = MakeScope(256);
        GUI.color = Color.black;
        float side = (w - h) / 2f;
        GUI.DrawTexture(new Rect(0, 0, side, h), white); GUI.DrawTexture(new Rect(w - side, 0, side, h), white);
        GUI.DrawTexture(new Rect(side, 0, h, h), scopeTex);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(w / 2 - 1, h / 2 - h * 0.45f, 1, h * 0.9f), white);
        GUI.DrawTexture(new Rect(w / 2 - h * 0.45f, h / 2 - 1, h * 0.9f, 1), white);
    }

    // Black outside a circle, transparent inside
    static Texture2D MakeScope(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01((d - 0.9f) * 30f)));
            }
        tex.Apply();
        return tex;
    }

    // Noble Six falls: fade to black over the helmet, then the epilogue
    void DrawEnding(float w, float h)
    {
        float t = Time.time - endStart;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01((t - 3.5f) / 3.5f)); GUI.DrawTexture(new Rect(0, 0, w, h), white);

        big.fontSize = 44;
        Fade(w, h * 0.22f, "NOBLE SIX", t, 7f, big);
        Fade(w, h * 0.22f + 56f, "SPARTAN-B312 - MISSING IN ACTION", t, 8f, small);
        Fade(w, h * 0.42f, "7 JULY 2589", t, 10f, small);
        Fade(w, h * 0.42f + 34f, "Reach is glass and silence.", t, 10.8f, small);
        Fade(w, h * 0.56f, "Dr. Halsey, recorded: Six held the line so the rest of us could carry on.", t, 12.5f, small);
        Fade(w, h * 0.56f + 34f, "Everything that came after began with that stand.", t, 13.5f, small);
        Fade(w, h * 0.74f, "You lasted " + FormatTime(Mathf.Max(0f, endStart - startTime)) + "   -   " + kills + " kills", t, 14.5f, small);
        Fade(w, h * 0.74f + 40f, "Press ENTER to stand once more", t, 15.5f, small);
        GUI.color = Color.white;
    }

    void Fade(float w, float y, string text, float t, float start, GUIStyle style)
    {
        float a = Mathf.Clamp01((t - start) / 1.5f);
        if (a <= 0f) return;
        var c = style.normal.textColor; style.normal.textColor = new Color(1f, 1f, 1f, a);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, y, w, 60), text, style);
        style.normal.textColor = c;
    }

    void Shadow(Rect r, string text, GUIStyle style)
    {
        var prev = GUI.color;
        shadowStyle.fontSize = style.fontSize; shadowStyle.alignment = style.alignment; shadowStyle.fontStyle = style.fontStyle;
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * prev.a);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, shadowStyle);
        GUI.color = new Color(1f, 1f, 1f, prev.a);
        GUI.Label(r, text, style);
        GUI.color = prev;
    }

    void Bar(Rect r, float frac, Color c)
    {
        GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(r, white);
        GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), white);
        GUI.color = Color.white;
    }
}
