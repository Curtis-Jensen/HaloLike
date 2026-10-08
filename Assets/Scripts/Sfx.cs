using System;
using System.Collections.Generic;
using UnityEngine;

// Procedural audio: every sound effect and the music loop are synthesized from math at runtime,
// so the project needs no audio assets. Clips are cached after first use.
public static class Sfx
{
    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static readonly System.Random rng = new System.Random(3);
    static GameObject root;
    static AudioSource[] pool;
    static AudioSource music;
    static int next;

    static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

    // Sine at a frequency gliding f0 -> f1 over `dur` seconds (phase is integrated so there are no clicks).
    static float Sweep(float t, float f0, float f1, float dur) { return Mathf.Sin(TwoPi * (f0 * t + (f1 - f0) * t * t / (2f * dur))); }

    static AudioClip Make(string name, float seconds, Func<float, float> f)
    {
        int n = (int)(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void Ensure()
    {
        if (root) return;
        root = new GameObject("Sfx");
        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++) { pool[i] = root.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
        music = root.AddComponent<AudioSource>();
        music.loop = true; music.playOnAwake = false; music.volume = 0.4f;
    }

    static AudioSource NextSource() { next = (next + 1) % pool.Length; return pool[next]; }

    public static void Play2D(string name, float volume = 1f)
    {
        Ensure();
        var s = NextSource();
        s.transform.position = Vector3.zero;
        s.spatialBlend = 0f; s.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
        s.PlayOneShot(Get(name), volume);
    }

    public static void Play3D(string name, Vector3 pos, float volume = 1f)
    {
        Ensure();
        var s = NextSource();
        s.transform.position = pos;
        s.spatialBlend = 1f; s.minDistance = 4f; s.maxDistance = 70f; s.rolloffMode = AudioRolloffMode.Linear;
        s.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
        s.PlayOneShot(Get(name), volume);
    }

    public static float MusicVolume { set { Ensure(); music.volume = value; } }

    public static void StartMusic()
    {
        Ensure();
        if (music.isPlaying) return;
        music.clip = Get("music");
        music.Play();
    }

    static AudioClip Get(string name)
    {
        if (clips.TryGetValue(name, out var c) && c) return c;
        c = Build(name);
        clips[name] = c;
        return c;
    }

    static AudioClip Build(string name)
    {
        float lp = 0f;
        switch (name)
        {
            case "rifle":
                return Make(name, 0.18f, t => (Noise() * Mathf.Exp(-t * 38f) * 0.8f + Sweep(t, 220f, 60f, 0.18f) * Mathf.Exp(-t * 28f) * 0.7f));
            case "shotgun":
                return Make(name, 0.6f, t => { lp += (Noise() - lp) * 0.5f; return lp * Mathf.Exp(-t * 9f) * 1.6f + Sweep(t, 120f, 40f, 0.6f) * Mathf.Exp(-t * 10f); });
            case "reload":
                return Make(name, 0.4f, t =>
                {
                    float c = 0f;
                    foreach (float tc in new[] { 0f, 0.14f, 0.3f }) { float d = t - tc; if (d >= 0f) c += (Noise() * 0.6f + Mathf.Sin(TwoPi * 1800f * d) * 0.3f) * Mathf.Exp(-d * 160f); }
                    return c;
                });
            case "swap":
                return Make(name, 0.12f, t => (Noise() * 0.5f + Mathf.Sin(TwoPi * 1200f * t) * 0.4f) * Mathf.Exp(-t * 60f));
            case "melee":
                return Make(name, 0.25f, t => { lp += (Noise() - lp) * 0.12f; return lp * 4f * Mathf.Sin(Mathf.PI * t / 0.25f); });
            case "throw":
                return Make(name, 0.2f, t => { lp += (Noise() - lp) * 0.1f; return lp * 3f * Mathf.Sin(Mathf.PI * t / 0.2f); });
            case "hit":
                return Make(name, 0.2f, t => Sweep(t, 140f, 50f, 0.2f) * Mathf.Exp(-t * 20f) + Noise() * Mathf.Exp(-t * 60f) * 0.4f);
            case "bolt":
                return Make(name, 0.3f, t => Sweep(t, 1100f, 260f, 0.3f) * Mathf.Exp(-t * 7f) * 0.5f * (1f + 0.4f * Mathf.Sin(TwoPi * 40f * t)));
            case "bounce":
                return Make(name, 0.1f, t => (Mathf.Sin(TwoPi * 600f * t) * 0.5f + Noise() * 0.2f) * Mathf.Exp(-t * 50f));
            case "explosion":
                return Make(name, 1.4f, t => { lp += (Noise() - lp) * 0.08f; return lp * 4f * Mathf.Exp(-t * 2.5f) + Sweep(t, 70f, 25f, 1.4f) * Mathf.Exp(-t * 3f) * 0.9f; });
            case "edie":
                return Make(name, 0.5f, t => { lp += (Noise() - lp) * 0.3f; return lp * 1.5f * Mathf.Exp(-t * 6f) + Sweep(t, 400f, 80f, 0.5f) * Mathf.Exp(-t * 8f) * 0.6f; });
            case "hurt":
                return Make(name, 0.25f, t => Mathf.Sin(TwoPi * 90f * t) * Mathf.Exp(-t * 14f) * 0.8f + Noise() * Mathf.Exp(-t * 40f) * 0.4f);
            case "shielddown":
                return Make(name, 0.8f, t => Sweep(t, 900f, 200f, 0.8f) * (0.5f + 0.5f * Mathf.Sin(TwoPi * 12f * t)) * Mathf.Exp(-t * 2f) * 0.5f);
            case "recharge":
                return Make(name, 1f, t => Sweep(t, 300f, 1000f, 1f) * Mathf.Sin(Mathf.PI * t) * 0.3f);
            case "pickup":
                return Make(name, 0.4f, t =>
                {
                    float a = Mathf.Sin(TwoPi * 880f * t) * Mathf.Exp(-t * 12f);
                    float d = t - 0.08f;
                    return (a + (d > 0f ? Mathf.Sin(TwoPi * 1320f * d) * Mathf.Exp(-d * 10f) : 0f)) * 0.5f;
                });
            case "wave":
                return Make(name, 1.2f, t =>
                {
                    float env = Mathf.Min(t / 0.1f, 1f) * Mathf.Exp(-t * 1.5f) * 0.5f;
                    return (Mathf.Sin(TwoPi * 110f * t) + 0.5f * Mathf.Sin(TwoPi * 220f * t) + 0.3f * Mathf.Sin(TwoPi * 330f * t) + 0.5f * Mathf.Sin(TwoPi * 165f * t)) * env;
                });
            case "clear":
                return Make(name, 1f, t =>
                {
                    float s = 0f; float[] notes = { 440f, 554.4f, 659.3f, 880f };
                    for (int i = 0; i < notes.Length; i++) { float d = t - i * 0.15f; if (d >= 0f) s += Mathf.Sin(TwoPi * notes[i] * d) * Mathf.Exp(-d * 5f); }
                    return s * 0.3f;
                });
            case "dmr":
                return Make(name, 0.25f, t => Noise() * Mathf.Exp(-t * 45f) * 0.7f + Sweep(t, 180f, 50f, 0.25f) * Mathf.Exp(-t * 22f) * 0.8f);
            case "pistol":
                return Make(name, 0.3f, t => Noise() * Mathf.Exp(-t * 55f) * 0.9f + Sweep(t, 260f, 70f, 0.3f) * Mathf.Exp(-t * 18f) * 0.8f);
            case "sniper":
                return Make(name, 1.0f, t => { lp += (Noise() - lp) * 0.35f; return lp * 2f * Mathf.Exp(-t * 7f) + Sweep(t, 150f, 30f, 1f) * Mathf.Exp(-t * 6f) * 0.9f; });
            case "sword":
                return Make(name, 0.5f, t => (Mathf.Sin(TwoPi * 220f * t) * 0.4f + Mathf.Sin(TwoPi * 331f * t) * 0.3f + Noise() * 0.15f) * Mathf.Sin(Mathf.PI * Mathf.Min(t / 0.5f, 1f)) * (1f + 0.5f * Mathf.Sin(TwoPi * 30f * t)));
            case "concussion":
                return Make(name, 0.5f, t => Sweep(t, 160f, 40f, 0.5f) * Mathf.Exp(-t * 6f) * 0.9f + Noise() * Mathf.Exp(-t * 30f) * 0.3f);
            case "mortar":
                return Make(name, 0.7f, t => Sweep(t, 90f, 28f, 0.7f) * Mathf.Exp(-t * 4f) * 1.0f);
            case "armorlock":
                return Make(name, 0.6f, t => (Sweep(t, 1200f, 140f, 0.6f) * 0.5f + Noise() * 0.1f) * Mathf.Exp(-t * 4f));
            case "phantom":
                return Make(name, 2.5f, t => (Mathf.Sin(TwoPi * 55f * t) + 0.6f * Mathf.Sin(TwoPi * 82f * t)) * 0.35f * Mathf.Sin(Mathf.PI * t / 2.5f));
            case "death":
                return Make(name, 3f, t => { float e = Mathf.Exp(-t * 1.2f); return (Sweep(t, 220f, 40f, 3f) * 0.5f + Mathf.Sin(TwoPi * 55f * t) * 0.3f) * e; });
            case "music":
                return BuildMusic();
        }
        return Make(name, 0.05f, t => 0f);
    }

    // Somber requiem-style loop: very slow, D minor. A deep drone, swelling string pads, a sparse falling piano line
    // and a long echo - no drums, no pulse, lots of space.
    const int MRate = 22050;   // music is low-frequency; half rate halves generation time
    static AudioClip BuildMusic()
    {
        const float bpm = 48f;
        float beat = 60f / bpm, bar = beat * 4f;
        const int bars = 8;
        int n = (int)(bar * bars * MRate);
        var data = new float[n];

        // Dm | Bb | Gm | A | Dm | Bb | F | A   (chord tones, mid register)
        float[][] chords =
        {
            new[] { 146.8f, 220f, 293.7f, 349.2f }, new[] { 116.5f, 174.6f, 233.1f, 293.7f }, new[] { 98f, 146.8f, 233.1f, 293.7f }, new[] { 110f, 164.8f, 220f, 277.2f },
            new[] { 146.8f, 220f, 293.7f, 349.2f }, new[] { 116.5f, 174.6f, 233.1f, 293.7f }, new[] { 87.3f, 130.8f, 220f, 261.6f }, new[] { 110f, 164.8f, 220f, 277.2f },
        };
        // Sparse piano: (bar, beat, frequency) - a slow, sighing descent
        float[][] notes =
        {
            new[] { 0f, 0f, 587.3f }, new[] { 0f, 2f, 523.3f }, new[] { 1f, 0f, 466.2f }, new[] { 1f, 3f, 440f },
            new[] { 2f, 0f, 392f }, new[] { 2f, 2f, 440f }, new[] { 3f, 0f, 554.4f }, new[] { 3f, 2f, 440f },
            new[] { 4f, 0f, 587.3f }, new[] { 4f, 3f, 698.5f }, new[] { 5f, 0f, 622.3f }, new[] { 5f, 2f, 587.3f },
            new[] { 6f, 0f, 523.3f }, new[] { 6f, 2f, 440f }, new[] { 7f, 0f, 440f }, new[] { 7f, 3f, 293.7f },
        };

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)MRate;
            int b = Mathf.Min((int)(t / bar), bars - 1);
            float inBar = t - b * bar;
            float s = 0f;

            // Drone: low D with a slow breath
            float breath = 0.75f + 0.25f * Mathf.Sin(TwoPi * t / (bar * 2f));
            s += (Mathf.Sin(TwoPi * 36.7f * t) * 0.14f + Mathf.Sin(TwoPi * 73.4f * t) * 0.08f) * breath;

            // Strings: slow swell in and out of each chord, with a gentle shimmer
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * inBar / bar), 1.5f);
            float shimmer = 1f + 0.003f * Mathf.Sin(TwoPi * 4.6f * t);
            foreach (float f in chords[b])
                s += (Mathf.Sin(TwoPi * f * shimmer * t) + 0.35f * Mathf.Sin(TwoPi * f * 2f * t + 0.5f) + 0.15f * Mathf.Sin(TwoPi * f * 3f * t)) * 0.035f * env;
            // Faint high choir-like layer
            s += Mathf.Sin(TwoPi * chords[b][2] * 2f * t) * 0.012f * env;

