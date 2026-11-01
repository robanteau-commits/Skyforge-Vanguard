using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Veil Armada ships: angular, dark, with red/magenta glowing seams so they never read as the
    /// player. The Blender model is used when there is one (docs/BLENDER.md); otherwise these
    /// code-built stand-ins. Built nose towards +Z; the view turns them by <see cref="Enemy.Facing"/>.
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
                case EnemyKind.Gnat: Gnat(b); break;
                case EnemyKind.Needle: Needle(b); break;
                case EnemyKind.RamDrone: RamDrone(b); break;
                case EnemyKind.SpineMine: SpineMine(b); break;
                case EnemyKind.Warden: Warden(b); break;
                case EnemyKind.Bastion: Bastion(b); break;
                case EnemyKind.BroodCarrier: BroodCarrier(b); break;
                case EnemyKind.BroodBay: BroodBay(b); break;
                case EnemyKind.Tidebreaker: Tidebreaker(b); break;
                case EnemyKind.TideCannon: TideCannon(b); break;
                case EnemyKind.TideMissileBay: TideMissileBay(b); break;
                case EnemyKind.TideEye: TideEye(b); break;
                case EnemyKind.TideMissile: TideMissile(b); break;
                default: PracticeDrone(b); break;
            }
            var go = b.BuildObject(kind.ToString(), parent, Mats.EnemyHull, Mats.EnemyPlate, Mats.EnemyGlow);
            if (kind == EnemyKind.TideEye)
            {
                // The armoured iris over the eye: shown while it's shielded (EnemyViews toggles "Shield").
                var lid = new MeshBuilder();
                TideEyeShield(lid);
                lid.BuildObject(ShieldChild, go.transform, Mats.EnemyPlate, Mats.EnemyHull);
            }
            return go;
        }

        /// <summary>A child with this name is shown only while the enemy is shielded.</summary>
        public const string ShieldChild = "Shield";

        /// <summary>Kinds that spin in place instead of pointing where they go.</summary>
        public static bool Spins(EnemyKind kind) => kind == EnemyKind.PracticeDrone;

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

        /// <summary>Gnat: a small arrowhead with swept-back barbs. Radius ≈ 0.55.</summary>
        static void Gnat(MeshBuilder b)
        {
            b.ExtrudeXZ(new[]
            {
                new Vector2(0f, 0.6f), new Vector2(0.5f, -0.4f), new Vector2(0.16f, -0.22f),
                new Vector2(0f, -0.38f), new Vector2(-0.16f, -0.22f), new Vector2(-0.5f, -0.4f),
            }, -0.06f, 0.06f, Hull);
            b.Octahedron(new Vector3(0f, 0.08f, 0.05f), new Vector3(0.14f, 0.1f, 0.3f), Plate);
            b.Octahedron(new Vector3(0f, 0.13f, 0.12f), new Vector3(0.06f, 0.05f, 0.1f), Glow);
            b.Beam(new Vector3(0.08f, 0.07f, 0.45f), new Vector3(0.45f, 0.07f, -0.33f), 0.035f, 0.02f, Glow);
            b.Beam(new Vector3(-0.08f, 0.07f, 0.45f), new Vector3(-0.45f, 0.07f, -0.33f), 0.035f, 0.02f, Glow);
        }

        /// <summary>Needle: a long, thin lance with small fins and a glowing spine. Radius ≈ 0.6.</summary>
        static void Needle(MeshBuilder b)
        {
            b.Loft(new[]
            {
                new MeshBuilder.Ring(1.0f, 0f, 0f),
                new MeshBuilder.Ring(0.4f, 0.16f, 0.12f),
                new MeshBuilder.Ring(-0.45f, 0.2f, 0.14f),
                new MeshBuilder.Ring(-0.75f, 0.12f, 0.09f),
            }, 6, Hull, startDeg: 90f);
            for (int s = -1; s <= 1; s += 2)
            {
                b.ExtrudeXZ(new[]
                {
                    new Vector2(0.12f * s, 0f), new Vector2(0.62f * s, -0.55f), new Vector2(0.55f * s, -0.7f), new Vector2(0.14f * s, -0.5f),
                }, -0.03f, 0.03f, Plate);
            }
            b.Beam(new Vector3(0f, 0.15f, 0.55f), new Vector3(0f, 0.15f, -0.55f), 0.04f, 0.03f, Glow);
            b.Octahedron(new Vector3(0f, 0f, 0.95f), new Vector3(0.05f, 0.05f, 0.1f), Glow);
        }

        /// <summary>Ram Drone: a heavy wedge bristling with forward spikes. Radius ≈ 0.6.</summary>
        static void RamDrone(MeshBuilder b)
        {
            b.Octahedron(Vector3.zero, new Vector3(0.5f, 0.26f, 0.55f), Hull);
            b.Octahedron(new Vector3(0f, 0.12f, -0.1f), new Vector3(0.3f, 0.16f, 0.35f), Plate);
            for (int i = -1; i <= 1; i++)
                b.Octahedron(new Vector3(i * 0.28f, 0f, 0.55f - Mathf.Abs(i) * 0.12f), new Vector3(0.07f, 0.07f, 0.32f), Plate);
            b.Octahedron(new Vector3(0f, 0.2f, 0.22f), new Vector3(0.12f, 0.08f, 0.12f), Glow);
        }

        /// <summary>Spine Mine: a dark core studded with spikes, glowing at the heart. Radius ≈ 0.6.</summary>
        static void SpineMine(MeshBuilder b)
        {
            b.Octahedron(Vector3.zero, new Vector3(0.36f, 0.3f, 0.36f), Hull);
            for (int i = 0; i < 8; i++)
            {
                b.Push(Matrix4x4.Rotate(Quaternion.Euler(0f, i * 45f, 0f)));
                b.Octahedron(new Vector3(0f, 0f, 0.45f), new Vector3(0.06f, 0.06f, 0.22f), Plate);
                if (i % 2 == 0) b.Box(new Vector3(0f, 0f, 0.65f), new Vector3(0.05f, 0.05f, 0.05f), Glow);
                b.Pop();
            }
            b.Octahedron(new Vector3(0f, 0.22f, 0f), new Vector3(0.13f, 0.1f, 0.13f), Glow);
        }

        /// <summary>Warden: a broad hexagonal gunship with side pods and a glowing ring. Radius ≈ 1.5.</summary>
        static void Warden(MeshBuilder b)
        {
            var hex = new Vector2[6];
            var inner = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (90f + 60f * i) * Mathf.Deg2Rad;
                hex[i] = new Vector2(Mathf.Cos(a) * 1.35f, Mathf.Sin(a) * 1.35f);
                inner[i] = new Vector2(Mathf.Cos(a) * 0.8f, Mathf.Sin(a) * 0.8f);
            }
            b.ExtrudeXZ(hex, -0.2f, 0.18f, Hull);
            b.ExtrudeXZ(inner, 0.18f, 0.38f, Plate);
            for (int i = 0; i < 6; i++)
            {
                var a = hex[i] * 0.93f;
                var c = hex[(i + 1) % 6] * 0.93f;
                if (i % 2 == 0) b.Beam(new Vector3(a.x, 0.2f, a.y), new Vector3(c.x, 0.2f, c.y), 0.06f, 0.03f, Glow);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(1.45f * s, 0f, -0.2f), new Vector3(0.45f, 0.35f, 1.1f), Plate);
                b.Box(new Vector3(1.45f * s, 0f, 0.42f), new Vector3(0.18f, 0.18f, 0.2f), Glow);
            }
            b.Octahedron(new Vector3(0f, 0.5f, 0f), new Vector3(0.28f, 0.16f, 0.28f), Glow);
            b.Box(new Vector3(0f, 0.05f, -1.3f), new Vector3(0.35f, 0.25f, 0.4f), Plate);
        }

        /// <summary>Bastion: a squat turret with a single heavy barrel (its asteroid is drawn separately). Radius ≈ 0.9.</summary>
        static void Bastion(MeshBuilder b)
        {
            var oct = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float a = (22.5f + 45f * i) * Mathf.Deg2Rad;
                oct[i] = new Vector2(Mathf.Cos(a) * 0.85f, Mathf.Sin(a) * 0.85f);
            }
            b.ExtrudeXZ(oct, -0.25f, 0.05f, Plate);
            b.Octahedron(new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.32f, 0.55f), Hull);
            b.Box(new Vector3(0f, 0.18f, 0.6f), new Vector3(0.18f, 0.16f, 0.8f), Plate);
            b.Box(new Vector3(0f, 0.18f, 1.02f), new Vector3(0.2f, 0.18f, 0.06f), Glow);
            b.Octahedron(new Vector3(0f, 0.42f, -0.05f), new Vector3(0.12f, 0.07f, 0.12f), Glow);
        }

        static Vector2[] Ring(int n, float r, float startDeg = 90f)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = (startDeg + 360f * i / n) * Mathf.Deg2Rad;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return pts;
        }

        /// <summary>Brood Carrier: a broad armoured wedge with a glowing keel; its two bays are separate parts at x ±1.9. Radius ≈ 1.7.</summary>
        static void BroodCarrier(MeshBuilder b)
        {
            b.ExtrudeXZ(new[]
            {
                new Vector2(0f, 1.9f), new Vector2(1.2f, 0.9f), new Vector2(1.5f, -0.6f), new Vector2(1.1f, -1.6f),
                new Vector2(-1.1f, -1.6f), new Vector2(-1.5f, -0.6f), new Vector2(-1.2f, 0.9f),
            }, -0.3f, 0.2f, Hull);
            b.ExtrudeXZ(new[]
            {
                new Vector2(0f, 1.3f), new Vector2(0.75f, 0.5f), new Vector2(0.8f, -1.2f), new Vector2(-0.8f, -1.2f), new Vector2(-0.75f, 0.5f),
            }, 0.2f, 0.45f, Plate);
            b.Beam(new Vector3(0f, 0.47f, 1.1f), new Vector3(0f, 0.47f, -1.1f), 0.12f, 0.04f, Glow);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Beam(new Vector3(1.15f * s, 0.22f, 0.85f), new Vector3(1.45f * s, 0.22f, -0.6f), 0.06f, 0.03f, Glow);
                b.Box(new Vector3(0.55f * s, 0.1f, -1.75f), new Vector3(0.4f, 0.35f, 0.3f), Plate);
                b.Box(new Vector3(0.55f * s, 0.1f, -1.93f), new Vector3(0.26f, 0.2f, 0.06f), Glow);
            }
            b.Octahedron(new Vector3(0f, 0.6f, 0.2f), new Vector3(0.3f, 0.18f, 0.4f), Glow);
        }

        /// <summary>Launch bay: a boxy pod with a glowing mouth facing forward. Radius ≈ 0.8.</summary>
        static void BroodBay(MeshBuilder b)
        {
            b.ExtrudeXZ(Ring(6, 0.75f, 0f), -0.25f, 0.25f, Plate);
            b.Box(new Vector3(0f, 0.3f, -0.1f), new Vector3(0.7f, 0.2f, 0.9f), Hull);
            b.Box(new Vector3(0f, 0.05f, 0.7f), new Vector3(0.6f, 0.32f, 0.12f), Glow);
            b.Box(new Vector3(0f, 0.42f, -0.1f), new Vector3(0.12f, 0.06f, 0.6f), Glow);
        }

        /// <summary>
        /// The Tidebreaker's hull: an armoured sea-fortress filling the top of the screen. Its parts
        /// sit on top: cannons at local x ±5.5 (z 1.2), the missile bay behind (z −2.4), the eye in
        /// front (z 1.6). Built for the 18-wide field; on wide screens it simply has room either side.
        /// </summary>
        static void Tidebreaker(MeshBuilder b)
        {
            // Main deck: a wide, low hexagonal slab with a prow.
            b.ExtrudeXZ(new[]
            {
                new Vector2(0f, 3.3f), new Vector2(3.2f, 2.4f), new Vector2(7.2f, 1.6f), new Vector2(7.6f, -0.6f),
                new Vector2(6.2f, -3.2f), new Vector2(-6.2f, -3.2f), new Vector2(-7.6f, -0.6f), new Vector2(-7.2f, 1.6f), new Vector2(-3.2f, 2.4f),
            }, -0.8f, 0.0f, Hull);
            // Raised central citadel.
            b.ExtrudeXZ(new[]
            {
                new Vector2(0f, 2.8f), new Vector2(2.4f, 1.8f), new Vector2(2.8f, -2.9f), new Vector2(-2.8f, -2.9f), new Vector2(-2.4f, 1.8f),
            }, 0f, 0.35f, Plate);
            // Glowing seams and the waterline.
            b.Beam(new Vector3(-3.2f, 0.02f, 2.35f), new Vector3(-7.1f, 0.02f, 1.55f), 0.1f, 0.04f, Glow);
            b.Beam(new Vector3(3.2f, 0.02f, 2.35f), new Vector3(7.1f, 0.02f, 1.55f), 0.1f, 0.04f, Glow);
            b.Beam(new Vector3(-6.1f, -0.4f, -3.25f), new Vector3(6.1f, -0.4f, -3.25f), 0.1f, 0.12f, Glow);
            for (int s = -1; s <= 1; s += 2)
            {
                // Cannon platforms and sponsons.
                b.ExtrudeXZ(new[]
                {
                    new Vector2(5.5f * s + 1.3f, 2.2f), new Vector2(5.5f * s + 1.5f, 0.2f), new Vector2(5.5f * s - 1.5f, 0.2f), new Vector2(5.5f * s - 1.3f, 2.2f),
                }, 0f, 0.15f, Plate);
                // Towers at the back corners.
                b.Box(new Vector3(4.6f * s, 0.6f, -2.2f), new Vector3(1.0f, 1.2f, 1.1f), Plate);
                b.Box(new Vector3(4.6f * s, 1.25f, -2.2f), new Vector3(0.5f, 0.12f, 0.5f), Glow);
                // Armour ribs along the sides.
                for (int k = 0; k < 3; k++)
                    b.Box(new Vector3((3.2f + k * 1.1f) * s, 0.12f, -0.9f), new Vector3(0.25f, 0.25f, 3.6f), Hull);
            }
            b.Box(new Vector3(0f, 0.36f, 0.4f), new Vector3(3.6f, 0.1f, 0.15f), Glow);
        }

        /// <summary>Flank cannon: a turret with a long twin barrel. Radius ≈ 1.1.</summary>
        static void TideCannon(MeshBuilder b)
        {
            b.ExtrudeXZ(Ring(8, 1.05f, 22.5f), 0.1f, 0.4f, Plate);
            b.Octahedron(new Vector3(0f, 0.5f, -0.1f), new Vector3(0.7f, 0.35f, 0.7f), Hull);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(0.2f * s, 0.55f, 0.9f), new Vector3(0.16f, 0.16f, 1.4f), Plate);
                b.Box(new Vector3(0.2f * s, 0.55f, 1.62f), new Vector3(0.2f, 0.2f, 0.08f), Glow);
            }
            b.Octahedron(new Vector3(0f, 0.85f, -0.2f), new Vector3(0.18f, 0.1f, 0.18f), Glow);
        }

        /// <summary>Missile bay: an armoured block with rows of glowing launch tubes. Radius ≈ 1.2.</summary>
        static void TideMissileBay(MeshBuilder b)
        {
            b.Box(new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.7f, 1.6f), Plate);
            b.Box(new Vector3(0f, 0.85f, 0f), new Vector3(2.0f, 0.12f, 1.2f), Hull);
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 2; j++)
                    b.Box(new Vector3(-0.75f + i * 0.5f, 0.93f, -0.3f + j * 0.6f), new Vector3(0.28f, 0.06f, 0.28f), Glow);
        }

        /// <summary>The core eye: a great glowing orb in an octagonal mount. Radius ≈ 1.4.</summary>
        static void TideEye(MeshBuilder b)
        {
            b.ExtrudeXZ(Ring(8, 1.45f, 22.5f), 0.1f, 0.45f, Plate);
            b.ExtrudeXZ(Ring(8, 1.15f, 22.5f), 0.45f, 0.55f, Hull);
            b.Octahedron(new Vector3(0f, 0.75f, 0f), new Vector3(0.85f, 0.5f, 0.85f), Glow);
        }

        /// <summary>Armoured iris plates closed over the eye (only while it's shielded).</summary>
        static void TideEyeShield(MeshBuilder b)
        {
            for (int i = 0; i < 6; i++)
            {
                b.Push(Matrix4x4.Rotate(Quaternion.Euler(0f, i * 60f, 0f)));
                b.ExtrudeXZ(new[] { new Vector2(0f, 0f), new Vector2(0.6f, 1.05f), new Vector2(-0.6f, 1.05f) }, 0.6f, 1.05f, 0);
                b.Pop();
            }
            b.Octahedron(new Vector3(0f, 1.1f, 0f), new Vector3(0.25f, 0.12f, 0.25f), 1);
        }

        /// <summary>Missile: a short dart with a glowing nose and tail. Radius ≈ 0.45.</summary>
        static void TideMissile(MeshBuilder b)
        {
            b.Loft(new[]
            {
                new MeshBuilder.Ring(0.55f, 0f, 0f),
                new MeshBuilder.Ring(0.25f, 0.13f, 0.13f),
                new MeshBuilder.Ring(-0.4f, 0.13f, 0.13f),
            }, 4, Hull, startDeg: 45f);
            for (int s = -1; s <= 1; s += 2)
                b.ExtrudeXZ(new[] { new Vector2(0.1f * s, -0.15f), new Vector2(0.35f * s, -0.45f), new Vector2(0.1f * s, -0.4f) }, -0.02f, 0.02f, Plate);
            b.Octahedron(new Vector3(0f, 0f, 0.5f), new Vector3(0.07f, 0.07f, 0.1f), Glow);
            b.Box(new Vector3(0f, 0f, -0.45f), new Vector3(0.18f, 0.18f, 0.08f), Glow);
        }

        /// <summary>The asteroid a Bastion rides on: a lumpy cluster of rock, below the turret.</summary>
        public static Mesh AsteroidMesh(int variant)
        {
            var b = new MeshBuilder();
            float r = 2f + variant * 0.3f;
            b.Octahedron(Vector3.zero, new Vector3(r, r * 0.55f, r * 0.9f), 0);
            b.Octahedron(new Vector3(r * 0.5f, -0.1f, r * 0.35f), new Vector3(r * 0.6f, r * 0.45f, r * 0.55f), 1);
            b.Octahedron(new Vector3(-r * 0.45f, -0.05f, -r * 0.4f), new Vector3(r * 0.55f, r * 0.4f, r * 0.6f), 1);
            if (variant % 2 == 1) b.Octahedron(new Vector3(-r * 0.6f, 0f, r * 0.45f), new Vector3(r * 0.35f, r * 0.35f, r * 0.35f), 0);
            return b.Build("Asteroid" + variant);
        }
    }
}
