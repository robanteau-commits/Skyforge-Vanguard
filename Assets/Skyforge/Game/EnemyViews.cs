using System.Collections.Generic;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// One pooled GameObject per live enemy, kept in step with the simulation. Positions are
    /// interpolated between the previous and latest step; ships turn to their facing; hits make the
    /// model flinch; an attack winding up shows a growing muzzle glow (the telegraph).
    /// </summary>
    public sealed class EnemyViews
    {
        sealed class View
        {
            public GameObject Go;
            public EnemyKind Kind;
            public Vector3 Prev, Curr;
            public float Facing, Telegraph, Radius;
            public float Flinch;
            public float Spin;
            public bool Seen;
        }

        readonly Transform _parent;
        readonly Dictionary<int, View> _live = new Dictionary<int, View>();
        readonly Dictionary<EnemyKind, Stack<GameObject>> _pool = new Dictionary<EnemyKind, Stack<GameObject>>();
        readonly List<int> _gone = new List<int>();
        readonly InstancedBatch _glow;
        readonly Mesh _glowMesh;

        public EnemyViews(Transform parent)
        {
            _parent = parent;
            var b = new MeshBuilder();
            b.Octahedron(Vector3.zero, Vector3.one, 0);
            _glowMesh = b.Build("TelegraphGlow");
            _glow = new InstancedBatch(_glowMesh, Mats.Telegraph);
        }

        /// <summary>Call right after every simulation step.</summary>
        public void AfterStep(StageSim sim)
        {
            foreach (var v in _live.Values) v.Seen = false;
            foreach (var e in sim.Enemies)
            {
                var pos = Fx.ToWorld(e.Pos);
                if (!_live.TryGetValue(e.Id, out var v))
                {
                    v = new View { Go = Take(e.Kind), Kind = e.Kind, Curr = pos, Facing = e.Facing, Spin = e.Phase * 57f };
                    v.Go.transform.localRotation = Quaternion.Euler(0f, e.Facing, 0f); // pooled views keep old headings
                    v.Go.transform.localScale = Vector3.one;
                    _live.Add(e.Id, v);
                }
                v.Seen = true;
                v.Prev = v.Curr;
                v.Curr = pos;
                v.Facing = e.Facing;
                v.Telegraph = e.Telegraph;
                v.Radius = e.Radius;
            }
            foreach (var ev in sim.Events)
                if (ev.Type == SimEventType.Hit && _live.TryGetValue(ev.EnemyId, out var hit)) hit.Flinch = 1f;

            _gone.Clear();
            foreach (var kv in _live) if (!kv.Value.Seen) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                Give(_live[id]);
                _live.Remove(id);
            }
        }

        /// <summary>Place every view; <paramref name="alpha"/> = 0..1 between previous and latest step.</summary>
        public void Render(float alpha, float dt)
        {
            _glow.Clear();
            foreach (var v in _live.Values)
            {
                var t = v.Go.transform;
                var pos = Vector3.Lerp(v.Prev, v.Curr, alpha);
                t.localPosition = pos;
                if (EnemyModels.Spins(v.Kind))
                {
                    v.Spin += dt * 60f;
                    t.localRotation = Quaternion.Euler(0f, v.Spin, 0f);
                }
                else
                {
                    // Turn smoothly towards the facing the simulation reports.
                    var target = Quaternion.Euler(0f, v.Facing, 0f);
                    t.localRotation = dt > 0f ? Quaternion.RotateTowards(t.localRotation, target, 540f * dt) : t.localRotation;
                }
                v.Flinch = Mathf.Max(0f, v.Flinch - dt * 12f);
                t.localScale = Vector3.one * (1f - 0.12f * v.Flinch);

                if (v.Telegraph > 0.02f)
                {
                    // Muzzle glow just ahead of the ship, growing and pulsing as the attack winds up.
                    var dir = Quaternion.Euler(0f, v.Facing, 0f) * Vector3.forward;
                    float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 40f);
                    float size = (0.12f + 0.3f * v.Telegraph) * pulse * Mathf.Clamp(v.Radius, 0.6f, 1.2f);
                    _glow.Add(pos + dir * (v.Radius * 0.7f) + Vector3.up * 0.3f, Quaternion.identity, Vector3.one * size);
                }
            }
            _glow.Draw();
        }

        public void Clear()
        {
            foreach (var v in _live.Values) Give(v);
            _live.Clear();
        }

        GameObject Take(EnemyKind kind)
        {
            if (_pool.TryGetValue(kind, out var stack) && stack.Count > 0)
            {
                var go = stack.Pop();
                go.SetActive(true);
                return go;
            }
            return EnemyModels.Create(kind, _parent);
        }

        void Give(View v)
        {
            v.Go.SetActive(false);
            if (!_pool.TryGetValue(v.Kind, out var stack)) _pool[v.Kind] = stack = new Stack<GameObject>();
            stack.Push(v.Go);
        }

        public void Dispose()
        {
            if (_glowMesh != null) Object.Destroy(_glowMesh);
        }
    }
}
