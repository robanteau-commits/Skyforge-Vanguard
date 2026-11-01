using System.Collections.Generic;
using UnityEngine;

namespace Skyforge.Game
{
    /// <summary>
    /// The scrolling ground far below the ship: dark low-poly coastline with water in the middle,
    /// hills and rock towards the sides, and sparse amber city lights. Endless: chunks are rebuilt
    /// as they scroll off the bottom and reused at the top. Heights come from a hash of the global
    /// grid coordinate, so chunk edges always match.
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        /// <summary>Height of the ground plane below the play field.</summary>
        public const float GroundY = -9f;
        /// <summary>Ground scroll speed in units per second (the ship's apparent airspeed).</summary>
        public const float ScrollSpeed = 3.5f;

        const float Cell = 2f;
        const int CellsX = 34;       // 68 units wide: wider than the tilted view
        const int CellsZ = 12;       // 24 units per chunk
        const float ChunkDepth = CellsZ * Cell;
        const int ChunkCount = 5;
        const float StartZ = -34f;   // world Z of global row 0 when scroll = 0

        sealed class Chunk
        {
            public GameObject Go;
            public MeshFilter Ground;
            public MeshFilter Lights;
            public int Index = int.MinValue;
        }

        readonly List<Chunk> _chunks = new List<Chunk>();
        public int Seed = 1;

        void Awake()
        {
            for (int i = 0; i < ChunkCount; i++)
            {
                var c = new Chunk { Go = new GameObject("GroundChunk" + i) };
                c.Go.transform.SetParent(transform, false);
                c.Ground = AddRenderer(c.Go, "Ground", Mats.Water, Mats.Land, Mats.Rock);
                c.Lights = AddRenderer(c.Go, "Lights", Mats.CityLight);
                _chunks.Add(c);
            }
        }

        static MeshFilter AddRenderer(GameObject parent, string name, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mf;
        }

        /// <summary>Scroll to <paramref name="time"/> seconds into the stage.</summary>
        public void SetTime(float time)
        {
            float scroll = time * ScrollSpeed;
            int first = Mathf.FloorToInt(scroll / ChunkDepth);
            for (int i = 0; i < ChunkCount; i++)
            {
                int index = first + i;
                var c = _chunks[((index % ChunkCount) + ChunkCount) % ChunkCount];
                if (c.Index != index) Rebuild(c, index);
                c.Go.transform.localPosition = new Vector3(0f, GroundY, StartZ + index * ChunkDepth - scroll);
            }
        }

        void Rebuild(Chunk c, int index)
        {
            c.Index = index;
            if (c.Ground.sharedMesh != null) Destroy(c.Ground.sharedMesh);
            if (c.Lights.sharedMesh != null) Destroy(c.Lights.sharedMesh);

            var ground = new MeshBuilder();
            var lights = new MeshBuilder();
            int row0 = index * CellsZ;
            for (int gz = 0; gz < CellsZ; gz++)
            {
                for (int gx = 0; gx < CellsX; gx++)
                {
                    int ix = gx - CellsX / 2, iz = row0 + gz;
                    var p00 = Vertex(ix, iz, gz);
                    var p10 = Vertex(ix + 1, iz, gz);
                    var p01 = Vertex(ix, iz + 1, gz + 1);
                    var p11 = Vertex(ix + 1, iz + 1, gz + 1);
                    // Alternate the diagonal for a less regular faceting.
                    bool flip = ((ix + iz) & 1) == 0;
                    if (flip)
                    {
                        AddTri(ground, p00, p01, p11);
                        AddTri(ground, p00, p11, p10);
                    }
                    else
                    {
                        AddTri(ground, p00, p01, p10);
                        AddTri(ground, p10, p01, p11);
                    }
                    // Sparse lights on low, flat land near the shore: towns.
                    float h = (p00.y + p11.y) * 0.5f;
                    if (h > 0.15f && h < 1.6f && Hash(ix * 7 + 3, iz * 13 + 5) < 0.09f)
                    {
                        var centre = (p00 + p11) * 0.5f;
                        int n = 2 + (int)(Hash(ix, iz + 99) * 4f);
                        for (int k = 0; k < n; k++)
                        {
                            float ox = (Hash(ix + k * 31, iz) - 0.5f) * 1.6f;
                            float oz = (Hash(ix, iz + k * 17) - 0.5f) * 1.6f;
                            lights.Box(centre + new Vector3(ox, 0.15f, oz), new Vector3(0.16f, 0.12f, 0.16f), 0);
                        }
                    }
                }
            }
            c.Ground.sharedMesh = ground.Build("Ground" + index);
            c.Lights.sharedMesh = lights.Build("Lights" + index);
        }

        /// <summary>Ground vertex for global grid point (ix, iz); local Z within its chunk.</summary>
        Vector3 Vertex(int ix, int iz, int localRow)
        {
            float x = ix * Cell;
            float h = Height(ix, iz);
            return new Vector3(x, h, localRow * Cell);
        }

        float Height(int ix, int iz)
        {
            // Two octaves of value noise.
            float n = 0.65f * ValueNoise(ix / 5f, iz / 5f) + 0.35f * ValueNoise(ix / 2f + 17f, iz / 2f - 9f);
            // A winding sea channel down the middle; land rises towards the sides.
            float channel = Mathf.Sin(iz * 0.07f + Seed) * 5f;
            float side = Mathf.Abs(ix * Cell - channel);
            float h = n * 5f - 3.2f + side * 0.09f;
            return h < 0f ? 0f : h; // water is flat
        }

        static void AddTri(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d)
        {
            float avg = (a.y + c.y + d.y) / 3f;
            int sub = (a.y <= 0.01f && c.y <= 0.01f && d.y <= 0.01f) ? 0 : avg < 2.6f ? 1 : 2;
            b.Tri(a, c, d, Vector3.up, sub);
        }

        float ValueNoise(float x, float z)
        {
            int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z);
            float tx = x - x0, tz = z - z0;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);
            float a = Hash(x0, z0), b = Hash(x0 + 1, z0), c = Hash(x0, z0 + 1), d = Hash(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263 + Seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        void OnDestroy()
        {
            foreach (var c in _chunks)
            {
                if (c.Ground != null && c.Ground.sharedMesh != null) Destroy(c.Ground.sharedMesh);
                if (c.Lights != null && c.Lights.sharedMesh != null) Destroy(c.Lights.sharedMesh);
            }
        }
    }
}
