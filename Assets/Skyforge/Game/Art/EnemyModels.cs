using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Veil Armada ships: angular, dark, with red/magenta glowing seams so they never read as the
    /// player. Built from code, one mesh per kind, shared by every instance.
    /// </summary>
    public static class EnemyModels
    {
        const int Hull = 0, Plate = 1, Glow = 2;

        /// <summary>Blender model name for an enemy kind: <c>enemy_</c> + the kind in camelCase
        /// (e.g. <c>enemy_practiceDrone</c>).</summary>
        public static string ModelName(EnemyKind kind)
        {
            string s = kind.ToString();
            return "enemy_" + char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>The enemy's Blender model if there is one (docs/BLENDER.md), else the code-built one.</summary>
        public static GameObject Create(EnemyKind kind, Transform parent)
        {
            var model = ModelLibrary.TryCreate(ModelName(kind), parent);
            if (model != null) return model;
            var b = new MeshBuilder();
            switch (kind)
            {
                case EnemyKind.PracticeDrone: PracticeDrone(b); break;
            }
            var go = b.BuildObject(kind.ToString(), parent, Mats.EnemyHull, Mats.EnemyPlate, Mats.EnemyGlow);
            return go;
        }

        /// <summary>Target drone: a flat diamond with four vanes and a glowing eye. Radius ≈ 0.75.</summary>
        static void PracticeDrone(MeshBuilder b)
        {
            b.Octahedron(Vector3.zero, new Vector3(0.5f, 0.2f, 0.5f), Hull);
            b.Octahedron(new Vector3(0f, 0.14f, 0f), new Vector3(0.28f, 0.1f, 0.28f), Plate);
            b.Octahedron(new Vector3(0f, 0.24f, 0f), new Vector3(0.11f, 0.08f, 0.11f), Glow);
            for (int i = 0; i < 4; i++)
            {
                b.Push(Matrix4x4.Rotate(Quaternion.Euler(0f, 45f + 90f * i, 0f)));
                b.ExtrudeXZ(new[]
                {
                    new Vector2(-0.07f, 0.35f), new Vector2(0.07f, 0.35f), new Vector2(0.05f, 0.78f), new Vector2(-0.05f, 0.78f),
                }, -0.03f, 0.03f, Plate);
                b.Box(new Vector3(0f, 0.04f, 0.72f), new Vector3(0.07f, 0.04f, 0.1f), Glow);
                b.Pop();
            }
        }
    }
}
