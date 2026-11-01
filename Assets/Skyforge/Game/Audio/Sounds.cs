namespace Skyforge.Game.Audio
{
    /// <summary>Every sound effect in the game.</summary>
    public enum Sfx : byte
    {
        ShotPulse, ShotScatter, ShotLance, ShotSeeker, ShotHornet, ShotArc, ShotDisc,
        /// <summary>The Sunline Beam's hum (loops while a beam fires).</summary>
        BeamHum,
        Hit, Deflect, ExplodeSmall, ExplodeBig, ExplodeBoss, Blast, MineBurst, EnemyShot,
        Credit, Repair, ShieldHit, HullHit, PlayerDown,
        CoreNova, CoreLance, CoreAegis, CoreWingmates, CoreReady,
        Warning, Laser, Click, Buy, Clear, Fail, Medal,
    }

    /// <summary>
    /// The sound designs: each effect is a few synthesised <see cref="Tone"/> layers
    /// (docs/DESIGN.md §10.2). Player weapons sit in a soft, high register and enemy sounds lower and
    /// harsher, so you can hear what's yours. Volumes here are relative; the mixer scales them.
    /// </summary>
    public static class Sounds
    {
        public const int Count = (int)Sfx.Medal + 1;

        /// <summary>How loud each effect plays (0..1), before the settings' volume.</summary>
        public static float Gain(Sfx s)
        {
            switch (s)
            {
                case Sfx.ShotPulse: case Sfx.ShotHornet: case Sfx.ShotScatter: return 0.18f;
                case Sfx.ShotLance: case Sfx.ShotDisc: case Sfx.ShotArc: return 0.24f;
                case Sfx.ShotSeeker: return 0.28f;
                case Sfx.BeamHum: return 0.22f;
                case Sfx.Hit: return 0.2f;
                case Sfx.Deflect: return 0.2f;
                case Sfx.EnemyShot: return 0.16f;
                case Sfx.Credit: return 0.25f;
                case Sfx.Click: return 0.35f;
                case Sfx.ExplodeSmall: return 0.45f;
                case Sfx.Blast: return 0.35f;
                default: return 0.7f;
            }
        }

        /// <summary>Least time between two plays of the same effect (stops a wall of noise).</summary>
        public static float MinGap(Sfx s)
        {
            switch (s)
            {
                case Sfx.ShotPulse: case Sfx.ShotScatter: case Sfx.ShotHornet: case Sfx.ShotDisc: return 0.07f;
                case Sfx.ShotLance: case Sfx.ShotArc: case Sfx.ShotSeeker: return 0.1f;
                case Sfx.Hit: case Sfx.Deflect: return 0.06f;
                case Sfx.EnemyShot: return 0.12f;
                case Sfx.Credit: return 0.045f;
                case Sfx.ExplodeSmall: return 0.05f;
                case Sfx.Blast: return 0.08f;
                default: return 0.02f;
            }
        }

        static Tone T(Wave w, float f, float f2, float a, float s, float d, float v = 1f) => Tone.Of(w, f, f2, a, s, d, v);

        public static Tone[] Design(Sfx s)
        {
            switch (s)
            {
                // ---------------------------------------------------------- player weapons
                case Sfx.ShotPulse:
                    return new[] { T(Wave.Square, 1250f, 620f, 0f, 0.015f, 0.06f).WithDuty(0.25f).Lp(5000f), T(Wave.Sine, 2500f, 1800f, 0f, 0f, 0.04f, 0.3f) };
                case Sfx.ShotScatter:
                    return new[] { T(Wave.Noise, 5000f, 1500f, 0f, 0.01f, 0.07f, 0.7f).Lp(4000f), T(Wave.Square, 520f, 260f, 0f, 0.01f, 0.06f, 0.5f).Lp(2500f) };
                case Sfx.ShotLance:
                    return new[] { T(Wave.Saw, 1800f, 300f, 0f, 0.02f, 0.13f).Lp(6000f), T(Wave.Sine, 3600f, 2000f, 0f, 0f, 0.05f, 0.4f) };
                case Sfx.ShotSeeker:
                    return new[] { T(Wave.Noise, 900f, 2400f, 0.02f, 0.08f, 0.2f, 0.8f).Lp(2200f), T(Wave.Saw, 160f, 220f, 0.01f, 0.05f, 0.15f, 0.4f).Lp(900f) };
                case Sfx.ShotHornet:
                    return new[] { T(Wave.Square, 1700f, 1300f, 0f, 0.01f, 0.04f).WithDuty(0.3f).Lp(6000f), T(Wave.Noise, 6000f, 6000f, 0f, 0f, 0.03f, 0.3f) };
                case Sfx.ShotArc:
                    return new[] { T(Wave.Noise, 9000f, 3000f, 0f, 0.03f, 0.08f, 0.6f), T(Wave.Square, 2200f, 1600f, 0f, 0.02f, 0.07f, 0.5f).Vib(0.25f, 60f).Lp(7000f) };
                case Sfx.ShotDisc:
                    return new[] { T(Wave.Triangle, 700f, 1100f, 0f, 0.02f, 0.08f).Vib(0.06f, 35f), T(Wave.Sine, 1400f, 2200f, 0f, 0f, 0.05f, 0.3f) };
                case Sfx.BeamHum:
                    // Half a second that loops cleanly: whole cycles of every partial (220, 330, 440 Hz, 8 Hz wobble).
                    return new[]
                    {
                        T(Wave.Saw, 220f, 220f, 0f, 0.5f, 0f, 0.5f).Lp(1400f).Vib(0.004f, 8f),
                        T(Wave.Sine, 330f, 330f, 0f, 0.5f, 0f, 0.4f),
                        T(Wave.Sine, 440f, 440f, 0f, 0.5f, 0f, 0.3f).Vib(0.01f, 8f),
                    };

                // ---------------------------------------------------------- hits and blasts
                case Sfx.Hit:
                    return new[] { T(Wave.Noise, 7000f, 3000f, 0f, 0f, 0.035f, 0.8f), T(Wave.Square, 900f, 500f, 0f, 0f, 0.03f, 0.3f) };
                case Sfx.Deflect:
                    return new[] { T(Wave.Triangle, 2600f, 2400f, 0f, 0.01f, 0.09f), T(Wave.Sine, 3900f, 3700f, 0f, 0f, 0.06f, 0.4f) };
                case Sfx.ExplodeSmall:
                    return new[] { T(Wave.Noise, 2500f, 300f, 0f, 0.03f, 0.3f).Lp(3000f), T(Wave.Sine, 140f, 50f, 0f, 0.02f, 0.2f, 0.6f) };
                case Sfx.ExplodeBig:
                    return new[]
                    {
                        T(Wave.Noise, 1800f, 120f, 0f, 0.08f, 0.8f).Lp(1800f),
                        T(Wave.Sine, 110f, 32f, 0f, 0.05f, 0.7f, 0.9f),
                        T(Wave.Noise, 4000f, 800f, 0f, 0.02f, 0.2f, 0.4f).At(0.05f),
                    };
                case Sfx.ExplodeBoss:
                    return new[]
                    {
                        T(Wave.Noise, 1500f, 60f, 0f, 0.3f, 1.8f).Lp(1500f),
                        T(Wave.Sine, 90f, 25f, 0f, 0.3f, 1.6f, 1f),
                        T(Wave.Noise, 3000f, 400f, 0f, 0.05f, 0.5f, 0.6f).At(0.35f),
                        T(Wave.Noise, 2500f, 300f, 0f, 0.05f, 0.6f, 0.6f).At(0.8f),
                        T(Wave.Sine, 70f, 30f, 0f, 0.1f, 0.8f, 0.7f).At(0.8f),
                    };
                case Sfx.Blast:
                    return new[] { T(Wave.Noise, 3000f, 500f, 0f, 0.02f, 0.18f).Lp(3500f), T(Wave.Sine, 180f, 70f, 0f, 0f, 0.12f, 0.5f) };
                case Sfx.MineBurst:
                    return new[] { T(Wave.Square, 400f, 120f, 0f, 0.02f, 0.25f).Lp(2000f), T(Wave.Noise, 3000f, 500f, 0f, 0.01f, 0.2f, 0.6f) };
                case Sfx.EnemyShot:
                    return new[] { T(Wave.Square, 330f, 240f, 0f, 0.02f, 0.07f).WithDuty(0.4f).Lp(1800f) };

                // ---------------------------------------------------------- the player's ship
                case Sfx.Credit:
                    return new[] { T(Wave.Square, 1320f, 1320f, 0f, 0.03f, 0.08f).WithDuty(0.5f).Lp(5000f).Jump(1.5f, 0.035f) };
                case Sfx.Repair:
                    return new[] { T(Wave.Sine, 520f, 1400f, 0.01f, 0.12f, 0.15f), T(Wave.Triangle, 1040f, 2800f, 0.01f, 0.1f, 0.12f, 0.4f) };
                case Sfx.ShieldHit:
                    return new[] { T(Wave.Sine, 900f, 380f, 0f, 0.03f, 0.16f), T(Wave.Noise, 6000f, 3000f, 0f, 0f, 0.06f, 0.3f), T(Wave.Triangle, 1800f, 760f, 0f, 0.02f, 0.12f, 0.3f) };
                case Sfx.HullHit:
                    return new[] { T(Wave.Noise, 1600f, 400f, 0f, 0.05f, 0.25f).Lp(2500f), T(Wave.Square, 140f, 70f, 0f, 0.05f, 0.2f, 0.7f).Lp(900f) };
                case Sfx.PlayerDown:
                    return new[]
                    {
                        T(Wave.Noise, 2000f, 80f, 0f, 0.2f, 1.3f).Lp(2000f),
                        T(Wave.Saw, 600f, 40f, 0f, 0.1f, 1.2f, 0.6f).Lp(1500f),
                        T(Wave.Sine, 100f, 30f, 0f, 0.2f, 1f, 0.8f),
                    };

                // ---------------------------------------------------------- cores
                case Sfx.CoreNova:
                    return new[]
                    {
                        T(Wave.Saw, 200f, 1600f, 0.15f, 0f, 0f, 0.4f).Lp(3000f),
                        T(Wave.Noise, 3000f, 200f, 0f, 0.1f, 0.9f).Lp(2500f).At(0.15f),
                        T(Wave.Sine, 160f, 40f, 0f, 0.1f, 0.8f, 0.9f).At(0.15f),
                    };
                case Sfx.CoreLance:
                    return new[]
                    {
                        T(Wave.Saw, 120f, 480f, 0.25f, 0.2f, 0.6f, 0.7f).Lp(2500f).Vib(0.02f, 12f),
                        T(Wave.Sine, 240f, 960f, 0.25f, 0.2f, 0.6f, 0.5f),
                        T(Wave.Noise, 8000f, 4000f, 0.2f, 0.3f, 0.5f, 0.2f),
                    };
                case Sfx.CoreAegis:
                    return new[]
                    {
                        T(Wave.Sine, 660f, 990f, 0.05f, 0.3f, 0.5f).Vib(0.03f, 9f),
                        T(Wave.Sine, 990f, 1320f, 0.05f, 0.3f, 0.5f, 0.6f).Vib(0.03f, 11f),
                        T(Wave.Triangle, 330f, 330f, 0.1f, 0.3f, 0.4f, 0.4f),
                    };
                case Sfx.CoreWingmates:
                    return new[]
                    {
                        T(Wave.Square, 523f, 523f, 0f, 0.07f, 0.04f, 0.6f).Lp(4000f),
                        T(Wave.Square, 659f, 659f, 0f, 0.07f, 0.04f, 0.6f).Lp(4000f).At(0.1f),
                        T(Wave.Square, 784f, 784f, 0f, 0.07f, 0.04f, 0.6f).Lp(4000f).At(0.2f),
                        T(Wave.Square, 1047f, 1047f, 0f, 0.12f, 0.2f, 0.6f).Lp(4000f).At(0.3f),
                    };
                case Sfx.CoreReady:
                    return new[] { T(Wave.Sine, 880f, 880f, 0f, 0.06f, 0.1f, 0.6f), T(Wave.Sine, 1320f, 1320f, 0f, 0.08f, 0.2f, 0.6f).At(0.08f) };

                // ---------------------------------------------------------- boss and alerts
                case Sfx.Warning:
                    return new[]
                    {
                        T(Wave.Square, 440f, 440f, 0.01f, 0.18f, 0.03f, 0.5f).Lp(2000f),
                        T(Wave.Square, 330f, 330f, 0.01f, 0.18f, 0.03f, 0.5f).Lp(2000f).At(0.22f),
                        T(Wave.Square, 440f, 440f, 0.01f, 0.18f, 0.03f, 0.5f).Lp(2000f).At(0.44f),
                        T(Wave.Square, 330f, 330f, 0.01f, 0.18f, 0.1f, 0.5f).Lp(2000f).At(0.66f),
                    };
                case Sfx.Laser:
                    return new[] { T(Wave.Saw, 90f, 70f, 0.05f, 1.1f, 0.3f, 0.8f).Lp(1200f).Vib(0.05f, 30f), T(Wave.Noise, 5000f, 4000f, 0.05f, 1.1f, 0.3f, 0.3f).Lp(3000f) };

                // ---------------------------------------------------------- menus and results
                case Sfx.Click:
                    return new[] { T(Wave.Square, 1800f, 1500f, 0f, 0.005f, 0.025f).Lp(6000f) };
                case Sfx.Buy:
                    return new[]
                    {
                        T(Wave.Square, 988f, 988f, 0f, 0.05f, 0.05f, 0.6f).Lp(5000f),
                        T(Wave.Square, 1319f, 1319f, 0f, 0.12f, 0.2f, 0.6f).Lp(5000f).At(0.08f),
                    };
                case Sfx.Clear:
                    return new[]
                    {
                        T(Wave.Square, 523f, 523f, 0f, 0.1f, 0.05f, 0.5f).Lp(3500f),
                        T(Wave.Square, 659f, 659f, 0f, 0.1f, 0.05f, 0.5f).Lp(3500f).At(0.15f),
                        T(Wave.Square, 784f, 784f, 0f, 0.1f, 0.05f, 0.5f).Lp(3500f).At(0.3f),
                        T(Wave.Square, 1047f, 1047f, 0f, 0.4f, 0.5f, 0.6f).Lp(3500f).At(0.45f),
                        T(Wave.Triangle, 262f, 262f, 0f, 0.8f, 0.5f, 0.5f).At(0.45f),
                    };
                case Sfx.Fail:
                    return new[]
                    {
                        T(Wave.Square, 392f, 392f, 0f, 0.15f, 0.05f, 0.5f).Lp(2000f),
                        T(Wave.Square, 349f, 349f, 0f, 0.15f, 0.05f, 0.5f).Lp(2000f).At(0.22f),
                        T(Wave.Square, 311f, 293f, 0f, 0.5f, 0.6f, 0.5f).Lp(2000f).At(0.44f),
                    };
                default: // Medal
                    return new[]
                    {
                        T(Wave.Sine, 1568f, 1568f, 0f, 0.05f, 0.3f, 0.6f),
                        T(Wave.Sine, 2093f, 2093f, 0f, 0.05f, 0.4f, 0.6f).At(0.07f),
                        T(Wave.Triangle, 784f, 784f, 0f, 0.1f, 0.3f, 0.4f),
                    };
            }
        }
    }
}
