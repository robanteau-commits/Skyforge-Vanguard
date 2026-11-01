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
            _pulse.Clear();
            var rot = Quaternion.identity;
            var one = Vector3.one;
            foreach (var s in sim.Shots)
            {
                var p = s.Pos - s.Vel * behind;
                _pulse.Add(new Vector3(p.X, PlayY, p.Y), rot, one);
            }
            _pulse.Draw();

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
