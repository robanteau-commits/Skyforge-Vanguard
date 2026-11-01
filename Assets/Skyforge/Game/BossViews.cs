using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The Tidebreaker's lasers (docs/DESIGN.md §6.1): a thin flickering warning line first, then a
    /// hot beam that sweeps. Glow geometry drawn with instancing, purely visual.
    /// </summary>
    public sealed class BossViews
    {
        const float Length = 48f;
        readonly Mesh _box, _flare;
        readonly InstancedBatch _warn, _outer, _core, _flares;

        public BossViews()
        {
            var b = new MeshBuilder();
            b.Box(Vector3.zero, Vector3.one, 0);
            _box = b.Build("LaserBox");
            var f = new MeshBuilder();
            f.Octahedron(Vector3.zero, new Vector3(0.5f, 0.3f, 0.5f), 0);
            _flare = f.Build("LaserFlare");
            _warn = new InstancedBatch(_box, Mats.Telegraph);
            _outer = new InstancedBatch(_box, Mats.EnemyBullet);
            _core = new InstancedBatch(_box, Mats.Spark);
            _flares = new InstancedBatch(_flare, Mats.EnemyBullet);
        }

        public void Render(StageSim sim, float time)
        {
            _warn.Clear(); _outer.Clear(); _core.Clear(); _flares.Clear();
            foreach (var l in sim.Lasers)
            {
                var rot = Quaternion.Euler(0f, l.Heading, 0f);
                var origin = Fx.ToWorld(l.Origin, 0.4f);
                var mid = origin + rot * Vector3.forward * (Length * 0.5f);
                if (!l.Firing)
                {
                    // Flickers faster as it's about to fire.
                    float k = l.WarnFraction;
                    if (Mathf.Sin(time * (20f + 40f * k)) > -0.3f)
                        _warn.Add(mid, rot, new Vector3(0.06f + 0.1f * k, 0.05f, Length));
                    _flares.Add(origin, Quaternion.identity, Vector3.one * (0.4f + 0.8f * k));
                }
                else
                {
                    float pulse = 1f + 0.1f * Mathf.Sin(time * 50f);
                    float w = StageSim.LaserHalfWidth * 2f * pulse;
                    _outer.Add(mid, rot, new Vector3(w * 1.3f, 0.1f, Length));
                    _core.Add(mid + Vector3.up * 0.05f, rot, new Vector3(w * 0.4f, 0.1f, Length));
                    _flares.Add(origin, Quaternion.identity, Vector3.one * 1.6f * pulse);
                }
            }
            _warn.Draw(); _outer.Draw(); _core.Draw(); _flares.Draw();
        }

        public void Dispose()
        {
            if (_box != null) Object.Destroy(_box);
            if (_flare != null) Object.Destroy(_flare);
        }
    }
}
