using System;

namespace Skyforge.Game.Audio
{
    /// <summary>A generated music loop's recipe.</summary>
    public sealed class Track
    {
        public string Id;
        public string Name;
        public float Bpm = 120f;
        /// <summary>Root note as a MIDI number (57 = A3).</summary>
        public int Root = 57;
        /// <summary>The scale, as semitones above the root.</summary>
        public int[] Scale = Music.Minor;
        /// <summary>Four chords (scale degrees, 0-based), two bars each; the loop plays them twice.</summary>
        public int[] Chords = { 0, 5, 2, 6 };
        /// <summary>0 = calm (menus), 1 = a stage, 2 = the boss.</summary>
        public int Energy = 1;
        public uint Seed = 1;
    }

    /// <summary>
    /// Synthwave loops generated in code (docs/DESIGN.md §10.2): drums, a plucked bass, a detuned
    /// pad and an echoing arpeggio over a four-chord progression. Each loop is 16 bars: a lighter
    /// first half, then everything. Notes that ring past the end wrap round to the start, so the loop
    /// is seamless. Plain C# (no engine types) so it can be rendered and previewed outside Unity.
    /// </summary>
    public static class Music
    {
        public const int Rate = 32000;
        public const int Bars = 16;

        public static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        public static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        public static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };

        public static readonly Track Hangar = new Track { Id = "hangar", Name = "Skyforge", Bpm = 92f, Root = 57, Scale = Dorian, Chords = new[] { 0, 5, 2, 6 }, Energy = 0, Seed = 11 };
        public static readonly Track[] StageTracks =
        {
            new Track { Id = "1-1", Name = "First Light", Bpm = 112f, Root = 50, Chords = new[] { 0, 5, 2, 6 }, Seed = 21 },
            new Track { Id = "1-2", Name = "Harbor Line", Bpm = 116f, Root = 52, Chords = new[] { 0, 3, 5, 4 }, Seed = 22 },
            new Track { Id = "1-3", Name = "Stormwall", Bpm = 122f, Root = 48, Chords = new[] { 0, 6, 5, 6 }, Seed = 23 },
            new Track { Id = "1-4", Name = "Foundry Ridge", Bpm = 118f, Root = 53, Chords = new[] { 0, 2, 3, 4 }, Seed = 24 },
            new Track { Id = "1-5", Name = "Upper Air", Bpm = 126f, Root = 55, Chords = new[] { 5, 6, 0, 0 }, Seed = 25 },
            new Track { Id = "1-B", Name = "The Tidebreaker", Bpm = 140f, Root = 52, Scale = Phrygian, Chords = new[] { 0, 1, 0, 6 }, Energy = 2, Seed = 26 },
        };

        /// <summary>The track for a stage id (the first stage's for anything else, e.g. test stages).</summary>
        public static Track ForStage(string stageId)
        {
            foreach (var t in StageTracks) if (t.Id == stageId) return t;
            return StageTracks[0];
        }

        static float Midi(int note) => 440f * (float)Math.Pow(2.0, (note - 69) / 12.0);

        /// <summary>The note <paramref name="degree"/> steps up the scale from the root (wraps into higher octaves).</summary>
        static int Note(Track t, int degree, int octave)
        {
            int n = t.Scale.Length;
            int o = degree >= 0 ? degree / n : (degree - n + 1) / n;
            int d = degree - o * n;
            return t.Root + 12 * (octave + o) + t.Scale[d];
        }

