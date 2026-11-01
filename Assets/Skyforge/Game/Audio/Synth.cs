using System;

namespace Skyforge.Game.Audio
{
    public enum Wave : byte { Square, Saw, Triangle, Sine, Noise }

    /// <summary>
    /// One synthesised sound layer, sfxr-style (docs/DESIGN.md §10.2): a waveform whose pitch slides
    /// from <see cref="Freq"/> to <see cref="FreqEnd"/>, shaped by an attack / sustain / decay
    /// envelope, with optional vibrato, an arpeggio jump and a low-pass filter.
    /// </summary>
    public struct Tone
    {
        public Wave Wave;
        public float Freq, FreqEnd;
        /// <summary>Square duty cycle (0..1, 0.5 = even).</summary>
        public float Duty;
        public float Attack, Sustain, Decay;
        /// <summary>Starts this many seconds into the sound.</summary>
        public float Delay;
        public float Volume;
        /// <summary>Vibrato depth (fraction of the pitch) and rate (Hz).</summary>
        public float Vibrato, VibratoRate;
        /// <summary>Jump the pitch by this factor at <see cref="ArpAt"/> seconds (0 = no jump).</summary>
        public float Arp, ArpAt;
        /// <summary>One-pole low-pass cutoff in Hz (0 = off).</summary>
        public float LowPass;

        public float Length => Delay + Attack + Sustain + Decay;

        public static Tone Of(Wave w, float freq, float freqEnd, float attack, float sustain, float decay, float volume = 1f) =>
            new Tone { Wave = w, Freq = freq, FreqEnd = freqEnd, Duty = 0.5f, Attack = attack, Sustain = sustain, Decay = decay, Volume = volume };

        public Tone At(float delay) { Delay = delay; return this; }
        public Tone Lp(float hz) { LowPass = hz; return this; }
        public Tone Vib(float depth, float rate) { Vibrato = depth; VibratoRate = rate; return this; }
        public Tone Jump(float factor, float at) { Arp = factor; ArpAt = at; return this; }
        public Tone WithDuty(float d) { Duty = d; return this; }
    }

    /// <summary>
    /// Plain C# sound generation (no engine types, so it can be tested and previewed outside Unity).
    /// Renders mono float samples in −1..1.
    /// </summary>
    public static class Synth
    {
        public const int SfxRate = 44100;

        /// <summary>Render a sound made of several layers, normalised to <paramref name="peak"/>.</summary>
        public static float[] Render(Tone[] layers, int rate = SfxRate, float peak = 0.9f, uint seed = 1)
        {
            float len = 0f;
            foreach (var t in layers) len = Math.Max(len, t.Length);
            var buf = new float[Math.Max(1, (int)(len * rate) + 1)];
            var rng = new Noise(seed);
            foreach (var t in layers) Add(buf, t, rate, ref rng);
            Normalise(buf, peak);
            return buf;
        }

        static void Add(float[] buf, Tone t, int rate, ref Noise rng)
        {
            int start = (int)(t.Delay * rate);
            int n = (int)((t.Attack + t.Sustain + t.Decay) * rate);
            double phase = 0;
            float lp = 0f, noiseHold = 0f;
            double noisePhase = 0;
            float a = t.LowPass > 0f ? 1f - (float)Math.Exp(-2.0 * Math.PI * t.LowPass / rate) : 1f;
            float dur = Math.Max(1e-4f, t.Attack + t.Sustain + t.Decay);
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                float time = i / (float)rate;
                // Exponential slide between the two pitches.
                float f = t.FreqEnd > 0f && t.FreqEnd != t.Freq ? t.Freq * (float)Math.Pow(t.FreqEnd / t.Freq, time / dur) : t.Freq;
                if (t.Arp > 0f && time >= t.ArpAt) f *= t.Arp;
                if (t.Vibrato > 0f) f *= 1f + t.Vibrato * (float)Math.Sin(2.0 * Math.PI * t.VibratoRate * time);
                phase += f / rate;
                phase -= Math.Floor(phase);
                float p = (float)phase, s;
                switch (t.Wave)
                {
                    case Wave.Square: s = p < t.Duty ? 1f : -1f; break;
                    case Wave.Saw: s = 2f * p - 1f; break;
                    case Wave.Triangle: s = p < 0.5f ? 4f * p - 1f : 3f - 4f * p; break;
                    case Wave.Sine: s = (float)Math.Sin(2.0 * Math.PI * p); break;
                    default:
                        // Noise: a new random value each "cycle", so the pitch colours it.
                        noisePhase += f / rate;
                        if (noisePhase >= 1.0) { noisePhase -= Math.Floor(noisePhase); noiseHold = rng.Next() * 2f - 1f; }
                        s = noiseHold;
                        break;
                }
                lp += (s - lp) * a;
                buf[start + i] += lp * Envelope(t, time) * t.Volume;
            }
        }

        static float Envelope(Tone t, float time)
        {
            if (time < t.Attack) return t.Attack <= 0f ? 1f : time / t.Attack;
            time -= t.Attack;
            if (time < t.Sustain) return 1f;
            time -= t.Sustain;
            if (t.Decay <= 0f) return 0f;
            float k = 1f - time / t.Decay;
            return k <= 0f ? 0f : k * k; // a curved decay sounds more natural than a straight one
        }

        public static void Normalise(float[] buf, float peak)
        {
            float max = 0f;
            foreach (var v in buf) max = Math.Max(max, Math.Abs(v));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        /// <summary>A small seeded noise generator (xorshift), so a sound is the same every run.</summary>
        public struct Noise
        {
            uint _s;
            public Noise(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
