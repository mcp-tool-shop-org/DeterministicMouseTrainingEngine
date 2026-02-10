using System;
using System.IO;

// Deterministic WAV generator for MouseTrainer.
// Run once, commit the output. Zero runtime dependencies.

const int SampleRate = 44100;

var outDir = args.Length > 0
    ? args[0]
    : Path.Combine("..", "..", "src", "MouseTrainer.MauiHost", "Resources", "Raw");

Directory.CreateDirectory(outDir);

// ── HIT (thud + filtered noise) ──────────────────────────────
WriteWav(Path.Combine(outDir, "sfx_hit_01.wav"), Hit(seed: 1, baseFreq: 90f));
WriteWav(Path.Combine(outDir, "sfx_hit_02.wav"), Hit(seed: 2, baseFreq: 110f));
WriteWav(Path.Combine(outDir, "sfx_hit_03.wav"), Hit(seed: 3, baseFreq: 130f));

// ── GATE (bright chirp) ──────────────────────────────────────
WriteWav(Path.Combine(outDir, "sfx_gate_01.wav"), Gate(650f, 1150f));
WriteWav(Path.Combine(outDir, "sfx_gate_02.wav"), Gate(780f, 1450f));

// ── COMBO (ascending arp) ────────────────────────────────────
WriteWav(Path.Combine(outDir, "sfx_combo_01.wav"), Combo([900, 1200, 1500]));
WriteWav(Path.Combine(outDir, "sfx_combo_02.wav"), Combo([1000, 1330, 1660]));
WriteWav(Path.Combine(outDir, "sfx_combo_03.wav"), Combo([1100, 1460, 1830]));

// ── DRAG ─────────────────────────────────────────────────────
WriteWav(Path.Combine(outDir, "sfx_drag_start.wav"), Click(520f));
WriteWav(Path.Combine(outDir, "sfx_drag_end.wav"), Click(360f));
WriteWav(Path.Combine(outDir, "sfx_drag_loop.wav"), DragLoop(seconds: 1.0));

// ── LEVEL COMPLETE ───────────────────────────────────────────
WriteWav(Path.Combine(outDir, "sfx_level_complete.wav"), LevelComplete());

// ── AMBIENT (loopable 4s pad) ────────────────────────────────
WriteWav(Path.Combine(outDir, "amb_zen_loop.wav"), AmbienceLoop(seconds: 4.0, seed: 123));

Console.WriteLine($"Generated 13 WAV files into: {Path.GetFullPath(outDir)}");

// ═══════════════════════════════════════════════════════════════
//  DSP Generators
// ═══════════════════════════════════════════════════════════════

static float[] Hit(int seed, float baseFreq)
{
    var dur = 0.18f;
    int n = (int)(dur * SampleRate);
    var s = new float[n];

    for (int i = 0; i < n; i++)
    {
        float t = i / (float)SampleRate;
        float env = Adsr(i, n, a: 0.002f, d: 0.05f, sus: 0f, r: 0.12f);

        // Low thud oscillator
        float thud = MathF.Sin(2f * MathF.PI * baseFreq * t) * env * 0.75f;

        // Deterministic filtered noise for texture
        float noise = (Rand(seed, i) * 2f - 1f) * env * 0.35f;

        // Crude low-pass via moving average (3-tap)
        s[i] = thud + noise;
        if (i >= 2)
            s[i] = (s[i] + s[i - 1] + s[i - 2]) / 3f + thud * 0.5f;
    }

    return Normalize(s, 0.9f);
}

static float[] Gate(float f0, float f1)
{
    var dur = 0.12f;
    int n = (int)(dur * SampleRate);
    var s = new float[n];

    for (int i = 0; i < n; i++)
    {
        float t = i / (float)SampleRate;
        // Linear frequency sweep from f0 to f1
        float k = (f1 - f0) / dur;
        float phase = 2f * MathF.PI * (f0 * t + 0.5f * k * t * t);
        float env = Adsr(i, n, a: 0.001f, d: 0.06f, sus: 0f, r: 0.05f);

        s[i] = MathF.Sin(phase) * env;
    }

    return Normalize(s, 0.9f);
}

static float[] Combo(int[] freqs)
{
    float noteDur = 0.06f;
    float total = freqs.Length * noteDur;
    int n = (int)(total * SampleRate);
    var s = new float[n];

    for (int note = 0; note < freqs.Length; note++)
    {
        int start = (int)(note * noteDur * SampleRate);
        int end = Math.Min(start + (int)(noteDur * SampleRate), n);
        int noteLen = end - start;

        for (int i = start; i < end; i++)
        {
            float t = (i - start) / (float)SampleRate;
            float env = Adsr(i - start, noteLen, a: 0.001f, d: 0.03f, sus: 0f, r: 0.03f);
            s[i] += MathF.Sin(2f * MathF.PI * freqs[note] * t) * env;
        }
    }

    return Normalize(s, 0.9f);
}

static float[] Click(float baseFreq)
{
    var dur = 0.05f;
    int n = (int)(dur * SampleRate);
    var s = new float[n];

    for (int i = 0; i < n; i++)
    {
        float t = i / (float)SampleRate;
        float env = Adsr(i, n, a: 0.0005f, d: 0.02f, sus: 0f, r: 0.02f);
        // Tiny chirp: rising frequency
        float phase = 2f * MathF.PI * (baseFreq * t + 0.5f * baseFreq * t * t);
        s[i] = MathF.Sin(phase) * env;
    }

    return Normalize(s, 0.9f);
}

