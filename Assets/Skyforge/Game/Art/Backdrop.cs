using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Skyforge.Game
{
    /// <summary>
    /// The scrolling ground far below the ship. Stage 1-1 uses Rob's pick from the backdrop
    /// concepts (2026-11-01): <b>B · Open Ocean, darker</b>. A dark low-poly swell whose facets catch
    /// a low dawn sun, with the odd rocky islet drifting past. No point lights and nothing solid near
    /// the ship's height, so the action stays easy to read.
    /// <para>
    /// The sea is one fixed sheet of flat-shaded triangles whose heights are recomputed every frame
    /// from the scroll distance and time, so it scrolls endlessly with no chunk seams. Islets come
    /// from hashed slots along the route and are pooled. Purely visual; uses no gameplay state.
    /// </para>
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        /// <summary>Height of the sea below the play field.</summary>
        public const float GroundY = -9f;
        /// <summary>Scroll speed in units per second (the ship's apparent airspeed).</summary>
        public const float ScrollSpeed = 3.5f;

        // The sea sheet covers everything the tilted camera can see at sea level, with margin.
        const float Cell = 1.6f;
        const float MinX = -28f, MaxX = 28f;
        const float MinZ = -26f, MaxZ = 66f;

        // Islets: one possible islet per slot of route.
        const float IsletSlot = 26f;
        const float IsletChance = 0.5f;
        const int IsletVariants = 3;

        int _nx, _nz;
        float[] _heights;
        Vector3[] _verts, _normals;
        Mesh _sea;

        readonly List<Mesh> _isletMeshes = new List<Mesh>();
        readonly List<GameObject> _isletPool = new List<GameObject>();

        void Awake()
        {
            BuildSea();
            for (int v = 0; v < IsletVariants; v++) _isletMeshes.Add(IsletMesh(v));
        }

        // ------------------------------------------------------------------ sea

        void BuildSea()
        {
            _nx = Mathf.CeilToInt((MaxX - MinX) / Cell);
            _nz = Mathf.CeilToInt((MaxZ - MinZ) / Cell);
            _heights = new float[(_nx + 1) * (_nz + 1)];
            int triCount = _nx * _nz * 2;
            _verts = new Vector3[triCount * 3];
            _normals = new Vector3[triCount * 3];
            var tris = new int[triCount * 3];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;

            _sea = new Mesh { name = "Sea" };
            _sea.MarkDynamic();
            if (_verts.Length > 65000) _sea.indexFormat = IndexFormat.UInt32;
            _sea.vertices = _verts;
            _sea.normals = _normals;
            _sea.triangles = tris;
            _sea.bounds = new Bounds(new Vector3(0f, 0f, (MinZ + MaxZ) * 0.5f), new Vector3(MaxX - MinX, 4f, MaxZ - MinZ));

            var go = new GameObject("Sea");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, GroundY, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = _sea;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Sea;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>
        /// Swell height at world (x, z) at this moment. <paramref name="g"/> is the distance along
        /// the route (z plus scroll), so the swell travels down the screen with the flight.
        /// Three crossing wave trains, long and slow to short and quick.
        /// </summary>
        static float Swell(float x, float g, float t)
        {
            const float k = 2f * Mathf.PI / 120f;
            return 0.32f * Mathf.Sin(0.42f * x + 4f * k * g + 0.8f * t)
                 + 0.22f * Mathf.Sin(-0.28f * x + 7f * k * g - 1.1f * t)
                 + 0.10f * Mathf.Sin(1.25f * x + 15f * k * g + 1.9f * t);
        }

        void UpdateSea(float time, float scroll)
        {
            int stride = _nx + 1;
            for (int j = 0; j <= _nz; j++)
            {
                float z = MinZ + j * Cell;
                for (int i = 0; i <= _nx; i++)
                    _heights[j * stride + i] = Swell(MinX + i * Cell, z + scroll, time);
            }

            int v = 0;
            for (int j = 0; j < _nz; j++)
            {
                float z0 = MinZ + j * Cell, z1 = z0 + Cell;
                for (int i = 0; i < _nx; i++)
                {
                    float x0 = MinX + i * Cell, x1 = x0 + Cell;
                    var p00 = new Vector3(x0, _heights[j * stride + i], z0);
                    var p10 = new Vector3(x1, _heights[j * stride + i + 1], z0);
                    var p01 = new Vector3(x0, _heights[(j + 1) * stride + i], z1);
                    var p11 = new Vector3(x1, _heights[(j + 1) * stride + i + 1], z1);
                    // Alternate the diagonal for a less regular faceting. Clockwise from above.
                    if (((i + j) & 1) == 0)
                    {
                        Tri(ref v, p00, p01, p11);
                        Tri(ref v, p00, p11, p10);
                    }
                    else
                    {
                        Tri(ref v, p00, p01, p10);
                        Tri(ref v, p10, p01, p11);
                    }
                }
            }
            _sea.vertices = _verts;
            _sea.normals = _normals;
        }

        void Tri(ref int v, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a).normalized; // faces up for clockwise-from-above
            _verts[v] = a; _normals[v++] = n;
            _verts[v] = b; _normals[v++] = n;
            _verts[v] = c; _normals[v++] = n;
        }

        // ------------------------------------------------------------------ islets

        void UpdateIslets(float scroll)
        {
            int used = 0;
            int k0 = Mathf.FloorToInt((scroll + MinZ - 8f) / IsletSlot);
            int k1 = Mathf.CeilToInt((scroll + MaxZ + 8f) / IsletSlot);
            for (int k = k0; k <= k1; k++)
            {
                if (Hash(k, 1) > IsletChance) continue;
                float side = Hash(k, 2) < 0.5f ? -1f : 1f;
                float x = side * (7.5f + Hash(k, 3) * 6f);
                float z = k * IsletSlot + Hash(k, 4) * 10f - scroll;
                float size = 0.8f + Hash(k, 5) * 0.6f;
                int variant = Mathf.Min(IsletVariants - 1, (int)(Hash(k, 6) * IsletVariants));

                if (used == _isletPool.Count) _isletPool.Add(NewIslet());
                var go = _isletPool[used++];
                go.SetActive(true);
                go.GetComponent<MeshFilter>().sharedMesh = _isletMeshes[variant];
                go.transform.localPosition = new Vector3(x, GroundY, z);
                go.transform.localRotation = Quaternion.Euler(0f, Hash(k, 7) * 360f, 0f);
                go.transform.localScale = Vector3.one * size;
            }
            for (int i = used; i < _isletPool.Count; i++) _isletPool[i].SetActive(false);
        }

        GameObject NewIslet()
        {
            var go = new GameObject("Islet");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Mats.IsletSand, Mats.IsletRock, Mats.IsletGreen };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        /// <summary>A small rocky island: a sand shelf, a lumpy rock and a cap of scrub.</summary>
        static Mesh IsletMesh(int variant)
        {
            const int Sand = 0, Rock = 1, Green = 2;
            var b = new MeshBuilder();
            float r = 2.4f + variant * 0.5f;
            var shelf = new Vector2[7];
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                float rr = r * (1.2f + 0.25f * Hash(variant * 10 + i, 40));
                shelf[i] = new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
            }
            b.ExtrudeXZ(shelf, -0.6f, 0.12f, Sand);
            b.Octahedron(new Vector3(0f, 0.15f, 0f), new Vector3(r, r * 0.42f, r * 0.85f), Rock);
            b.Octahedron(new Vector3(r * 0.45f, 0.1f, -r * 0.3f), new Vector3(r * 0.6f, r * 0.32f, r * 0.55f), Rock);
            b.Octahedron(new Vector3(0.15f, r * 0.33f, 0.05f), new Vector3(r * 0.6f, r * 0.24f, r * 0.55f), Green);
            if (variant == 2)
                b.Octahedron(new Vector3(-r * 0.5f, 0.2f, r * 0.35f), new Vector3(r * 0.45f, r * 0.5f, r * 0.4f), Rock);
            return b.Build("Islet" + variant);
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Scroll to <paramref name="time"/> seconds into the stage.</summary>
        public void SetTime(float time)
        {
            float scroll = time * ScrollSpeed;
            UpdateSea(time, scroll);
            UpdateIslets(scroll);
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
            if (_sea != null) Destroy(_sea);
            foreach (var m in _isletMeshes) if (m != null) Destroy(m);
        }
    }
}
