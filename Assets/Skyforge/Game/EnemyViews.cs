using System.Collections.Generic;
using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// One pooled GameObject per live enemy, kept in step with the simulation. Positions are
    /// interpolated between the previous and latest step; hits make the model flinch.
    /// </summary>
    public sealed class EnemyViews
    {
        sealed class View
        {
            public GameObject Go;
            public EnemyKind Kind;
            public Vector3 Prev, Curr;
            public float Flinch;
            public float Spin;
        }

        readonly Transform _parent;
        readonly Dictionary<int, View> _live = new Dictionary<int, View>();
        readonly Dictionary<EnemyKind, Stack<GameObject>> _pool = new Dictionary<EnemyKind, Stack<GameObject>>();
        readonly List<int> _gone = new List<int>();
        readonly HashSet<int> _seen = new HashSet<int>();

        public EnemyViews(Transform parent) { _parent = parent; }

        /// <summary>Call right after every simulation step.</summary>
        public void AfterStep(StageSim sim)
        {
            _seen.Clear();
            foreach (var e in sim.Enemies)
            {
                _seen.Add(e.Id);
                var pos = Fx.ToWorld(e.Pos);
                if (!_live.TryGetValue(e.Id, out var v))
                {
                    v = new View { Go = Take(e.Kind), Kind = e.Kind, Curr = pos, Spin = e.Phase * 57f };
                    _live.Add(e.Id, v);
                }
                v.Prev = v.Curr;
                v.Curr = pos;
            }
            foreach (var ev in sim.Events)
                if (ev.Type == SimEventType.Hit && _live.TryGetValue(ev.EnemyId, out var hit)) hit.Flinch = 1f;

            _gone.Clear();
            foreach (var kv in _live) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                Give(_live[id]);
                _live.Remove(id);
            }
        }

        /// <summary>Place every view; <paramref name="alpha"/> = 0..1 between previous and latest step.</summary>
        public void Render(float alpha, float dt)
        {
            foreach (var v in _live.Values)
            {
                var t = v.Go.transform;
                t.localPosition = Vector3.Lerp(v.Prev, v.Curr, alpha);
                v.Spin += dt * 60f;
                t.localRotation = Quaternion.Euler(0f, v.Spin, 0f);
                v.Flinch = Mathf.Max(0f, v.Flinch - dt * 12f);
                t.localScale = Vector3.one * (1f - 0.12f * v.Flinch);
            }
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
    }
}