static float[] DragLoop(double seconds)
{
    int n = (int)(seconds * SampleRate);
    var s = new float[n];

    for (int i = 0; i < n; i++)
    {
        float t = i / (float)SampleRate;
        // Warm hum: fundamental + octave
        float hum = MathF.Sin(2f * MathF.PI * 110f * t) * 0.35f
                   + MathF.Sin(2f * MathF.PI * 220f * t) * 0.15f;

        // Gentle tremolo
        float trem = 0.6f + 0.4f * MathF.Sin(2f * MathF.PI * 0.5f * t);
        s[i] = hum * trem;
    }

    // Crossfade edges for seamless loop
    int fade = (int)(0.05 * SampleRate);
    for (int i = 0; i < fade; i++)
    {
        float a = i / (float)fade;
        s[i] *= a;
        s[n - 1 - i] *= a;
    }

    return Normalize(s, 0.6f);
}

static float[] LevelComplete()
{
    var dur = 0.7f;
    int n = (int)(dur * SampleRate);
    var s = new float[n];

    for (int i = 0; i < n; i++)
    {
        float t = i / (float)SampleRate;
        // Major triad: A4 + C#5 + E5
        float chord = MathF.Sin(2f * MathF.PI * 440f * t)
                     + MathF.Sin(2f * MathF.PI * 554f * t)
                     + MathF.Sin(2f * MathF.PI * 659f * t);
        chord *= 0.33f;

        float env = Adsr(i, n, a: 0.02f, d: 0.22f, sus: 0.2f, r: 0.25f);
        s[i] = chord * env;
    }

    return Normalize(s, 0.9f);
}

static float[] AmbienceLoop(double seconds, int seed)
{
    int n = (int)(seconds * SampleRate);
    var s = new float[n];

    for (int i = 0; i < n; i++)
    {
        float t = i / (float)SampleRate;
        // Deep sub-bass pad with slow LFO
        float lfo = 0.7f + 0.3f * MathF.Sin(2f * MathF.PI * 0.25f * t);
        float pad = MathF.Sin(2f * MathF.PI * 55f * t) * 0.12f
                   + MathF.Sin(2f * MathF.PI * 110f * t) * 0.06f
                   + MathF.Sin(2f * MathF.PI * 82.5f * t) * 0.04f; // fifth

        // Subtle deterministic texture
        float nse = (Rand(seed, i) * 2f - 1f) * 0.03f;

        s[i] = (pad + nse) * lfo;
    }

    // Crossfade for seamless loop (200ms each end)
    int fade = (int)(0.2 * SampleRate);
    for (int i = 0; i < fade; i++)
    {
        float a = i / (float)fade;
        s[i] *= a;
        s[n - 1 - i] *= a;
    }

    return Normalize(s, 0.5f);
}

// ═══════════════════════════════════════════════════════════════
//  Utilities
// ═══════════════════════════════════════════════════════════════

static float Adsr(int i, int n, float a, float d, float sus, float r)
{
    int A = (int)(a * SampleRate);
    int D = (int)(d * SampleRate);
    int R = (int)(r * SampleRate);
    int S = Math.Max(0, n - (A + D + R));

    if (i < A) return i / (float)Math.Max(1, A);
    i -= A;

    if (i < D) return Lerp(1f, sus, i / (float)Math.Max(1, D));
    i -= D;

    if (i < S) return sus;
    i -= S;

    return Lerp(sus, 0f, i / (float)Math.Max(1, R));
}

static float Lerp(float a, float b, float t) => a + (b - a) * t;

static float Rand(int seed, int i)
{
    unchecked
    {
        uint x = (uint)(seed * 73856093) ^ (uint)(i * 19349663);
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & 0xFFFFFF) / (float)0x1000000;
    }
}

static float[] Normalize(float[] s, float peak)
{
    float max = 0f;
    for (int i = 0; i < s.Length; i++)
        max = MathF.Max(max, MathF.Abs(s[i]));

    if (max < 1e-6f) return s;

    float k = peak / max;
    for (int i = 0; i < s.Length; i++)
        s[i] *= k;

    return s;
}

static void WriteWav(string path, float[] samples)
{
    using var bw = new BinaryWriter(File.Create(path));

    int dataLen = samples.Length * 2; // 16-bit PCM
    int fmtLen = 16;
    int riffLen = 4 + (8 + fmtLen) + (8 + dataLen);

    // RIFF header
    bw.Write("RIFF"u8.ToArray());
    bw.Write(riffLen);
    bw.Write("WAVE"u8.ToArray());

    // fmt chunk
    bw.Write("fmt "u8.ToArray());
    bw.Write(fmtLen);
    bw.Write((short)1);           // PCM
    bw.Write((short)1);           // Mono
    bw.Write(SampleRate);         // Sample rate
    bw.Write(SampleRate * 2);     // Byte rate (mono 16-bit)
    bw.Write((short)2);           // Block align
    bw.Write((short)16);          // Bits per sample

    // data chunk
    bw.Write("data"u8.ToArray());
    bw.Write(dataLen);

    for (int i = 0; i < samples.Length; i++)
    {
        short v = (short)Math.Clamp((int)(samples[i] * 32767), short.MinValue, short.MaxValue);
        bw.Write(v);
    }
}
