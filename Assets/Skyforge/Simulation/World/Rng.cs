namespace Skyforge.Simulation
{
    /// <summary>
    /// Small seeded random generator (xorshift32). The simulation never uses System.Random or
    /// UnityEngine.Random, so the same seed and inputs always replay the same stage.
    /// </summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(uint seed) { _state = seed == 0 ? 0x9E3779B9u : seed; }

        public uint State => _state;

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Next01() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Next01();

        /// <summary>Uniform integer in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));
    }
}