            data[i] = s;
        }

        // Piano-like notes: soft attack, long decay, bell-ish overtones
        foreach (var nt in notes)
        {
            float start = ((int)nt[0] * 4 + nt[1]) * beat;
            int i0 = (int)(start * MRate), len = (int)(5f * MRate);
            for (int k = 0; k < len; k++)
            {
                int idx = (i0 + k) % n;           // wrap so the tail rings into the next loop
                float t = k / (float)MRate;
                float e = Mathf.Min(t / 0.01f, 1f) * Mathf.Exp(-t * 0.9f);
                float f = nt[2];
                data[idx] += (Mathf.Sin(TwoPi * f * t) + 0.4f * Mathf.Sin(TwoPi * f * 2f * t) * Mathf.Exp(-t * 2f) + 0.15f * Mathf.Sin(TwoPi * f * 3.01f * t) * Mathf.Exp(-t * 4f)) * 0.11f * e;
            }
        }

        // Cavernous echo (loop-safe): two taps wrapping around the end
        var outp = new float[n];
        int d1 = (int)(0.43f * MRate), d2 = (int)(0.97f * MRate), d3 = (int)(1.61f * MRate);
        for (int i = 0; i < n; i++)
            outp[i] = Mathf.Clamp(data[i] + 0.38f * data[(i - d1 + n) % n] + 0.26f * data[(i - d2 + n) % n] + 0.16f * data[(i - d3 + n) % n], -1f, 1f);

        var clip = AudioClip.Create("music", n, 1, MRate, false);
        clip.SetData(outp, 0);
        return clip;
    }
}
