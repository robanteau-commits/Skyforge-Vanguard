using UnityEngine;

namespace Skyforge.Game
{
    /// <summary>
    /// The background (Rob's call, 2026-11-01): plain black space with small, scattered stars
    /// streaming from the top of the screen to the bottom, so the ship feels like it's flying
    /// forward. Three layers give parallax: far stars are tiny, dim and slow; near ones are a
    /// little larger, brighter, faster and slightly streaked. Only the near layer is bright enough
    /// to bloom, and only faintly, so the stars never compete with bullets.
    /// <para>
    /// Every star's position is a pure function of its index and the time, so the field loops
    /// endlessly with no state. Drawn with GPU instancing. Purely visual.
    /// </para>
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        /// <summary>Depth of the star sheet below the play field (well below anything in play).</summary>
        public const float StarY = -14f;

        // The sheet is wider and taller than the tilted view at that depth, with margin.
        const float MinX = -26f, MaxX = 26f;
        const float MinZ = -30f, MaxZ = 56f;

        sealed class Layer
        {
            public int Count;
            /// <summary>Screen-down speed in units per second.</summary>
            public float Speed;
            public float SizeMin, SizeMax;
            /// <summary>Length/width: near stars streak slightly along their motion.</summary>
            public float Stretch;
            public int Salt;
            public InstancedBatch Batch;
        }

        Layer[] _layers;
        Mesh _starMesh;

        void Awake()
        {
            var b = new MeshBuilder();
            b.Octahedron(Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), 0);
            _starMesh = b.Build("Star");
            _layers = new[]
            {
                new Layer { Count = 150, Speed = 2.2f, SizeMin = 0.05f, SizeMax = 0.09f, Stretch = 1f, Salt = 11, Batch = new InstancedBatch(_starMesh, Mats.StarFar) },
                new Layer { Count = 60, Speed = 5.5f, SizeMin = 0.08f, SizeMax = 0.13f, Stretch = 1.6f, Salt = 23, Batch = new InstancedBatch(_starMesh, Mats.StarMid) },
                new Layer { Count = 18, Speed = 11f, SizeMin = 0.1f, SizeMax = 0.16f, Stretch = 3.2f, Salt = 37, Batch = new InstancedBatch(_starMesh, Mats.StarNear) },
            };
        }

        /// <summary>Move the stars to <paramref name="time"/> seconds into the stage and draw them.</summary>
        public void SetTime(float time)
        {
            float depth = MaxZ - MinZ;
            foreach (var layer in _layers)
            {
                layer.Batch.Clear();
                for (int i = 0; i < layer.Count; i++)
                {
                    float x = Mathf.Lerp(MinX, MaxX, Hash(i, layer.Salt));
                    float z0 = Hash(i, layer.Salt + 1) * depth;
                    // Each star drifts at its layer's speed, ±20%, so they don't move in lockstep.
                    float speed = layer.Speed * (0.8f + 0.4f * Hash(i, layer.Salt + 2));
                    float z = MinZ + Mod(z0 - speed * time, depth);
                    float size = Mathf.Lerp(layer.SizeMin, layer.SizeMax, Hash(i, layer.Salt + 3));
                    // A slow, gentle twinkle on a few stars.
                    if (Hash(i, layer.Salt + 4) < 0.25f)
                        size *= 0.8f + 0.2f * Mathf.Sin(time * (1.5f + 2f * Hash(i, layer.Salt + 5)) + i);
                    layer.Batch.Add(new Vector3(x, StarY, z), Quaternion.identity, new Vector3(size, size, size * layer.Stretch));
                }
                layer.Batch.Draw();
            }
        }

        static float Mod(float a, float m)
        {
            float r = a % m;
            return r < 0f ? r + m : r;
        }

        /// <summary>Deterministic hash of two integers to [0, 1).</summary>
        static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        void OnDestroy()
        {
            if (_starMesh != null) Destroy(_starMesh);
        }
    }
}
