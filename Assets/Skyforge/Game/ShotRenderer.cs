using System.Collections.Generic;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Draws every projectile and pickup with GPU instancing. Enemy bullets are the brightest
    /// things on screen (hot orange-pink) with a dark maroon rim behind them, so they read over
    /// anything; darts point along their flight.
    /// </summary>
    public sealed class ShotRenderer
    {
        const float PlayY = -0.13f;

        readonly InstancedBatch _pulse, _orb, _orbRim, _dart, _dartRim, _credit, _repair;
        readonly InstancedBatch _pellet, _lance, _missile, _micro, _missileTail, _disc;
        readonly List<Mesh> _meshes = new List<Mesh>();

        public ShotRenderer()
        {
            var bolt = Diamond(new Vector3(0.09f, 0.06f, 0.42f), "PulseBolt");
            var orb = Diamond(new Vector3(0.2f, 0.2f, 0.2f), "Orb");
            var orbRim = Diamond(new Vector3(0.3f, 0.1f, 0.3f), "OrbRim");
            var dart = Diamond(new Vector3(0.1f, 0.08f, 0.32f), "Dart");
            var dartRim = Diamond(new Vector3(0.16f, 0.06f, 0.42f), "DartRim");
            var shard = Diamond(new Vector3(0.14f, 0.2f, 0.14f), "Shard");
            var orbBig = Diamond(new Vector3(0.3f, 0.3f, 0.3f), "RepairOrb");
            _pulse = new InstancedBatch(bolt, Mats.PlayerShot);
            _orb = new InstancedBatch(orb, Mats.EnemyBullet);
            _orbRim = new InstancedBatch(orbRim, Mats.BulletRim);
            _dart = new InstancedBatch(dart, Mats.EnemyBullet);
            _dartRim = new InstancedBatch(dartRim, Mats.BulletRim);
            _credit = new InstancedBatch(shard, Mats.Credit);
            _repair = new InstancedBatch(orbBig, Mats.Repair);

            _pellet = new InstancedBatch(Diamond(new Vector3(0.11f, 0.08f, 0.16f), "Pellet"), Mats.PlayerShot);
            _lance = new InstancedBatch(Diamond(new Vector3(0.14f, 0.08f, 0.85f), "LanceBolt"), Mats.PlayerShot);
            _missile = new InstancedBatch(Diamond(new Vector3(0.09f, 0.09f, 0.34f), "Missile"), Mats.HullLight);
            _micro = new InstancedBatch(Diamond(new Vector3(0.06f, 0.06f, 0.22f), "MicroMissile"), Mats.HullLight);
            _missileTail = new InstancedBatch(Diamond(new Vector3(0.07f, 0.07f, 0.16f), "MissileTail"), Mats.PlayerGlow);
            _disc = new InstancedBatch(Diamond(new Vector3(0.36f, 0.05f, 0.36f), "Disc"), Mats.PlayerShot);
        }

        Mesh Diamond(Vector3 r, string name)
        {
            var b = new MeshBuilder();
            b.Octahedron(Vector3.zero, r, 0);
            var m = b.Build(name);
            _meshes.Add(m);
            return m;
        }

        /// <summary>
        /// Draw everything as it was <paramref name="behind"/> seconds before the latest step
        /// (render interpolation: 0 = latest position).
        /// </summary>
        public void Draw(StageSim sim, float behind, float time)
        {
            _pulse.Clear(); _pellet.Clear(); _lance.Clear(); _missile.Clear(); _micro.Clear(); _missileTail.Clear(); _disc.Clear();
            var rot = Quaternion.identity;
            var one = Vector3.one;
            foreach (var s in sim.Shots)
            {
                var p = s.Pos - s.Vel * behind;
                var pos = new Vector3(p.X, PlayY, p.Y);
                var along = s.Vel.LengthSq > 0.01f ? Quaternion.LookRotation(new Vector3(s.Vel.X, 0f, s.Vel.Y)) : rot;
                switch (s.Visual)
                {
                    case ShotVisual.Pellet: _pellet.Add(pos, along, one); break;
                    case ShotVisual.LanceBolt: _lance.Add(pos, along, one * (s.Radius / 0.22f)); break;
                    case ShotVisual.Missile:
                    case ShotVisual.MicroMissile:
                    {
                        bool micro = s.Visual == ShotVisual.MicroMissile;
                        (micro ? _micro : _missile).Add(pos, along, one);
                        var back = along * Vector3.back * (micro ? 0.25f : 0.38f);
                        float flicker = 0.8f + 0.4f * Mathf.PerlinNoise(time * 25f, s.Pos.X * 3f);
                        _missileTail.Add(pos + back, along, new Vector3(1f, 1f, flicker) * (micro ? 0.7f : 1f));
                        break;
                    }
                    case ShotVisual.Disc: _disc.Add(pos, Quaternion.Euler(0f, time * 900f, 0f), one); break;
                    default: _pulse.Add(pos, rot, one); break;
                }
            }
            _pulse.Draw(); _pellet.Draw(); _lance.Draw(); _missile.Draw(); _micro.Draw(); _missileTail.Draw(); _disc.Draw();

            _orb.Clear(); _orbRim.Clear(); _dart.Clear(); _dartRim.Clear();
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

        public void Dispose()
        {
            foreach (var m in _meshes) if (m != null) Object.Destroy(m);
            _meshes.Clear();
        }
    }
}
