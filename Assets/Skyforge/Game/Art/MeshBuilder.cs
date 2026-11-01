using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Skyforge.Game
{
    /// <summary>
    /// Builds flat-shaded low-poly meshes: every triangle gets its own vertices, so each facet has
    /// one normal and reads as a crisp plane. Faces are given with an outward direction and wound
    /// automatically, so callers never have to think about winding (mirrored transforms included).
    /// Each sub-mesh index maps to one material slot.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<List<int>> _subs = new List<List<int>>();
        Matrix4x4 _m = Matrix4x4.identity;
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public int VertexCount => _verts.Count;

        /// <summary>Apply <paramref name="m"/> to everything added until the matching <see cref="Pop"/>.</summary>
        public void Push(Matrix4x4 m)
        {
            _stack.Push(_m);
            _m = _m * m;
        }

        public void Pop() => _m = _stack.Pop();

        /// <summary>Mirror the X axis (build the right side, then mirror it for the left).</summary>
        public static Matrix4x4 MirrorX => Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        List<int> Sub(int sub)
        {
            while (_subs.Count <= sub) _subs.Add(new List<int>());
            return _subs[sub];
        }

        /// <summary>One triangle in world (builder) space, wound so its normal faces <paramref name="outward"/>.</summary>
        void TriRaw(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int sub)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return; // degenerate
            if (Vector3.Dot(n, outward) < 0f) { var t = b; b = c; c = t; n = -n; }
            n.Normalize();
            var list = Sub(sub);
            int i = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            _normals.Add(n); _normals.Add(n); _normals.Add(n);
            list.Add(i); list.Add(i + 1); list.Add(i + 2);
        }

        /// <summary>A flat polygon (convex, points in order), facing <paramref name="outward"/> (local space).</summary>
        public void Poly(IList<Vector3> pts, Vector3 outward, int sub)
        {
            if (pts.Count < 3) return;
            var w = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) w[i] = _m.MultiplyPoint3x4(pts[i]);
            var o = _m.MultiplyVector(outward);
            for (int i = 1; i < w.Length - 1; i++) TriRaw(w[0], w[i], w[i + 1], o, sub);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int sub) =>
            Poly(new[] { a, b, c }, outward, sub);

        /// <summary>A flat 2D outline (X/Z, convex) extruded between heights <paramref name="y0"/> and <paramref name="y1"/>.</summary>
        public void ExtrudeXZ(IList<Vector2> outline, float y0, float y1, int sub, int sideSub = -1)
        {
            if (sideSub < 0) sideSub = sub;
            int n = outline.Count;
            var top = new Vector3[n];
            var bottom = new Vector3[n];
            var centre = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                top[i] = new Vector3(outline[i].x, y1, outline[i].y);
                bottom[i] = new Vector3(outline[i].x, y0, outline[i].y);
                centre += outline[i];
            }
            centre /= n;
            Poly(top, Vector3.up, sub);
            Poly(bottom, Vector3.down, sub);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var mid = (outline[i] + outline[j]) * 0.5f - centre;
                Poly(new[] { bottom[i], bottom[j], top[j], top[i] }, new Vector3(mid.x, 0f, mid.y), sideSub);
            }
        }

        /// <summary>A flat 2D outline in the Z/Y plane (a fin), extruded across X by <paramref name="thickness"/>.</summary>
        public void ExtrudeZY(IList<Vector2> outline, float thickness, int sub)
        {
            int n = outline.Count;
            float h = thickness * 0.5f;
            var a = new Vector3[n];
            var b = new Vector3[n];
            var centre = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                a[i] = new Vector3(-h, outline[i].y, outline[i].x);
                b[i] = new Vector3(h, outline[i].y, outline[i].x);
                centre += outline[i];
            }
            centre /= n;
            Poly(a, Vector3.left, sub);
            Poly(b, Vector3.right, sub);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var mid = (outline[i] + outline[j]) * 0.5f - centre;
                Poly(new[] { a[i], a[j], b[j], b[i] }, new Vector3(0f, mid.y, mid.x), sub);
            }
        }

        /// <summary>One cross-section of a <see cref="Loft"/>: an ellipse-ish ring at depth Z.</summary>
        public struct Ring
        {
            public float Z, HalfWidth, HalfHeight, Y;
            public Ring(float z, float halfWidth, float halfHeight, float y = 0f)
            { Z = z; HalfWidth = halfWidth; HalfHeight = halfHeight; Y = y; }
        }

        /// <summary>
        /// A faceted body along the Z axis through the given rings (front to back or back to front).
        /// <paramref name="sides"/> = facets around; <paramref name="startDeg"/> is the angle of the
        /// first corner (90 puts a corner on top: a hexagon then has a spine and flat sides).
        /// <paramref name="x"/> shifts the axis sideways. Ends are capped.
        /// </summary>
        public void Loft(IList<Ring> rings, int sides, int sub, float startDeg = 90f, float x = 0f)
        {
            var pts = new Vector3[rings.Count][];
            for (int r = 0; r < rings.Count; r++)
            {
                pts[r] = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float a = (startDeg + 360f * k / sides) * Mathf.Deg2Rad;
                    pts[r][k] = new Vector3(x + Mathf.Cos(a) * rings[r].HalfWidth,
                        rings[r].Y + Mathf.Sin(a) * rings[r].HalfHeight, rings[r].Z);
                }
            }
            for (int r = 0; r < rings.Count - 1; r++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    var p0 = pts[r][k]; var p1 = pts[r][k2]; var p2 = pts[r + 1][k2]; var p3 = pts[r + 1][k];
                    var mid = (p0 + p1 + p2 + p3) * 0.25f;
                    float cy = (rings[r].Y + rings[r + 1].Y) * 0.5f;
                    var outward = new Vector3(mid.x - x, mid.y - cy, 0f);
                    Tri(p0, p1, p2, outward, sub);
                    Tri(p0, p2, p3, outward, sub);
                }
            }
            float dir = rings[0].Z > rings[rings.Count - 1].Z ? 1f : -1f;
            if (rings[0].HalfWidth > 1e-4f) Poly(pts[0], new Vector3(0f, 0f, dir), sub);
            int last = rings.Count - 1;
            if (rings[last].HalfWidth > 1e-4f) Poly(pts[last], new Vector3(0f, 0f, -dir), sub);
        }

        /// <summary>An axis-aligned box.</summary>
        public void Box(Vector3 centre, Vector3 size, int sub)
        {
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => centre + new Vector3(x * h.x, y * h.y, z * h.z);
            Poly(new[] { P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1) }, Vector3.up, sub);
            Poly(new[] { P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1) }, Vector3.down, sub);
            Poly(new[] { P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1) }, Vector3.forward, sub);
            Poly(new[] { P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1) }, Vector3.back, sub);
            Poly(new[] { P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1) }, Vector3.right, sub);
            Poly(new[] { P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1) }, Vector3.left, sub);
        }

        /// <summary>A thin box running from <paramref name="a"/> to <paramref name="b"/> (e.g. a glowing edge strip).</summary>
        public void Beam(Vector3 a, Vector3 b, float width, float height, int sub)
        {
            var dir = b - a;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Push(Matrix4x4.TRS((a + b) * 0.5f, rot, Vector3.one));
            Box(Vector3.zero, new Vector3(width, height, dir.magnitude), sub);
            Pop();
        }

        /// <summary>A diamond: six points on the axes. Good for crystals, bolts and shards.</summary>
        public void Octahedron(Vector3 c, Vector3 r, int sub)
        {
            Vector3 px = c + new Vector3(r.x, 0, 0), nx = c - new Vector3(r.x, 0, 0);
            Vector3 py = c + new Vector3(0, r.y, 0), ny = c - new Vector3(0, r.y, 0);
            Vector3 pz = c + new Vector3(0, 0, r.z), nz = c - new Vector3(0, 0, r.z);
            void F(Vector3 a, Vector3 b, Vector3 d) => Tri(a, b, d, (a + b + d) / 3f - c, sub);
            F(py, px, pz); F(py, pz, nx); F(py, nx, nz); F(py, nz, px);
            F(ny, pz, px); F(ny, nx, pz); F(ny, nz, nx); F(ny, px, nz);
        }

        /// <summary>A 4-sided pyramid-ish shard (tetrahedron) for debris.</summary>
        public void Tetra(Vector3 c, float r, int sub)
        {
            var a = c + new Vector3(0f, r, 0f);
            var b = c + new Vector3(r * 0.94f, -r * 0.33f, 0f);
            var d = c + new Vector3(-r * 0.47f, -r * 0.33f, r * 0.82f);
            var e = c + new Vector3(-r * 0.47f, -r * 0.33f, -r * 0.82f);
            void F(Vector3 p, Vector3 q, Vector3 s) => Tri(p, q, s, (p + q + s) / 3f - c, sub);
            F(a, b, d); F(a, d, e); F(a, e, b); F(b, e, d);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.subMeshCount = Mathf.Max(1, _subs.Count);
            for (int s = 0; s < _subs.Count; s++) mesh.SetTriangles(_subs[s], s, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Builds the mesh onto a new child object with one material per sub-mesh.</summary>
        public GameObject BuildObject(string name, Transform parent, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Build(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }
}
