using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>Draws every projectile with GPU instancing, one batch per visual.</summary>
    public sealed class ShotRenderer
    {
        readonly InstancedBatch _pulse;
        readonly Mesh _bolt;

        public ShotRenderer()
        {
            var b = new MeshBuilder();
            b.Octahedron(Vector3.zero, new Vector3(0.09f, 0.06f, 0.42f), 0);
            _bolt = b.Build("PulseBolt");
            _pulse = new InstancedBatch(_bolt, Mats.PlayerShot);
        }

        /// <summary>
        /// Draw the shots as they were <paramref name="behind"/> seconds before the latest step
        /// (render interpolation: 0 = latest position).
        /// </summary>
        public void Draw(StageSim sim, float behind)
        {
            _pulse.Clear();
            var rot = Quaternion.identity;
            var scale = Vector3.one;
            foreach (var s in sim.Shots)
            {
                var p = s.Pos - s.Vel * behind;
                switch (s.Visual)
                {
                    case ShotVisual.PulseBolt: _pulse.Add(new Vector3(p.X, -0.13f, p.Y), rot, scale); break;
                }
            }
            _pulse.Draw();
        }

        public void Dispose()
        {
            if (_bolt != null) Object.Destroy(_bolt);
        }
    }
}
