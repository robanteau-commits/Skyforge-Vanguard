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
            public float Life;
        }

        readonly Mesh _box, _drone;
        readonly InstancedBatch _beamCore, _beamGlow, _arc, _aegis;
        readonly List<ArcSegment> _arcs = new List<ArcSegment>(64);
        readonly GameObject[] _wingmates = new GameObject[3];
        readonly Vector3[] _wingmatePos = new Vector3[3];

        public WeaponViews(Transform parent)
        {
            var b = new MeshBuilder();
            b.Box(Vector3.zero, Vector3.one, 0);
            _box = b.Build("UnitBox");
            _beamCore = new InstancedBatch(_box, Mats.PlayerShot);
            _beamGlow = new InstancedBatch(_box, Mats.PlayerGlowSoft);
            _arc = new InstancedBatch(_box, Mats.PlayerShot);
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
                // A jagged bolt: four segments with random sideways kinks.
                var a = new Vector3(e.Pos.X, PlayY, e.Pos.Y);
                var b = new Vector3(e.To.X, PlayY, e.To.Y);
                var side = Vector3.Cross(b - a, Vector3.up).normalized;
                var prev = a;
                for (int k = 1; k <= 4; k++)
                {
                    var next = k == 4 ? b : Vector3.Lerp(a, b, k / 4f) + side * Random.Range(-0.45f, 0.45f);
                    _arcs.Add(new ArcSegment { A = prev, B = next, Life = 0.09f });
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
            _beamCore.Clear(); _beamGlow.Clear(); _arc.Clear(); _aegis.Clear();
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
                // Brighter as the damage ramps up on a target.
                float ramp = wing.Stats.RampTime > 0f ? Mathf.Clamp01(p.BeamTime[i] / wing.Stats.RampTime) : 0f;
                _beamCore.Add(Segment(a, b, w * (0.35f + 0.2f * ramp) * pulse, 0.06f));
                _beamGlow.Add(Segment(a + Vector3.down * 0.05f, b, w * pulse, 0.04f));
            }

            // Solar Lance: a wide beam from the ship to past the top of the screen.
            if (sim.SolarLanceUp && p.Alive)
            {
                float w = sim.CoreStats.Width;
                var a = ship + new Vector3(0f, 0f, 0.8f);
                var b = new Vector3(ship.x, PlayY, Field.HalfHeight + 3f);
                float wobble = 1f + 0.06f * Mathf.Sin(time * 22f);
                _beamCore.Add(Segment(a, b, w * 0.45f * wobble, 0.1f));
                _beamGlow.Add(Segment(a + Vector3.down * 0.08f, b, w * wobble, 0.06f));
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
                _arc.Add(Segment(s.A, s.B, 0.07f, 0.05f));
            }

            _beamGlow.Draw(); _beamCore.Draw(); _arc.Draw(); _aegis.Draw();

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

        public void Clear()
        {
            _arcs.Clear();
            foreach (var go in _wingmates) go.SetActive(false);
        }

        public void Dispose()
        {
            if (_box != null) Object.Destroy(_box);
            if (_drone != null) Object.Destroy(_drone);
        }
    }
}