        /// <summary>Render the loop (mono, <see cref="Rate"/> Hz, peak about 0.8).</summary>
        public static float[] Render(Track t)
        {
            double stepSec = 60.0 / t.Bpm / 4.0;
            int steps = Bars * 16;
            int len = (int)(steps * stepSec * Rate);
            var buf = new float[len];
            var rng = new Synth.Noise(t.Seed);
            int S(int step) => (int)(step * stepSec * Rate);
            float stepLen = (float)stepSec;

            for (int bar = 0; bar < Bars; bar++)
            {
                bool full = bar >= Bars / 2;
                int chord = t.Chords[(bar / 2) % t.Chords.Length];
                bool fill = bar % 4 == 3;
                for (int k = 0; k < 16; k++)
                {
                    int step = bar * 16 + k;
                    int at = S(step);

                    // ---------------- drums
                    if (t.Energy == 0)
                    {
                        if (full && (k == 0 || k == 8)) Kick(buf, at, 0.5f);
                        if (full && k % 4 == 2) Hat(buf, at, 0.06f, 0.05f, ref rng);
                    }
                    else
                    {
                        if (k % 4 == 0 || (t.Energy == 2 && k == 14) || (fill && full && k == 10)) Kick(buf, at, 0.9f);
                        if ((full || t.Energy == 2) && (k == 4 || k == 12)) Snare(buf, at, 0.5f, ref rng);
                        if (fill && full && k >= 13) Snare(buf, at, 0.25f, ref rng);
                        if (k % 2 == 0) Hat(buf, at, k % 4 == 2 ? 0.14f : 0.08f, k % 4 == 2 ? 0.09f : 0.035f, ref rng);
                        else if (full && t.Energy == 2) Hat(buf, at, 0.05f, 0.03f, ref rng);
                    }

                    // ---------------- bass: root eighths (sixteenths for the boss), octave jumps on the off-beats
                    bool bassHit = t.Energy == 2 ? true : t.Energy == 0 ? k % 8 == 0 : k % 2 == 0;
                    if (bassHit)
                    {
                        int octave = (t.Energy >= 1 && k % 4 == 2) ? 3 : 2;
                        float f = Midi(Note(t, chord, octave - 3));
                        float dur = (t.Energy == 0 ? 7.5f : t.Energy == 2 ? 0.9f : 1.7f) * stepLen;
                        Pluck(buf, at, dur, f, t.Energy == 0 ? 0.32f : 0.38f, t.Energy == 0 ? 500f : 750f);
                    }

                    // ---------------- arpeggio: chord tones climbing in sixteenths, with an echo
                    bool arpOn = t.Energy == 0 ? full : (full || bar % 2 == 1);
                    if (arpOn && (t.Energy > 0 || k % 2 == 0))
                    {
                        int[] shape = { 0, 2, 4, 7, 4, 2, 4, 9 };
                        int deg = chord + shape[(k / (t.Energy == 0 ? 2 : 1)) % shape.Length];
                        float f = Midi(Note(t, deg, 1));
                        float v = t.Energy == 0 ? 0.08f : 0.1f;
                        Lead(buf, at, stepLen * 0.9f, f, v);
                        Lead(buf, at + S(3), stepLen * 0.9f, f, v * 0.35f);
                    }
                }
                // ---------------- pad: the chord held across the bar, slow attack
                if (bar % 2 == 0)
                {
                    int at = S(bar * 16);
                    float dur = 32f * stepLen;
                    for (int i = 0; i < 3; i++)
                    {
                        float f = Midi(Note(t, chord + i * 2, 0));
                        Pad(buf, at, dur, f, t.Energy == 0 ? 0.09f : 0.065f);
                    }
                }
            }

            // Gentle saturation, then normalise.
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * 1.2f);
            Synth.Normalise(buf, 0.8f);
            return buf;
        }

        // ------------------------------------------------------------------ instruments (all wrap round the loop)

        static void Kick(float[] buf, int at, float vol)
        {
            int n = (int)(0.32f * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 45f + 110f * (float)Math.Exp(-t * 30f);
                phase += f / Rate;
                float env = (float)Math.Exp(-t * 9f);
                buf[(at + i) % buf.Length] += (float)Math.Sin(2.0 * Math.PI * phase) * env * vol;
            }
        }

        static void Snare(float[] buf, int at, float vol, ref Synth.Noise rng)
        {
            int n = (int)(0.2f * Rate);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = rng.Next() * 2f - 1f;
                lp += (noise - lp) * 0.5f;
                float tone = (float)Math.Sin(2.0 * Math.PI * 190.0 * t) * (float)Math.Exp(-t * 30f);
                buf[(at + i) % buf.Length] += (lp * (float)Math.Exp(-t * 18f) + tone * 0.5f) * vol;
            }
        }

        static void Hat(float[] buf, int at, float vol, float decay, ref Synth.Noise rng)
        {
            int n = (int)(decay * 3f * Rate);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = rng.Next() * 2f - 1f;
                float hp = noise - prev; // crude high-pass
                prev = noise;
                buf[(at + i) % buf.Length] += hp * (float)Math.Exp(-t / decay) * vol;
            }
        }

        static void Pluck(float[] buf, int at, float dur, float f, float vol, float cutoff)
        {
            int n = (int)((dur + 0.05f) * Rate);
            double p1 = 0, p2 = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                p1 += f / Rate; p1 -= Math.Floor(p1);
                p2 += f * 0.5 / Rate; p2 -= Math.Floor(p2);
                float saw = 2f * (float)p1 - 1f;
                // The filter closes as the note goes on: the pluck.
                float c = cutoff * (0.4f + 2.5f * (float)Math.Exp(-t * 14f));
                float a = 1f - (float)Math.Exp(-2.0 * Math.PI * c / Rate);
                lp += (saw - lp) * a;
                float sub = (float)Math.Sin(2.0 * Math.PI * p2) * 0.6f;
                float env = t < dur ? 1f - 0.3f * t / dur : Math.Max(0f, 1f - (t - dur) / 0.05f) * 0.7f;
                env *= Math.Min(1f, t * 400f);
                buf[(at + i) % buf.Length] += (lp + sub) * env * vol;
            }
        }

        static void Lead(float[] buf, int at, float dur, float f, float vol)
        {
            int n = (int)((dur + 0.12f) * Rate);
            double p = 0;
            float lp = 0f;
            float a = 1f - (float)Math.Exp(-2.0 * Math.PI * 3200.0 / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                p += f / Rate; p -= Math.Floor(p);
                float sq = p < 0.3 ? 1f : -1f;
                lp += (sq - lp) * a;
                float env = (float)Math.Exp(-t * 9f) * Math.Min(1f, t * 300f);
                buf[(at + i) % buf.Length] += lp * env * vol;
            }
        }

        static void Pad(float[] buf, int at, float dur, float f, float vol)
        {
            float attack = 0.6f, release = 0.8f;
            int n = (int)((dur + release) * Rate);
            double p1 = 0, p2 = 0.33, p3 = 0.66;
            float lp = 0f;
            float a = 1f - (float)Math.Exp(-2.0 * Math.PI * 1100.0 / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                p1 += f * 0.997 / Rate; p1 -= Math.Floor(p1);
                p2 += f / Rate; p2 -= Math.Floor(p2);
                p3 += f * 1.003 / Rate; p3 -= Math.Floor(p3);
                float s = (2f * (float)p1 - 1f) + (2f * (float)p2 - 1f) + (2f * (float)p3 - 1f);
                lp += (s / 3f - lp) * a;
                float env = t < attack ? t / attack : t < dur ? 1f : Math.Max(0f, 1f - (t - dur) / release);
                buf[(at + i) % buf.Length] += lp * env * vol;
            }
        }
    }
}
