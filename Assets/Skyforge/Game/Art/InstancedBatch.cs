using UnityEngine;
using UnityEngine.Rendering;

namespace Skyforge.Game
{
    /// <summary>
    /// Draws many copies of one mesh with one material through GPU instancing, without a
    /// GameObject per copy. Fill it each frame with <see cref="Add"/>, then <see cref="Draw"/>.
    /// Used for bullets and effect particles (docs/DESIGN.md §10.3 performance budget).
    /// </summary>
    public sealed class InstancedBatch
    {
        const int Chunk = 511;
        readonly Mesh _mesh;
        readonly RenderParams _params;
        Matrix4x4[] _matrices = new Matrix4x4[256];
        int _count;

        public InstancedBatch(Mesh mesh, Material material)
        {
            _mesh = mesh;
            material.enableInstancing = true;
            _params = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0,
                worldBounds = new Bounds(Vector3.zero, new Vector3(400f, 200f, 400f)),
            };
        }

        public int Count => _count;

        public void Clear() => _count = 0;

        public void Add(in Matrix4x4 m)
        {
            if (_count == _matrices.Length) System.Array.Resize(ref _matrices, _count * 2);
            _matrices[_count++] = m;
        }

        public void Add(Vector3 pos, Quaternion rot, Vector3 scale) => Add(Matrix4x4.TRS(pos, rot, scale));

        public void Draw()
        {
            for (int start = 0; start < _count; start += Chunk)
            {
                int n = Mathf.Min(Chunk, _count - start);
                Graphics.RenderMeshInstanced(_params, _mesh, 0, _matrices, n, start);
            }
        }
    }
}
