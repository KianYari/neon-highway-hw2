using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Makes the textures and the sounds from code, so the project has no outside files.
public static class AssetFactory
{
    const int SampleRate = 44100;

    // ---------------------------------------------------------------- textures

    public static Texture2D SaveTexture(string path, Texture2D tex, TextureImporterType type, bool mipmaps,
        TextureWrapMode wrap, FilterMode filter, bool alphaIsTransparency = false)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = type;
        importer.mipmapEnabled = mipmaps;
        importer.wrapMode = wrap;
        importer.filterMode = filter;
        importer.alphaIsTransparency = alphaIsTransparency;
        if (type == TextureImporterType.Sprite) importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static Texture2D NightSky(string path)
    {
        const int w = 2048, h = 1024;
        Color32[] px = new Color32[w * h];
        System.Random rng = new System.Random(7);

        Color top = new Color(0.006f, 0.01f, 0.05f);
        Color mid = new Color(0.03f, 0.03f, 0.12f);
        Color glow = new Color(0.35f, 0.08f, 0.4f);
        Color below = new Color(0.02f, 0.02f, 0.06f);

        float moonLon = 0.5f, moonLat = 0.52f;

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float elevation = (v - 0.5f) * Mathf.PI;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                Color c;
                if (elevation < 0f)
                {
                    c = Color.Lerp(mid, below, Mathf.Clamp01(-elevation * 3f));
                }
                else
                {
                    float t = Mathf.Clamp01(elevation / (Mathf.PI * 0.5f));
                    c = Color.Lerp(mid, top, Mathf.Pow(t, 0.5f));
                    c += glow * Mathf.Exp(-elevation * 9f) * 0.6f;
                }

                float du = Mathf.Abs(u - moonLon);
                if (du > 0.5f) du = 1f - du;
                float dx = du * 2f * Mathf.PI * Mathf.Cos(Mathf.Clamp(elevation, -1.4f, 1.4f));
                float dy = elevation - (moonLat - 0.5f) * Mathf.PI;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 0.06f) c = Color.Lerp(new Color(0.85f, 0.9f, 1f), new Color(0.7f, 0.78f, 0.95f), d / 0.06f * 0.5f);
                else c += new Color(0.25f, 0.3f, 0.5f) * Mathf.Exp(-(d - 0.06f) * 14f) * 0.5f;

