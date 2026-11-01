using System.Collections.Generic;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Draws every projectile and pickup with GPU instancing.
    /// <para>
    /// Player weapons follow the "Weapon effects" render page: each has its own shape, hue and trail
    /// (Pulse: cyan bolts with a tail; Scatter: sky-blue flak pellets; Lance: ice-white spears with
    /// chevrons; Seeker: alloy missiles with an exhaust plume and grey smoke; Hornet: mint darts with
    /// dotted trails; Ricochet: spinning aqua saws). A look is several parts (mesh + material) drawn
    /// with the same matrix, so each part keeps its own colour and glow.
    /// </para>
    /// <para>
    /// Enemy bullets are the brightest warm things on screen (orange-pink) with a dark maroon rim,
    /// so they read over anything; darts point along their flight.
    /// </para>
    /// </summary>
    public sealed class ShotRenderer
    {
        const float PlayY = -0.13f;

        /// <summary>One visual part of a projectile: a mesh in the projectile's local space, one material.</summary>
        sealed class Look
        {
            public readonly List<InstancedBatch> Parts = new List<InstancedBatch>();
            public void Add(in Matrix4x4 m) { foreach (var p in Parts) p.Add(m); }
            public void Clear() { foreach (var p in Parts) p.Clear(); }
            public void Draw() { foreach (var p in Parts) p.Draw(); }
        }

        struct TrailBit
        {
            public Vector3 Pos, Vel;
            public float Life, Max, Size;
            public Quaternion Rot;
        }

        sealed class Trail
        {
            public readonly InstancedBatch Batch;
            public readonly List<TrailBit> Bits = new List<TrailBit>(256);
            public Trail(Mesh mesh, Material mat) { Batch = new InstancedBatch(mesh, mat); }
        }

        readonly Look _pulse, _pellet, _lance, _missile, _micro, _disc;
        readonly InstancedBatch _plume;
        readonly Trail _smoke, _hornetDots, _pulseDots;
        readonly InstancedBatch _orb, _orbRim, _dart, _dartRim, _credit, _repair;
        readonly List<Mesh> _meshes = new List<Mesh>();

        public ShotRenderer()
        {
            // Player projectiles. Local space: forward is +Z, up is +Y.
            _pulse = Make(
                (b => b.Octahedron(Vector3.zero, new Vector3(0.14f, 0.08f, 0.5f), 0), Mats.Pulse),
                (b => b.Octahedron(new Vector3(0f, 0.04f, 0.02f), new Vector3(0.06f, 0.06f, 0.36f), 0), Mats.PulseCore),
                (b => b.Octahedron(new Vector3(0f, 0f, -0.7f), new Vector3(0.05f, 0.03f, 0.55f), 0), Mats.PulseTrail));
            _pellet = Make(
                (b => b.Octahedron(Vector3.zero, new Vector3(0.14f, 0.12f, 0.14f), 0), Mats.Scatter),
                (b => b.Octahedron(new Vector3(0f, 0.06f, 0f), new Vector3(0.07f, 0.06f, 0.07f), 0), Mats.ScatterCore),
                (b => b.Octahedron(new Vector3(0f, 0f, -0.28f), new Vector3(0.04f, 0.03f, 0.22f), 0), Mats.ScatterTrail));
            _lance = Make(
                (b => b.Octahedron(Vector3.zero, new Vector3(0.17f, 0.09f, 1.25f), 0), Mats.Lance),
                (b => b.Octahedron(new Vector3(0f, 0.05f, 0f), new Vector3(0.06f, 0.06f, 1f), 0), Mats.LanceCore),
                (b =>
                {
                    for (int i = 1; i <= 3; i++)
                        b.Octahedron(new Vector3(0f, 0f, -(1.1f + i * 0.45f)), new Vector3(0.22f - i * 0.04f, 0.03f, 0.06f), 0);
                }, Mats.LanceTrail));
            _missile = Make(
                (b =>
                {
                    b.Loft(new[]
                    {
                        new MeshBuilder.Ring(0.68f, 0f, 0f),
                        new MeshBuilder.Ring(0.4f, 0.12f, 0.12f),
                        new MeshBuilder.Ring(-0.4f, 0.13f, 0.13f),
                    }, 6, 0, startDeg: 90f);
                }, Mats.HullLight),
                (b =>
                {
                    for (int k = 0; k < 4; k++)
                    {
                        b.Push(Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 45f + 90f * k)));
                        b.Box(new Vector3(0f, 0.17f, -0.28f), new Vector3(0.03f, 0.2f, 0.22f), 0);
                        b.Pop();
                    }
                }, Mats.HullDark));
            _plume = new InstancedBatch(Mesh(b => b.Octahedron(new Vector3(0f, 0f, -0.8f), new Vector3(0.19f, 0.14f, 0.6f), 0)), Mats.Exhaust);
            _micro = Make(
                (b => b.Octahedron(Vector3.zero, new Vector3(0.1f, 0.07f, 0.26f), 0), Mats.Hornet),
                (b => b.Octahedron(Vector3.zero, new Vector3(0.16f, 0.05f, 0.16f), 0), Mats.HornetTrail));
            _disc = Make(
                (b =>
                {
                    const int n = 16;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * 360f / n;
                        b.Push(Matrix4x4.Rotate(Quaternion.Euler(0f, a, 0f)));
                        b.Box(new Vector3(0f, 0f, 0.3f), new Vector3(0.14f, 0.05f, 0.05f), 0);
                        if (i % 3 == 0) b.Octahedron(new Vector3(0f, 0f, 0.42f), new Vector3(0.05f, 0.025f, 0.09f), 0);
                        b.Pop();
                    }
                }, Mats.Disc),
                (b => b.Octahedron(Vector3.zero, new Vector3(0.1f, 0.06f, 0.1f), 0), Mats.DiscHub),
                (b =>
                {
                    var plate = new Vector2[12];
                    for (int i = 0; i < 12; i++) { float a = i * Mathf.PI * 2f / 12f; plate[i] = new Vector2(Mathf.Cos(a) * 0.26f, Mathf.Sin(a) * 0.26f); }
                    b.ExtrudeXZ(plate, -0.03f, -0.01f, 0);
                }, Mats.DiscPlate));

            // Trails.
            var blob = Mesh(b => b.Octahedron(Vector3.zero, new Vector3(1f, 0.7f, 1f), 0));
            _smoke = new Trail(blob, Mats.Smoke);
            _hornetDots = new Trail(blob, Mats.HornetTrail);
            _pulseDots = new Trail(blob, Mats.PulseTrail);

            // Enemy bullets and pickups.
            _orb = new InstancedBatch(Diamond(new Vector3(0.2f, 0.2f, 0.2f)), Mats.EnemyBullet);
            _orbRim = new InstancedBatch(Diamond(new Vector3(0.3f, 0.1f, 0.3f)), Mats.BulletRim);
            _dart = new InstancedBatch(Diamond(new Vector3(0.1f, 0.08f, 0.32f)), Mats.EnemyBullet);
            _dartRim = new InstancedBatch(Diamond(new Vector3(0.16f, 0.06f, 0.42f)), Mats.BulletRim);
            _credit = new InstancedBatch(Diamond(new Vector3(0.14f, 0.2f, 0.14f)), Mats.Credit);
            _repair = new InstancedBatch(Diamond(new Vector3(0.3f, 0.3f, 0.3f)), Mats.Repair);
        }

        Mesh Mesh(System.Action<MeshBuilder> build)
        {
            var b = new MeshBuilder();
            build(b);
            var m = b.Build("Shot");
            _meshes.Add(m);
            return m;
        }

        Mesh Diamond(Vector3 r) => Mesh(b => b.Octahedron(Vector3.zero, r, 0));

        Look Make(params (System.Action<MeshBuilder> build, Material mat)[] parts)
        {
            var look = new Look();
            foreach (var (build, mat) in parts) look.Parts.Add(new InstancedBatch(Mesh(build), mat));
            return look;
        }

        /// <summary>
        /// Draw everything as it was <paramref name="behind"/> seconds before the latest step
        /// (render interpolation: 0 = latest position). <paramref name="dt"/> advances the trails (0 when paused).
        /// </summary>
        public void Draw(StageSim sim, float behind, float time, float dt = 0f)
        {
            _pulse.Clear(); _pellet.Clear(); _lance.Clear(); _missile.Clear(); _micro.Clear(); _disc.Clear(); _plume.Clear();
            foreach (var s in sim.Shots)
            {
                var p = s.Pos - s.Vel * behind;
                var pos = new Vector3(p.X, PlayY, p.Y);
                var dir = new Vector3(s.Vel.X, 0f, s.Vel.Y);
                var along = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
                switch (s.Visual)
                {
                    case ShotVisual.Pellet:
                        _pellet.Add(Matrix4x4.TRS(pos, along, Vector3.one));
                        break;
                    case ShotVisual.Shrapnel:
                        _pellet.Add(Matrix4x4.TRS(pos, along, Vector3.one * 0.55f));
                        break;
                    case ShotVisual.ChargedBolt:
                    {
                        // A fat, pulsing bolt: the pulse look at double size.
                        float throb = 2.2f + 0.25f * Mathf.Sin(time * 40f);
                        _pulse.Add(Matrix4x4.TRS(pos, along, new Vector3(throb, throb, 1.8f)));
                        Emit(_pulseDots, pos - dir.normalized * 0.9f, dt, 40f, 0.18f, 0.12f, Vector3.zero);
                        break;
                    }
                    case ShotVisual.Bomblet:
                        _plume.Add(Matrix4x4.TRS(pos + Vector3.forward * 0.4f, Quaternion.identity, Vector3.one * 0.5f)); // the plume mesh sits 0.8 behind its origin
                        break;
                    case ShotVisual.LanceBolt:
                        _lance.Add(Matrix4x4.TRS(pos, along, Vector3.one * (s.Radius / 0.22f)));
                        break;
                    case ShotVisual.Missile:
                    {
                        _missile.Add(Matrix4x4.TRS(pos, along, Vector3.one));
                        float flicker = 0.75f + 0.5f * Mathf.PerlinNoise(time * 25f, s.Pos.X * 3f);
                        _plume.Add(Matrix4x4.TRS(pos, along, new Vector3(1f, 1f, flicker)));
                        Emit(_smoke, pos - dir.normalized * 0.75f, dt, 60f, 0.6f, Random.Range(0.13f, 0.2f),
                            new Vector3(Random.Range(-0.4f, 0.4f), 0f, -1.5f));
                        break;
                    }
                    case ShotVisual.MicroMissile:
                        _micro.Add(Matrix4x4.TRS(pos, along, Vector3.one));
                        Emit(_hornetDots, pos, dt, 60f, 0.25f, 0.09f, Vector3.zero);
                        break;
                    case ShotVisual.Disc:
                        _disc.Add(Matrix4x4.TRS(pos, Quaternion.Euler(0f, time * 900f, 0f), Vector3.one * (s.Radius / 0.35f)));
                        break;
                    default: // PulseBolt
                        _pulse.Add(Matrix4x4.TRS(pos, along, Vector3.one));
                        Emit(_pulseDots, pos - dir.normalized * 0.5f, dt, 18f, 0.1f, 0.05f, Vector3.zero);
                        break;
                }
            }
            _pulse.Draw(); _pellet.Draw(); _lance.Draw(); _missile.Draw(); _plume.Draw(); _micro.Draw(); _disc.Draw();
            UpdateTrail(_smoke, dt);
            UpdateTrail(_hornetDots, dt);
            UpdateTrail(_pulseDots, dt);

            _orb.Clear(); _orbRim.Clear(); _dart.Clear(); _dartRim.Clear();
            var rot = Quaternion.identity;
            var one = Vector3.one;
            foreach (var s in sim.EnemyShots)
            {
                var p = s.Pos - s.Vel * behind;
                var pos = new Vector3(p.X, 0.15f, p.Y);
                var rim = new Vector3(p.X, 0.05f, p.Y);
                if (s.Visual == EnemyShotVisual.Dart)
                {
                    var r = s.Vel.LengthSq > 0.01f ? Quaternion.LookRotation(new Vector3(s.Vel.X, 0f, s.Vel.Y)) : rot;
                    _dart.Add(pos, r, one);
                    _dartRim.Add(rim, r, one);
                }
                else
                {
                    _orb.Add(pos, rot, one);
                    _orbRim.Add(rim, rot, one);
                }
            }
            _orbRim.Draw(); _dartRim.Draw(); _orb.Draw(); _dart.Draw();

            _credit.Clear(); _repair.Clear();
            foreach (var k in sim.Pickups)
            {
                var p = k.Pos - k.Vel * behind;
                var spin = Quaternion.Euler(0f, time * 240f + k.Id * 37f, 0f);
                if (k.Kind == PickupKind.Credit) _credit.Add(new Vector3(p.X, 0f, p.Y), spin, one);
                else _repair.Add(new Vector3(p.X, 0f, p.Y), spin, one * (1f + 0.12f * Mathf.Sin(time * 6f)));
            }
            _credit.Draw(); _repair.Draw();
        }

        /// <summary>Drop trail bits behind a projectile at about <paramref name="rate"/> per second.</summary>
        static void Emit(Trail trail, Vector3 at, float dt, float rate, float life, float size, Vector3 vel)
        {
            if (dt <= 0f || trail.Bits.Count > 1200) return;
            float n = rate * dt;
            int count = (int)n + (Random.value < n - (int)n ? 1 : 0);
            for (int i = 0; i < count; i++)
                trail.Bits.Add(new TrailBit { Pos = at, Vel = vel, Life = life, Max = life, Size = size, Rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) });
        }

        static void UpdateTrail(Trail trail, float dt)
        {
            trail.Batch.Clear();
            var bits = trail.Bits;
            int write = 0;
            for (int i = 0; i < bits.Count; i++)
            {
                var b = bits[i];
                b.Life -= dt;
                if (b.Life <= 0f) continue;
                b.Pos += b.Vel * dt;
                bits[write++] = b;
                trail.Batch.Add(b.Pos, b.Rot, Vector3.one * (b.Size * Mathf.Max(0.02f, b.Life / b.Max)));
            }
            bits.RemoveRange(write, bits.Count - write);
            trail.Batch.Draw();
        }

        public void Clear()
        {
            _smoke.Bits.Clear(); _hornetDots.Bits.Clear(); _pulseDots.Bits.Clear();
        }

        public void Dispose()
        {
            foreach (var m in _meshes) if (m != null) Object.Destroy(m);
            _meshes.Clear();
        }
    }
}
