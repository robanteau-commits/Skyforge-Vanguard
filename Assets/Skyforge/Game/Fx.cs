using System.Collections.Generic;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Cosmetic effects driven by simulation events: muzzle flashes, hit sparks and explosions
    /// (a flash, flying debris shards and embers). Particles are plain structs drawn with GPU
    /// instancing; they never touch gameplay. Uses Unity's Random, which is fine for visuals only.
    /// </summary>
    public sealed class Fx
    {
        struct Particle
        {
            public Vector3 Pos, Vel, Spin;
            public Quaternion Rot;
            public float Age, Life, Size;
            /// <summary>0 = shrink linearly; 1 = grow quickly then shrink (flashes).</summary>
            public byte Curve;
            public float Drag;
        }

        sealed class Layer
        {
            public readonly InstancedBatch Batch;
            public readonly List<Particle> Items = new List<Particle>(256);
            public Layer(Mesh mesh, Material mat) { Batch = new InstancedBatch(mesh, mat); }
        }

        readonly Layer _flash, _ember, _debris, _muzzle;
        readonly List<Mesh> _meshes = new List<Mesh>();

        public Fx()
        {
            var ball = Octa(1f);
            var shard = TetraMesh();
            _flash = new Layer(ball, Mats.Explosion);
            _ember = new Layer(ball, Mats.Spark);
            _debris = new Layer(shard, Mats.Debris);
            _muzzle = new Layer(ball, Mats.PlayerGlow);
        }

        Mesh Octa(float r)
        {
            var b = new MeshBuilder();
            b.Octahedron(Vector3.zero, new Vector3(r, r, r), 0);
            var m = b.Build("FxOcta");
            _meshes.Add(m);
            return m;
        }

        Mesh TetraMesh()
        {
            var b = new MeshBuilder();
            b.Tetra(Vector3.zero, 1f, 0);
            var m = b.Build("FxShard");
            _meshes.Add(m);
            return m;
        }

        public static Vector3 ToWorld(Vec2 p, float y = 0f) => new Vector3(p.X, y, p.Y);

        /// <summary>Spawn effects for this step's simulation events.</summary>
        public void Consume(List<SimEvent> events)
        {
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case SimEventType.Fired:
                        Spawn(_muzzle, ToWorld(e.Pos, -0.13f), Vector3.zero, 0.06f, 0.16f, 1);
                        break;
                    case SimEventType.Hit:
                        for (int i = 0; i < 2; i++)
                            Spawn(_ember, ToWorld(e.Pos, 0.1f), RandomDir(4f, 9f) + new Vector3(0, 0, -2f), 0.18f, 0.07f, 0);
                        break;
                    case SimEventType.Destroyed:
                        Explode(ToWorld(e.Pos), 1f);
                        break;
                }
            }
        }

        public void Explode(Vector3 at, float scale)
        {
            Spawn(_flash, at, Vector3.zero, 0.3f, 1.3f * scale, 1);
            Spawn(_flash, at + Random.insideUnitSphere * 0.3f, Vector3.zero, 0.22f, 0.8f * scale, 1);
            for (int i = 0; i < 9; i++)
                Spawn(_debris, at, RandomDir(3f, 8f) * scale + new Vector3(0, 0, -1.5f), Random.Range(0.5f, 0.9f), Random.Range(0.09f, 0.2f) * scale, 0, 1.5f);
            for (int i = 0; i < 8; i++)
                Spawn(_ember, at, RandomDir(6f, 14f) * scale, Random.Range(0.2f, 0.4f), Random.Range(0.06f, 0.12f) * scale, 0, 3f);
        }

        static Vector3 RandomDir(float min, float max)
        {
            var d = Random.insideUnitSphere;
            d.y *= 0.4f;
            return d.normalized * Random.Range(min, max);
        }

        static void Spawn(Layer layer, Vector3 pos, Vector3 vel, float life, float size, byte curve, float drag = 0f)
        {
            if (layer.Items.Count > 1500) return;
            layer.Items.Add(new Particle
            {
                Pos = pos, Vel = vel, Life = life, Size = size, Curve = curve, Drag = drag,
                Rot = Random.rotation,
                Spin = Random.insideUnitSphere * 540f,
            });
        }

        /// <summary>Advance and draw every particle.</summary>
        public void Tick(float dt)
        {
            Update(_flash, dt);
            Update(_ember, dt);
            Update(_debris, dt);
            Update(_muzzle, dt);
        }

        static void Update(Layer layer, float dt)
        {
            layer.Batch.Clear();
            var items = layer.Items;
            int write = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var p = items[i];
                p.Age += dt;
                if (p.Age >= p.Life) continue;
                p.Pos += p.Vel * dt;
                if (p.Drag > 0f) p.Vel *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.Rot = Quaternion.Euler(p.Spin * dt) * p.Rot;
                items[write++] = p;
                float t = p.Age / p.Life;
                float s = p.Curve == 1 ? (t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f) : 1f - t;
                layer.Batch.Add(p.Pos, p.Rot, Vector3.one * (p.Size * Mathf.Max(0.01f, s)));
            }
            items.RemoveRange(write, items.Count - write);
            layer.Batch.Draw();
        }

        public void Clear()
        {
            _flash.Items.Clear(); _ember.Items.Clear(); _debris.Items.Clear(); _muzzle.Items.Clear();
        }

        public void Dispose()
        {
            foreach (var m in _meshes) if (m != null) Object.Destroy(m);
            _meshes.Clear();
        }
    }
}