                px[y * w + x] = c;
            }
        }

        for (int i = 0; i < 4200; i++)
        {
            int sx = rng.Next(w);
            int sy = rng.Next((int)(h * 0.52f), h - 2);
            float b = (float)rng.NextDouble();
            b = 0.25f + b * b * 0.75f;
            Color tint = Color.Lerp(Color.white, new Color(0.7f, 0.8f, 1f), (float)rng.NextDouble());
            AddStar(px, w, sx, sy, tint * b);
            if (b > 0.8f)
            {
                AddStar(px, w, sx + 1, sy, tint * b * 0.5f);
                AddStar(px, w, sx - 1, sy, tint * b * 0.5f);
                AddStar(px, w, sx, sy + 1, tint * b * 0.5f);
                AddStar(px, w, sx, sy - 1, tint * b * 0.5f);
            }
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels32(px);
        return SaveTexture(path, tex, TextureImporterType.Default, true, TextureWrapMode.Repeat, FilterMode.Bilinear);
    }

    static void AddStar(Color32[] px, int w, int x, int y, Color c)
    {
        int h = px.Length / w;
        x = ((x % w) + w) % w;
        if (y < 0 || y >= h) return;
        Color old = px[y * w + x];
        px[y * w + x] = new Color(Mathf.Min(1f, old.r + c.r), Mathf.Min(1f, old.g + c.g), Mathf.Min(1f, old.b + c.b), 1f);
    }

    // Lit and unlit window textures, 8 x 8 windows per tile.
    public static void WindowTextures(string wallPath, string glowPath, out Texture2D wall, out Texture2D glow)
    {
        const int size = 128, cell = 16;
        Color32[] wallPx = new Color32[size * size];
        Color32[] glowPx = new Color32[size * size];
        System.Random rng = new System.Random(21);

        Color[] palette =
        {
            new Color(1f, 0.78f, 0.4f), new Color(1f, 0.85f, 0.55f), new Color(0.45f, 0.9f, 1f),
            new Color(1f, 0.4f, 0.8f), new Color(0.7f, 0.6f, 1f), new Color(1f, 1f, 1f),
        };

        for (int i = 0; i < wallPx.Length; i++)
        {
            float n = 0.9f + (float)rng.NextDouble() * 0.2f;
            wallPx[i] = new Color(0.07f * n, 0.08f * n, 0.13f * n, 1f);
            glowPx[i] = Color.black;
        }

        for (int cy = 0; cy < 8; cy++)
        {
            for (int cx = 0; cx < 8; cx++)
            {
                bool lit = rng.NextDouble() < 0.42;
                Color litColor = palette[rng.Next(palette.Length)] * (0.55f + (float)rng.NextDouble() * 0.45f);
                for (int y = 3; y < cell - 3; y++)
                {
                    for (int x = 3; x < cell - 3; x++)
                    {
                        int idx = (cy * cell + y) * size + cx * cell + x;
                        wallPx[idx] = lit ? new Color(0.25f, 0.22f, 0.2f) : new Color(0.03f, 0.04f, 0.07f);
                        if (lit) glowPx[idx] = litColor;
                    }
                }
            }
        }

        Texture2D w = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        w.SetPixels32(wallPx);
        Texture2D g = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        g.SetPixels32(glowPx);

        wall = SaveTexture(wallPath, w, TextureImporterType.Default, true, TextureWrapMode.Repeat, FilterMode.Bilinear);
        glow = SaveTexture(glowPath, g, TextureImporterType.Default, true, TextureWrapMode.Repeat, FilterMode.Bilinear);
    }

    // White soft dot. The alpha channel carries the falloff.
    public static Texture2D GlowDot(string path)
    {
        const int s = 64;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f;
                float dy = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        }
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        return SaveTexture(path, tex, TextureImporterType.Default, false, TextureWrapMode.Clamp, FilterMode.Bilinear, true);
    }

    public static Sprite WhiteSprite(string path)
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        SaveTexture(path, tex, TextureImporterType.Sprite, false, TextureWrapMode.Clamp, FilterMode.Bilinear, true);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Sprite HeartSprite(string path)
    {
        const int s = 96;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float fx = ((x + 0.5f) / s * 2f - 1f) * 1.25f;
                float fy = ((y + 0.5f) / s * 2f - 1f) * 1.25f + 0.2f;
                float a = fx * fx + fy * fy - 1f;
                float f = a * a * a - fx * fx * fy * fy * fy;
                float alpha = Mathf.Clamp01(0.5f - f * 12f);
                px[y * s + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        SaveTexture(path, tex, TextureImporterType.Sprite, false, TextureWrapMode.Clamp, FilterMode.Bilinear, true);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Sprite CoinSprite(string path)
    {
        const int s = 64;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f;
                float dy = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((0.96f - r) * 20f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.7f) * 14f);
                float shade = 1f - ring * 0.45f;
                px[y * s + x] = new Color(shade, shade, shade, alpha);
            }
        }
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        SaveTexture(path, tex, TextureImporterType.Sprite, false, TextureWrapMode.Clamp, FilterMode.Bilinear, true);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Rounded rectangle for 9-slice buttons. Filled or just the border.
    public static Sprite RoundedRect(string path, bool border)
    {
        const int s = 64;
        const float radius = 18f;
        const float thickness = 3f;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float px0 = x + 0.5f - s * 0.5f;
                float py0 = y + 0.5f - s * 0.5f;
                float qx = Mathf.Abs(px0) - (s * 0.5f - radius);
                float qy = Mathf.Abs(py0) - (s * 0.5f - radius);
                float dist = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                             + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float alpha = border
                    ? Mathf.Clamp01(0.5f - dist) * Mathf.Clamp01(dist + thickness + 0.5f)
                    : Mathf.Clamp01(0.5f - dist);
                px[y * s + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        SaveTexture(path, tex, TextureImporterType.Sprite, false, TextureWrapMode.Clamp, FilterMode.Bilinear, true);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.spriteBorder = new Vector4(20, 20, 20, 20);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ------------------------------------------------------------------ audio

    public static AudioClip SaveClip(string path, float[] samples)
    {
        float peak = 0.0001f;
        foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
        float gain = 0.9f / peak;

        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            int dataSize = samples.Length * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(SampleRate);
            bw.Write(SampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
            foreach (float s in samples) bw.Write((short)(Mathf.Clamp(s * gain, -1f, 1f) * 32767f));
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

    static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
    static float Square(float phase, float duty) => (phase - Mathf.Floor(phase)) < duty ? 1f : -1f;
    static float Tri(float phase) => 2f * Mathf.Abs(Saw(phase)) - 1f;

    public static float[] Music()
    {
        const float bpm = 112f;
        float beat = 60f / bpm;
        float bar = beat * 4f;
        int bars = 8;
        float[] buf = Buffer(bar * bars);
        int n = buf.Length;
        System.Random rng = new System.Random(3);

        // A minor, F, C, G. Two bars each.
        float[][] chords =
        {
            new[] { 220f, 261.63f, 329.63f },
            new[] { 174.61f, 220f, 261.63f },
            new[] { 261.63f, 329.63f, 392f },
            new[] { 196f, 246.94f, 293.66f },
        };
        float[] roots = { 55f, 43.65f, 65.41f, 49f };

        for (int b = 0; b < bars; b++)
        {
            int ci = b / 2;
            float t0 = b * bar;

            // pad
            for (int i = 0; i < Mathf.CeilToInt(bar * SampleRate); i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((bar - t) / 0.4f);
                float s = 0f;
                foreach (float f in chords[ci])
                {
                    s += Saw(f * 0.5f * t + 0.1f) * 0.5f + Saw(f * 0.5f * 1.006f * t) * 0.5f;
                }
                int idx = (Mathf.RoundToInt(t0 * SampleRate) + i) % n;
                buf[idx] += s * env * 0.035f;
            }

            for (int e = 0; e < 8; e++)
            {
                float te = t0 + e * beat * 0.5f;
                // bass
                AddNote(buf, te, beat * 0.5f, roots[ci], 0.34f, 0, 14f);
                // hat on the off beats
                if (e % 2 == 1) AddNoise(buf, te, 0.05f, 0.1f, 55f, rng, true);
            }

            for (int s = 0; s < 16; s++)
            {
                float ts = t0 + s * beat * 0.25f;
                float[] chord = chords[ci];
                int pattern = new[] { 0, 1, 2, 1, 2, 1, 0, 1 }[s % 8];
                AddNote(buf, ts, beat * 0.25f, chord[pattern] * 2f, 0.07f, 1, 16f);
            }

            for (int q = 0; q < 4; q++)
            {
                float tq = t0 + q * beat;
                AddKick(buf, tq, 0.55f);
                if (q % 2 == 1) AddNoise(buf, tq, 0.16f, 0.22f, 22f, rng, false);
            }
        }

        return buf;
    }

    // wave 0 = saw (bass), 1 = pulse (lead)
    static void AddNote(float[] buf, float start, float length, float freq, float amp, int wave, float decay)
    {
        int n = buf.Length;
        int begin = Mathf.RoundToInt(start * SampleRate);
        int count = Mathf.CeilToInt(length * 1.4f * SampleRate);
        float phase = 0f;
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            phase += freq / SampleRate;
            float s = wave == 0 ? Saw(phase) : Square(phase, 0.3f);
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t / 0.004f);
            low += (s - low) * (wave == 0 ? 0.12f : 0.35f);
            buf[(begin + i) % n] += low * env * amp;
        }
    }

    static void AddKick(float[] buf, float start, float amp)
    {
        int n = buf.Length;
        int begin = Mathf.RoundToInt(start * SampleRate);
        int count = Mathf.CeilToInt(0.3f * SampleRate);
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float f = 42f + 110f * Mathf.Exp(-t * 28f);
            phase += f / SampleRate;
            buf[(begin + i) % n] += Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 11f) * amp;
        }
    }

    static void AddNoise(float[] buf, float start, float length, float amp, float decay, System.Random rng, bool highPass)
    {
        int n = buf.Length;
        int begin = Mathf.RoundToInt(start * SampleRate);
        int count = Mathf.CeilToInt(length * SampleRate);
        float prev = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float r = (float)rng.NextDouble() * 2f - 1f;
            float s = highPass ? r - prev : r;
            prev = r;
            buf[(begin + i) % n] += s * Mathf.Exp(-t * decay) * amp;
        }
    }

    // One second, 55 Hz and its harmonics, so it loops with no click.
    public static float[] Engine()
    {
        float[] buf = Buffer(1f);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SampleRate;
            float s = 0f;
            for (int k = 1; k <= 14; k++)
                s += Mathf.Sin(2f * Mathf.PI * 55f * k * t + k * 0.7f) / Mathf.Pow(k, 0.85f);
            float rough = 1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 11f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 33f * t);
            buf[i] = s * rough;
        }
        return buf;
    }

    public static float[] Crash()
    {
        float[] buf = Buffer(0.9f);
        System.Random rng = new System.Random(11);
        float low = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            low += (noise - low) * Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(t * 3f));
            float thump = Mathf.Sin(2f * Mathf.PI * (60f + 40f * Mathf.Exp(-t * 20f)) * t) * Mathf.Exp(-t * 7f);
            buf[i] = low * Mathf.Exp(-t * 5f) * 0.9f + thump * 0.9f;
        }
        return buf;
    }

    public static float[] Coin()
    {
        float[] buf = Buffer(0.4f);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SampleRate;
            float f = t < 0.07f ? 988f : 1319f;
            float local = t < 0.07f ? t : t - 0.07f;
            buf[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t)) * Mathf.Exp(-local * 9f) * Mathf.Clamp01(t / 0.002f);
        }
        return buf;
    }

    public static float[] Fuel()
    {
        float[] buf = Buffer(0.5f);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SampleRate;
            float f = 280f + 700f * Mathf.Min(t / 0.35f, 1f);
            phase += f / SampleRate;
            float trem = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 22f * t);
            buf[i] = Tri(phase) * trem * Mathf.Exp(-Mathf.Max(0f, t - 0.25f) * 14f) * Mathf.Clamp01(t / 0.01f);
        }
        return buf;
    }

    public static float[] LowFuel()
    {
        float[] buf = Buffer(0.18f);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SampleRate;
            phase += 740f / SampleRate;
            buf[i] = Square(phase, 0.5f) * 0.4f * Mathf.Exp(-t * 14f) * Mathf.Clamp01(t / 0.004f);
        }
        return buf;
    }

    public static float[] Firework()
    {
        float[] buf = Buffer(1.4f);
        System.Random rng = new System.Random(5);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SampleRate;
            float pop = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 16f);
            buf[i] = pop * 0.9f;
        }
        for (int c = 0; c < 70; c++)
        {
            float start = 0.12f + (float)rng.NextDouble() * 1.0f;
            AddNoise(buf, start, 0.012f, 0.25f * (1f - start / 1.4f), 200f, rng, true);
        }
        return buf;
    }

    public static float[] GameOver()
    {
        float[] notes = { 440f, 349.23f, 293.66f, 220f };
        float[] buf = Buffer(1.3f);
        for (int n = 0; n < notes.Length; n++)
        {
            float start = n * 0.24f;
            float phase = 0f;
            int begin = Mathf.RoundToInt(start * SampleRate);
            for (int i = 0; begin + i < buf.Length && i < 0.45f * SampleRate; i++)
            {
                float t = i / (float)SampleRate;
                phase += notes[n] / SampleRate;
                buf[begin + i] += (Tri(phase) * 0.8f + Saw(phase) * 0.2f) * Mathf.Exp(-t * 4f);
            }
        }
        return buf;
    }

    public static float[] LevelUp()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        float[] buf = Buffer(0.6f);
        for (int n = 0; n < notes.Length; n++)
        {
            int begin = Mathf.RoundToInt(n * 0.08f * SampleRate);
            float phase = 0f;
            for (int i = 0; begin + i < buf.Length; i++)
            {
                float t = i / (float)SampleRate;
                phase += notes[n] / SampleRate;
                buf[begin + i] += Square(phase, 0.5f) * 0.3f * Mathf.Exp(-t * 7f);
            }
        }
        return buf;
    }

    public static float[] Click()
    {
        float[] buf = Buffer(0.07f);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SampleRate;
            buf[i] = Mathf.Sin(2f * Mathf.PI * 760f * t) * Mathf.Exp(-t * 55f);
        }
        return buf;
    }
}
