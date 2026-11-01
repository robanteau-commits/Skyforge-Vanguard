using System.Collections.Generic;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The drifting asteroids that Bastion turrets ride on. Purely visual: each appears where a
    /// Bastion spawns and drifts down the screen at the world's scroll speed, so it stays under its
    /// turret and keeps drifting after the turret is destroyed. Sits just below the play plane.
    /// Uses the Blender model <c>prop_asteroid</c> if there is one.
    /// </summary>
    public sealed class Asteroids
    {
        const float Depth = -1.4f;
        const int Variants = 3;

        sealed class Rock
        {
            public GameObject Go;
            public Vector3 Start;
            public float Born;
            public float SpinSpeed;
        }

        readonly Transform _parent;
        readonly List<Rock> _live = new List<Rock>();
        readonly Stack<GameObject> _pool = new Stack<GameObject>();
        readonly Mesh[] _meshes = new Mesh[Variants];
        int _next;

        public Asteroids(Transform parent)
        {
            _parent = parent;
            for (int i = 0; i < Variants; i++) _meshes[i] = EnemyModels.AsteroidMesh(i);
        }

        public void Consume(List<SimEvent> events, float time)
        {
            foreach (var e in events)
            {
                if (e.Type != SimEventType.BastionSpawned) continue;
                var go = _pool.Count > 0 ? _pool.Pop() : Create();
                go.SetActive(true);
                _live.Add(new Rock
                {
                    Go = go,
                    Start = new Vector3(e.Pos.X, Depth, e.Pos.Y),
                    Born = time,
                    SpinSpeed = (e.EnemyId % 2 == 0 ? 1f : -1f) * (6f + e.EnemyId % 5),
                });
            }
        }

        GameObject Create()
        {
            var model = ModelLibrary.TryCreate("prop_asteroid", _parent);
            if (model != null) return model;
            var go = new GameObject("Asteroid");
            go.transform.SetParent(_parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _meshes[_next++ % Variants];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Mats.Asteroid, Mats.AsteroidDark };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        public void Render(float time)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var r = _live[i];
                float age = time - r.Born;
                var pos = r.Start + new Vector3(0f, 0f, -Field.ScrollSpeed * age);
                if (pos.z < -Field.HalfHeight - 6f)
                {
                    r.Go.SetActive(false);
                    _pool.Push(r.Go);
                    _live.RemoveAt(i);
                    continue;
                }
                r.Go.transform.localPosition = pos;
                r.Go.transform.localRotation = Quaternion.Euler(0f, age * r.SpinSpeed, 0f);
            }
        }

        public void Clear()
        {
            foreach (var r in _live) { r.Go.SetActive(false); _pool.Push(r.Go); }
            _live.Clear();
        }

        public void Dispose()
        {
            foreach (var m in _meshes) if (m != null) Object.Destroy(m);
        }
    }
}
