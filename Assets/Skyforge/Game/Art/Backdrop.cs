using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Skyforge.Game
{
    /// <summary>
    /// The scrolling ground far below the ship. Stage 1-1, "First Light": a coastal city at dawn
    /// (docs/DESIGN.md §6). Ocean on the left with glinting wave facets, a beach and a coastal
    /// highway with moving car lights, city blocks of low-poly buildings with lit windows and street
    /// lights, hills inland, and a few low clouds drifting between the ship and the ground.
    /// <para>
    /// Endless: the ground is built in chunks that are rebuilt as they scroll off the bottom and
    /// reused at the top. Everything is a pure function of the global coordinate (hashes, no
    /// state), so chunk edges always match. Cars and clouds are drawn each frame with GPU
    /// instancing. Purely visual; uses no gameplay state.
    /// </para>
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        /// <summary>Height of the ground plane below the play field.</summary>
        public const float GroundY = -9f;
        /// <summary>Ground scroll speed in units per second (the ship's apparent airspeed).</summary>
        public const float ScrollSpeed = 3.5f;
        /// <summary>Height of the cloud layer (between the ground and the ship).</summary>
        public const float CloudY = -4.5f;

        const float Cell = 2f;
        const int CellsX = 26;       // 52 units wide: wider than the tilted view
        const int CellsZ = 12;       // 24 units per chunk
        const float ChunkDepth = CellsZ * Cell;
        const int ChunkCount = 5;
        const float StartZ = -34f;   // world Z of global Z = 0 when scroll = 0
        const float HalfWidth = CellsX * Cell * 0.5f;

        // City grid: 7-unit blocks = 5 units of lots + a 2-unit road.
        const float BlockPitch = 7f;
        const float BlockSize = 5f;
        // Distances inland from the shoreline.
        const float BeachEnd = 1.2f;
        const float HighwayIn = 1.7f, HighwayOut = 2.9f;
        const float CityStart = 3.5f, CityEnd = 16f;

        // Ground sub-meshes.
        const int SubDeep = 0, SubShallow = 1, SubSand = 2, SubGround = 3, SubPark = 4, SubRock = 5,
                  SubBuilding = 6, SubRoof = 7, SubRoad = 8;
        // Light sub-meshes.
        const int SubWindow = 0, SubStreet = 1, SubBeacon = 2;

        sealed class Chunk
        {
            public GameObject Go;
            public MeshFilter Ground;
            public MeshFilter Lights;
            public int Index = int.MinValue;
        }

        public int Seed = 1;
        readonly List<Chunk> _chunks = new List<Chunk>();
        InstancedBatch _carHeads, _carTails, _clouds;
        readonly List<Mesh> _ownedMeshes = new List<Mesh>();

        void Awake()
        {
            for (int i = 0; i < ChunkCount; i++)
            {
                var c = new Chunk { Go = new GameObject("GroundChunk" + i) };
                c.Go.transform.SetParent(transform, false);
                c.Ground = AddRenderer(c.Go, "Ground", Mats.DeepWater, Mats.ShallowWater, Mats.Sand, Mats.Ground,
                    Mats.Park, Mats.Rock, Mats.Building, Mats.Roof, Mats.Road);
                c.Lights = AddRenderer(c.Go, "Lights", Mats.WindowLight, Mats.CityLight, Mats.Beacon);
                _chunks.Add(c);
            }

            var car = new MeshBuilder();
            car.Octahedron(Vector3.zero, new Vector3(0.09f, 0.05f, 0.16f), 0);
            var carMesh = Own(car.Build("CarLight"));
            _carHeads = new InstancedBatch(carMesh, Mats.CarHead);
            _carTails = new InstancedBatch(carMesh, Mats.CarTail);
            _clouds = new InstancedBatch(Own(CloudMesh()), Mats.Cloud);
        }

        Mesh Own(Mesh m)
        {
            _ownedMeshes.Add(m);
            return m;
        }

        static MeshFilter AddRenderer(GameObject parent, string name, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mf;
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Scroll to <paramref name="time"/> seconds into the stage and draw the moving parts.</summary>
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
            DrawCars(time, scroll);
            DrawClouds(scroll);
        }

        /// <summary>World Z of global ground coordinate <paramref name="g"/>.</summary>
        static float WorldZ(float g, float scroll) => StartZ + g - scroll;

        void DrawCars(float time, float scroll)
        {
            _carHeads.Clear();
            _carTails.Clear();
            const float loop = 120f;        // cars repeat every 120 units of highway
            const int perLane = 34;
            float windowStart = scroll - 40f;
            for (int lane = 0; lane < 2; lane++)
            {
                float dir = lane == 0 ? 1f : -1f; // lane 0 drives up the screen (headlights), lane 1 down
                for (int i = 0; i < perLane; i++)
                {
                    float speed = 5f + Hash(i, lane + 50) * 3f;
                    float baseZ = (i + Hash(i, lane + 70) * 0.6f) * (loop / perLane);
                    float g = windowStart + Mod(baseZ + dir * speed * time - windowStart, loop);
                    float x = Shore(g) + (HighwayIn + HighwayOut) * 0.5f + (lane == 0 ? 0.28f : -0.28f);
                    var pos = new Vector3(x, GroundY + 0.32f, WorldZ(g, scroll));
                    if (lane == 0) _carHeads.Add(pos, Quaternion.identity, Vector3.one);
                    else _carTails.Add(pos, Quaternion.identity, Vector3.one);
                }
            }
            _carHeads.Draw();
            _carTails.Draw();
        }

        void DrawClouds(float scroll)
        {
            _clouds.Clear();
            const float slot = 11f;
            // Clouds are nearer the camera than the ground, so they drift past a little faster.
            float s2 = scroll * 1.15f;
            int k0 = Mathf.FloorToInt((s2 - 10f) / slot), k1 = Mathf.CeilToInt((s2 + 95f) / slot);
            for (int k = k0; k <= k1; k++)
            {
                if (Hash(k, 900) > 0.42f) continue;
                float g = k * slot + Hash(k, 901) * 6f;
                float x = (Hash(k, 902) - 0.5f) * 30f;
                float s = 1.4f + Hash(k, 903) * 1.8f;
                var rot = Quaternion.Euler(0f, Hash(k, 904) * 360f, 0f);
                var pos = new Vector3(x, CloudY + Hash(k, 905) * 1.2f, StartZ + g - s2);
                _clouds.Add(pos, rot, new Vector3(s, s * 0.6f, s));
            }
            _clouds.Draw();
        }

        static float Mod(float a, float m)
        {
            float r = a % m;
            return r < 0f ? r + m : r;
        }

        // ------------------------------------------------------------------ chunk building

        void Rebuild(Chunk c, int index)
        {
            c.Index = index;
            if (c.Ground.sharedMesh != null) Destroy(c.Ground.sharedMesh);
            if (c.Lights.sharedMesh != null) Destroy(c.Lights.sharedMesh);

            var ground = new MeshBuilder();
            var lights = new MeshBuilder();
            float g0 = index * ChunkDepth; // global Z of this chunk's first row
            int row0 = index * CellsZ;

            // Terrain grid.
            for (int gz = 0; gz < CellsZ; gz++)
            {
                for (int gx = 0; gx < CellsX; gx++)
                {
                    int ix = gx - CellsX / 2, iz = row0 + gz;
                    var p00 = Vertex(ix, iz, g0);
                    var p10 = Vertex(ix + 1, iz, g0);
                    var p01 = Vertex(ix, iz + 1, g0);
                    var p11 = Vertex(ix + 1, iz + 1, g0);
                    bool flip = ((ix + iz) & 1) == 0;
                    if (flip)
                    {
                        GroundTri(ground, p00, p01, p11, g0);
                        GroundTri(ground, p00, p11, p10, g0);
                    }
                    else
                    {
                        GroundTri(ground, p00, p01, p10, g0);
                        GroundTri(ground, p10, p01, p11, g0);
                    }
                }
            }

            BuildHighway(ground, lights, g0);
            BuildCity(ground, lights, g0);

            c.Ground.sharedMesh = ground.Build("Ground" + index);
            c.Lights.sharedMesh = lights.Build("Lights" + index);
        }

        /// <summary>Ground vertex for global grid point (ix, iz), in chunk-local space.</summary>
        Vector3 Vertex(int ix, int iz, float g0)
        {
            float x = ix * Cell, g = iz * Cell;
            return new Vector3(x, Height(x, g, ix, iz), g - g0);
        }

        /// <summary>The shoreline's X at global Z: a gently winding coast, sea to the left.</summary>
        float Shore(float g) => -5f + 4.5f * Mathf.Sin(g * 0.031f + Seed) + 2f * Mathf.Sin(g * 0.087f + 1.3f + Seed * 2f);

        float Height(float x, float g, int ix, int iz)
        {
            float d = x - Shore(g);
            if (d < -1f) return -0.25f + (Hash(ix, iz) - 0.5f) * 0.22f;       // waves: jittered facets
            if (d < BeachEnd) return Mathf.Lerp(-0.25f, 0.2f, (d + 1f) / (BeachEnd + 1f));
            if (d < CityEnd + 1f) return 0.2f;                                  // flat for the city
            float hill = 0.5f + ValueNoise(x / 6f, g / 6f);
            return 0.2f + (d - CityEnd - 1f) * 0.32f * hill;
        }

        void GroundTri(MeshBuilder b, Vector3 a, Vector3 c, Vector3 e, float g0)
        {
            var mid = (a + c + e) / 3f;
            float g = mid.z + g0;
            float d = mid.x - Shore(g);
            int sub;
            if (d < -4f) sub = SubDeep;
            else if (d < -1f) sub = SubShallow;
            else if (d < BeachEnd) sub = SubSand;
            else if (mid.y > 1.6f) sub = SubRock;
            else if (IsPark(mid.x, g)) sub = SubPark;
            else sub = SubGround;
            b.Tri(a, c, e, Vector3.up, sub);
        }

        /// <summary>A city block that is left as a park (no buildings).</summary>
        bool IsPark(float x, float g)
        {
            int bx = Mathf.FloorToInt(x / BlockPitch), bz = Mathf.FloorToInt(g / BlockPitch);
            float d = x - Shore(g);
            return d > CityStart && d < CityEnd && Hash(bx * 3 + 1, bz * 5 + 2) < 0.12f;
        }

        /// <summary>The coastal highway: a dark strip just inland of the beach, lit along both sides.</summary>
        void BuildHighway(MeshBuilder ground, MeshBuilder lights, float g0)
        {
            const float step = 1f;
            for (float l = 0f; l < ChunkDepth; l += step)
            {
                float ga = g0 + l, gb = ga + step;
                float sa = Shore(ga), sb = Shore(gb);
                ground.Poly(new[]
                {
                    new Vector3(sa + HighwayIn, 0.24f, l), new Vector3(sa + HighwayOut, 0.24f, l),
                    new Vector3(sb + HighwayOut, 0.24f, l + step), new Vector3(sb + HighwayIn, 0.24f, l + step),
                }, Vector3.up, SubRoad);
            }
            for (float l = 0.5f; l < ChunkDepth; l += 3f)
            {
                float s = Shore(g0 + l);
                lights.Octahedron(new Vector3(s + HighwayIn - 0.12f, 0.45f, l), new Vector3(0.08f, 0.08f, 0.08f), SubStreet);
                lights.Octahedron(new Vector3(s + HighwayOut + 0.12f, 0.45f, l + 1.5f), new Vector3(0.08f, 0.08f, 0.08f), SubStreet);
            }
        }

        /// <summary>City blocks whose corner lies in this chunk: buildings, windows, street lights.</summary>
        void BuildCity(MeshBuilder ground, MeshBuilder lights, float g0)
        {
            int bz0 = Mathf.CeilToInt(g0 / BlockPitch), bz1 = Mathf.CeilToInt((g0 + ChunkDepth) / BlockPitch) - 1;
            int bx0 = Mathf.FloorToInt(-HalfWidth / BlockPitch), bx1 = Mathf.FloorToInt(HalfWidth / BlockPitch);
            for (int bz = bz0; bz <= bz1; bz++)
            {
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    float x0 = bx * BlockPitch + 1f, gStart = bz * BlockPitch + 1f; // block lots start after the road
                    float cx = x0 + BlockSize * 0.5f, cg = gStart + BlockSize * 0.5f;
                    float d = cx - Shore(cg);
                    // The whole block must be inland of the highway and short of the hills.
                    if (x0 - Shore(gStart) < CityStart || x0 - Shore(gStart + BlockSize) < CityStart || d > CityEnd)
                        continue;

                    StreetLights(lights, x0, gStart, g0);
                    if (IsPark(cx, cg)) continue;

                    // Taller towers downtown: a band that comes and goes along the coast, nearest the sea.
                    float downtown = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(cg * 0.045f + 1f + Seed)), 2f);
                    float nearSea = Mathf.Clamp01(1f - (d - CityStart) / 10f);
                    for (int lx = 0; lx < 2; lx++)
                    {
                        for (int lz = 0; lz < 2; lz++)
                        {
                            int hx = bx * 2 + lx, hz = bz * 2 + lz;
                            if (Hash(hx, hz + 300) > 0.8f) continue; // empty lot
                            float w = 1.3f + Hash(hx, hz + 301) * 0.8f;
                            float dep = 1.3f + Hash(hx, hz + 302) * 0.8f;
                            float h = 0.5f + Hash(hx, hz + 303) * 1.3f + downtown * nearSea * Hash(hx, hz + 304) * 5.5f;
                            float px = x0 + 1.25f + lx * 2.5f + (Hash(hx, hz + 305) - 0.5f) * 0.4f;
                            float pg = gStart + 1.25f + lz * 2.5f + (Hash(hx, hz + 306) - 0.5f) * 0.4f;
                            Building(ground, lights, new Vector3(px, 0.2f, pg - g0), w, dep, h, hx, hz);
                        }
                    }
                }
            }
        }

        /// <summary>One building: a block with a lighter inset roof, lit window strips on the sides
        /// the camera can see, and a beacon on the tall ones.</summary>
        static void Building(MeshBuilder ground, MeshBuilder lights, Vector3 baseCentre, float w, float dep, float h, int hx, int hz)
        {
            ground.Box(baseCentre + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, dep), SubBuilding);
            ground.Box(baseCentre + new Vector3(0f, h + 0.04f, 0f), new Vector3(w - 0.3f, 0.08f, dep - 0.3f), SubRoof);

            // Window strips, one per lit floor, on the south face (towards the camera) and the sides.
            int floors = Mathf.FloorToInt((h - 0.3f) / 0.45f);
            for (int f = 0; f < floors; f++)
            {
                float y = baseCentre.y + 0.35f + f * 0.45f;
                for (int face = 0; face < 3; face++)
                {
                    if (Hash(hx * 7 + face, hz * 13 + f) > 0.42f) continue;
                    float frac = 0.3f + Hash(hx + f, hz * 3 + face) * 0.55f;
                    Vector3 c, size;
                    if (face == 0)
                    {
                        c = baseCentre + new Vector3(0f, 0f, -dep * 0.5f - 0.02f);
                        size = new Vector3(w * frac, 0.12f, 0.04f);
                    }
                    else
                    {
                        float s = face == 1 ? 1f : -1f;
                        c = baseCentre + new Vector3(s * (w * 0.5f + 0.02f), 0f, 0f);
                        size = new Vector3(0.04f, 0.12f, dep * frac);
                    }
                    c.y = y;
                    lights.Box(c, size, SubWindow);
                }
            }
            if (h > 4f)
            {
                ground.Box(baseCentre + new Vector3(0f, h + 0.4f, 0f), new Vector3(0.06f, 0.7f, 0.06f), SubRoof);
                lights.Octahedron(baseCentre + new Vector3(0f, h + 0.8f, 0f), new Vector3(0.1f, 0.1f, 0.1f), SubBeacon);
            }
        }

        /// <summary>Lamps along the road on the west and south edges of a block.</summary>
        static void StreetLights(MeshBuilder lights, float x0, float gStart, float g0)
        {
            var r = new Vector3(0.07f, 0.07f, 0.07f);
            for (float t = 0.5f; t < BlockSize; t += 2.4f)
            {
                lights.Octahedron(new Vector3(x0 - 0.25f, 0.45f, gStart + t - g0), r, SubStreet);
                lights.Octahedron(new Vector3(x0 + t, 0.45f, gStart - 0.25f - g0), r, SubStreet);
            }
        }

        /// <summary>A low-poly cloud: a few overlapping flattened diamonds.</summary>
        static Mesh CloudMesh()
        {
            var b = new MeshBuilder();
            b.Octahedron(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.45f, 0.8f), 0);
            b.Octahedron(new Vector3(0.75f, -0.1f, 0.25f), new Vector3(0.7f, 0.35f, 0.6f), 0);
            b.Octahedron(new Vector3(-0.7f, -0.12f, -0.2f), new Vector3(0.65f, 0.3f, 0.55f), 0);
            b.Octahedron(new Vector3(0.1f, 0.15f, -0.45f), new Vector3(0.55f, 0.3f, 0.5f), 0);
            return b.Build("Cloud");
        }

        // ------------------------------------------------------------------ noise

        float ValueNoise(float x, float z)
        {
            int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z);
            float tx = x - x0, tz = z - z0;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);
            float a = Hash(x0, z0), b = Hash(x0 + 1, z0), c = Hash(x0, z0 + 1), d = Hash(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
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
            foreach (var c in _chunks)
            {
                if (c.Ground != null && c.Ground.sharedMesh != null) Destroy(c.Ground.sharedMesh);
                if (c.Lights != null && c.Lights.sharedMesh != null) Destroy(c.Lights.sharedMesh);
            }
            foreach (var m in _ownedMeshes) if (m != null) Destroy(m);
        }
    }
}
