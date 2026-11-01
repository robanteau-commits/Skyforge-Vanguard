using UnityEngine;

namespace Skyforge.Game
{
    /// <summary>
    /// The world map's backdrop: a low-poly Earth (dark oceans, muted land, white polar caps) turning
    /// slowly below the stage nodes, with a thin cyan atmosphere ring. Built from a subdivided
    /// octahedron, flat-shaded; land is chosen per facet from a hash-based noise so it looks the same
    /// every time.
    /// </summary>
    public static class GlobeModel
    {
        const float Radius = 9f;
        static readonly Vector3 Centre = new Vector3(0f, -12f, 1f);

        public static GameObject Create(Transform parent)
        {
            var b = new MeshBuilder();
            var tris = new System.Collections.Generic.List<(Vector3, Vector3, Vector3)>();
            Vector3[] v = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[,] f = { { 0, 4, 3 }, { 0, 3, 5 }, { 0, 5, 2 }, { 0, 2, 4 }, { 1, 3, 4 }, { 1, 5, 3 }, { 1, 2, 5 }, { 1, 4, 2 } };
            for (int i = 0; i < 8; i++) tris.Add((v[f[i, 0]], v[f[i, 1]], v[f[i, 2]]));
            for (int level = 0; level < 3; level++)
            {
                var next = new System.Collections.Generic.List<(Vector3, Vector3, Vector3)>();
                foreach (var (a, c, d) in tris)
                {
                    var ac = ((a + c) * 0.5f).normalized; var cd = ((c + d) * 0.5f).normalized; var da = ((d + a) * 0.5f).normalized;
                    next.Add((a, ac, da)); next.Add((ac, c, cd)); next.Add((da, cd, d)); next.Add((ac, cd, da));
                }
                tris = next;
            }
            foreach (var (a, c, d) in tris)
            {
                var mid = (a + c + d) / 3f;
                int sub = Mathf.Abs(mid.y) > 0.86f ? 2 : Land(mid) ? 1 : 0;
                float lift = sub == 0 ? 1f : 1.015f;
                b.Tri(a * Radius * lift, c * Radius * lift, d * Radius * lift, mid, sub);
            }
            var go = b.BuildObject("Globe", parent, Mats.Lit("globeOcean", new Color(0.04f, 0.09f, 0.16f), 0.1f, 0.6f),
                Mats.Lit("globeLand", new Color(0.16f, 0.2f, 0.15f), 0f, 0.1f), Mats.Lit("globeIce", new Color(0.7f, 0.74f, 0.8f), 0f, 0.3f));

            // Atmosphere: a thin ring of glow around the limb.
            var ring = new MeshBuilder();
            const int n = 48;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                ring.Beam(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (Radius * 1.04f), new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (Radius * 1.04f), 0.12f, 0.12f, 0);
            }
            var halo = ring.BuildObject("Atmosphere", go.transform, Mats.PlayerGlowSoft);
            go.transform.localPosition = Centre;
            go.SetActive(false);
            return go;
        }

        /// <summary>Show the globe on the map screen, turning slowly.</summary>
        public static void Show(GameObject globe, bool on, float time)
        {
            if (globe == null) return;
            if (globe.activeSelf != on) globe.SetActive(on);
            if (!on) return;
            globe.transform.localRotation = Quaternion.Euler(-20f, time * 4f, 0f);
            var halo = globe.transform.Find("Atmosphere");
            // Keep the ring facing the camera (which looks down, leaning 20° forward) as the globe turns.
            if (halo != null) halo.rotation = Quaternion.Euler(-PlayfieldCamera.Tilt, 0f, 0f);
        }

        static bool Land(Vector3 p)
        {
            float n = 0.55f * Noise(p * 2.2f) + 0.3f * Noise(p * 4.7f + Vector3.one * 3f) + 0.15f * Noise(p * 9f);
            return n > 0.52f;
        }

        static float Noise(Vector3 p)
        {
            var i = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
            var f = p - (Vector3)i;
            f = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y), f.z * f.z * (3f - 2f * f.z));
            float H(int x, int y, int z)
            {
                unchecked
                {
                    uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
                    h = (h ^ (h >> 13)) * 1274126177u;
                    return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
                }
            }
            float L(float a, float b2, float t) => a + (b2 - a) * t;
            return L(L(L(H(i.x, i.y, i.z), H(i.x + 1, i.y, i.z), f.x), L(H(i.x, i.y + 1, i.z), H(i.x + 1, i.y + 1, i.z), f.x), f.y),
                     L(L(H(i.x, i.y, i.z + 1), H(i.x + 1, i.y, i.z + 1), f.x), L(H(i.x, i.y + 1, i.z + 1), H(i.x + 1, i.y + 1, i.z + 1), f.x), f.y), f.z);
        }
    }
}
