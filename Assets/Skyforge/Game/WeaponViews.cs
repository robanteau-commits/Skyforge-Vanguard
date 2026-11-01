using System.Collections.Generic;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Weapon effects that aren't projectiles: the Sunline Beam, the Solar Lance core, the Aegis
    /// Field ring, Arc Coil lightning and the Wingmate drones. Everything is glow geometry drawn
    /// with instancing (no transparency); purely visual.
    /// </summary>
    public sealed class WeaponViews
    {
        const float PlayY = -0.13f;

        struct ArcSegment
        {
            public Vector3 A, B;
            public float Life, Width;
        }

        readonly Mesh _box, _drone, _ring, _flare;
        readonly InstancedBatch _beamCore, _beamMid, _beamOuter, _beamRings, _beamFlare, _arcCore, _arcGlow, _aegis;
        readonly List<ArcSegment> _arcs = new List<ArcSegment>(64);
        readonly GameObject[] _wingmates = new GameObject[3];
        readonly Vector3[] _wingmatePos = new Vector3[3];

        public WeaponViews(Transform parent)
        {
            var b = new MeshBuilder();
            b.Box(Vector3.zero, Vector3.one, 0);
            _box = b.Build("UnitBox");
            var rb = new MeshBuilder();
            rb.Octahedron(Vector3.zero, new Vector3(0.5f, 0.03f, 0.08f), 0);
            _ring = rb.Build("BeamRing");
            var fb = new MeshBuilder();
            fb.Octahedron(Vector3.zero, new Vector3(0.35f, 0.2f, 0.35f), 0);
            _flare = fb.Build("BeamFlare");
            // Sunline Beam / Solar Lance: white core, cyan body, deep blue edge, rings running up it.
            _beamCore = new InstancedBatch(_box, Mats.BeamCore);
            _beamMid = new InstancedBatch(_box, Mats.Beam);
            _beamOuter = new InstancedBatch(_box, Mats.BeamOuter);
            _beamRings = new InstancedBatch(_ring, Mats.Beam);
            _beamFlare = new InstancedBatch(_flare, Mats.BeamCore);
            // Arc Coil: violet lightning with a white core.
            _arcCore = new InstancedBatch(_box, Mats.ArcCore);
            _arcGlow = new InstancedBatch(_box, Mats.Arc);
            _aegis = new InstancedBatch(_box, Mats.PlayerGlow);

            _drone = DroneMesh();
            for (int i = 0; i < _wingmates.Length; i++)
            {
                var go = new GameObject("Wingmate" + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = _drone;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { Mats.Hull, Mats.HullDark, Mats.PlayerGlow };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _wingmates[i] = go;
            }
        }

        /// <summary>A small escort drone: a little delta with a glowing eye and engine. Nose +Z.</summary>
        static Mesh DroneMesh()
        {
            var b = new MeshBuilder();
            b.ExtrudeXZ(new[] { new Vector2(0f, 0.7f), new Vector2(0.6f, -0.4f), new Vector2(0f, -0.2f), new Vector2(-0.6f, -0.4f) }, -0.05f, 0.05f, 0, 1);
            b.Octahedron(new Vector3(0f, 0.06f, 0.05f), new Vector3(0.14f, 0.1f, 0.35f), 1);
            b.Octahedron(new Vector3(0f, 0.14f, 0.2f), new Vector3(0.06f, 0.05f, 0.1f), 2);
            b.Box(new Vector3(0f, 0f, -0.3f), new Vector3(0.12f, 0.06f, 0.08f), 2);
            return b.Build("Wingmate");
        }

        /// <summary>Pick up arc bolts from this step's events.</summary>
        public void Consume(List<SimEvent> events)
        {
            foreach (var e in events)
            {
                if (e.Type != SimEventType.Arc) continue;
                // A jagged bolt: five segments with random sideways kinks, and usually a small fork.
                var a = new Vector3(e.Pos.X, PlayY, e.Pos.Y);
                var b = new Vector3(e.To.X, PlayY, e.To.Y);
                var side = Vector3.Cross(b - a, Vector3.up).normalized;
                var prev = a;
                const int n = 5;
                for (int k = 1; k <= n; k++)
                {
                    var next = k == n ? b : Vector3.Lerp(a, b, k / (float)n) + side * Random.Range(-0.5f, 0.5f);
                    _arcs.Add(new ArcSegment { A = prev, B = next, Life = 0.09f, Width = 0.07f });
                    if (k == 2 && Random.value < 0.7f)
                    {
                        var fork = next + side * Random.Range(-1f, 1f) + (b - a).normalized * Random.Range(-0.5f, 0.8f);
                        _arcs.Add(new ArcSegment { A = next, B = fork, Life = 0.07f, Width = 0.045f });
                    }
                    prev = next;
                }
            }
        }

        static Matrix4x4 Segment(Vector3 a, Vector3 b, float width, float height)
        {
            var d = b - a;
            float len = d.magnitude;
            var rot = len > 1e-4f ? Quaternion.LookRotation(d / len) : Quaternion.identity;
            return Matrix4x4.TRS((a + b) * 0.5f, rot, new Vector3(width, height, len));
        }

        public void Render(StageSim sim, Vec2 shipPos, float time, float dt)
        {
            _beamCore.Clear(); _beamMid.Clear(); _beamOuter.Clear(); _beamRings.Clear(); _beamFlare.Clear();
            _arcCore.Clear(); _arcGlow.Clear(); _aegis.Clear();
            var p = sim.Player;
            var ship = new Vector3(shipPos.X, PlayY, shipPos.Y);
            float pulse = 1f + 0.12f * Mathf.Sin(time * 30f);

            // Sunline Beam, per wing.
            for (int i = 0; i < 2; i++)
            {
                if (!p.BeamOn[i] || !p.Alive) continue;
                var wing = sim.Loadout.Wing(i == 0 ? Side.Left : Side.Right);
                if (wing.IsEmpty) continue;
                var muzzle = shipPos + PlayerShip.PodOffset(i == 0 ? Side.Left : Side.Right);
                var a = new Vector3(muzzle.X, PlayY, muzzle.Y);
                var b = new Vector3(muzzle.X, PlayY, Mathf.Max(muzzle.Y + 0.1f, p.BeamEndY[i]));
                float w = wing.Stats.Width;
                // Thicker and brighter-cored as the damage ramps up on a target.
                float ramp = wing.Stats.RampTime > 0f ? Mathf.Clamp01(p.BeamTime[i] / wing.Stats.RampTime) : 0f;
                Beam(a, b, w * (1f + 0.3f * ramp), pulse, time + i * 0.37f, p.BeamTarget[i] != 0);
            }

            // Solar Lance: a wide beam from the ship to past the top of the screen.
            if (sim.SolarLanceUp && p.Alive)
            {
                float w = sim.CoreStats.Width;
                var a = ship + new Vector3(0f, 0f, 0.8f);
                var b = new Vector3(ship.x, PlayY, Field.HalfHeight + 3f);
                float wobble = 1f + 0.06f * Mathf.Sin(time * 22f);
                Beam(a, b, w, wobble, time, false);
            }

            // Aegis Field: a rotating ring of glowing segments around the ship.
            if (sim.AegisUp && p.Alive)
            {
                const int n = 14;
                float r = 1.9f + 0.08f * Mathf.Sin(time * 6f);
                for (int k = 0; k < n; k++)
                {
                    float a0 = time * 1.2f + k * Mathf.PI * 2f / n;
                    float a1 = a0 + Mathf.PI * 2f / n * 0.7f;
                    var pa = ship + new Vector3(Mathf.Cos(a0) * r, 0.1f, Mathf.Sin(a0) * r);
                    var pb = ship + new Vector3(Mathf.Cos(a1) * r, 0.1f, Mathf.Sin(a1) * r);
                    _aegis.Add(Segment(pa, pb, 0.08f, 0.06f));
                }
            }

            // Arc Coil lightning.
            for (int k = _arcs.Count - 1; k >= 0; k--)
            {
                var s = _arcs[k];
                s.Life -= dt;
                if (s.Life <= 0f) { _arcs.RemoveAt(k); continue; }
                _arcs[k] = s;
                _arcGlow.Add(Segment(s.A + Vector3.down * 0.03f, s.B + Vector3.down * 0.03f, s.Width * 2.6f, 0.03f));
                _arcCore.Add(Segment(s.A, s.B, s.Width, 0.05f));
            }

            _beamOuter.Draw(); _beamMid.Draw(); _beamCore.Draw(); _beamRings.Draw(); _beamFlare.Draw();
            _arcGlow.Draw(); _arcCore.Draw(); _aegis.Draw();

            // Wingmates follow the ship with a little lag.
            for (int i = 0; i < _wingmates.Length; i++)
            {
                var go = _wingmates[i];
                bool on = p.Alive && i < p.WingmateCount;
                if (go.activeSelf != on)
                {
                    go.SetActive(on);
                    if (on) _wingmatePos[i] = ship;
                }
                if (!on) continue;
                var off = PlayerShip.WingmateOffset(i);
                var target = ship + new Vector3(off.X, 0.1f * Mathf.Sin(time * 3f + i), off.Y);
                _wingmatePos[i] = Vector3.Lerp(_wingmatePos[i], target, 1f - Mathf.Exp(-dt * 12f));
                go.transform.localPosition = _wingmatePos[i];
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(time * 2f + i));
            }
        }

        /// <summary>A layered beam from <paramref name="a"/> to <paramref name="b"/> with rings running along it
        /// and, if it's burning a target, a flare at the end.</summary>
        void Beam(Vector3 a, Vector3 b, float width, float pulse, float time, bool flare)
        {
            _beamOuter.Add(Segment(a + Vector3.down * 0.06f, b + Vector3.down * 0.06f, width * 2.1f * pulse, 0.02f));
            _beamMid.Add(Segment(a, b, width * pulse, 0.04f));
            _beamCore.Add(Segment(a + Vector3.up * 0.05f, b + Vector3.up * 0.05f, width * 0.35f, 0.06f));
            var d = b - a;
            float len = d.magnitude;
            if (len > 0.5f)
            {
                var dir = d / len;
                var rot = Quaternion.LookRotation(dir);
                const int rings = 5;
                for (int k = 0; k < rings; k++)
                {
                    float f = Mathf.Repeat(time * 2.2f + k / (float)rings, 1f);
                    _beamRings.Add(Matrix4x4.TRS(a + d * f + Vector3.up * 0.08f, rot, new Vector3(width * 2.4f * pulse, 1f, 1f)));
                }
            }
            if (flare) _beamFlare.Add(Matrix4x4.TRS(b + Vector3.up * 0.2f, Quaternion.Euler(0f, time * 400f, 0f), Vector3.one * (0.8f + 0.4f * Mathf.Sin(time * 40f)) * (width / 0.35f)));
        }

        public void Clear()
        {
            _arcs.Clear();
            foreach (var go in _wingmates) go.SetActive(false);
        }

        public void Dispose()
        {
            if (_box != null) Object.Destroy(_box);
            if (_ring != null) Object.Destroy(_ring);
            if (_flare != null) Object.Destroy(_flare);
            if (_drone != null) Object.Destroy(_drone);
        }
    }
}
